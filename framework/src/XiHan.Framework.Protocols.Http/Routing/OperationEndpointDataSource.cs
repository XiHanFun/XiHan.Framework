// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Primitives;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.Routing;

/// <summary>
/// 随操作登记表变化的操作端点集合
/// </summary>
/// <remarks>
/// <c>MapXiHanOperations()</c> 把它加进路由表（ADR-0251）：端点按需从登记表建出，变更令牌就是登记表的令牌，登记表一变，ASP.NET Core 的路由重新读端点。运行期登记的操作随即有路由，撤回的随即没有
/// 约定构建器记下的约定与收尾约定作用于每次建出的每个端点；约定在前、操作的元数据在后、收尾约定最后，与路由组里的端点同一个次序
/// </remarks>
internal sealed class OperationEndpointDataSource : EndpointDataSource
{
    /// <summary>
    /// 操作登记表
    /// </summary>
    private readonly OperationRegistry _registry;

    /// <summary>
    /// HTTP 选项（路由覆盖与前缀）
    /// </summary>
    private readonly HttpProtocolOptions _options;

    /// <summary>
    /// 应用容器，交给端点构建器
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>
    /// 端点执行的委托
    /// </summary>
    private readonly Func<HttpContext, OperationDescriptor, Task> _handle;

    /// <summary>
    /// 端点元数据里的绑定委托工厂
    /// </summary>
    private readonly Func<OperationDescriptor, OperationPayloadBinder> _binderOf;

    /// <summary>
    /// 端点元数据里的写出委托
    /// </summary>
    private readonly OperationResponseWriter _writer;

    /// <summary>
    /// 约定
    /// </summary>
    /// <remarks>
    /// 只在 <see cref="_gate"/> 内读写
    /// </remarks>
    private readonly List<Action<EndpointBuilder>> _conventions = [];

    /// <summary>
    /// 收尾约定
    /// </summary>
    /// <remarks>
    /// 只在 <see cref="_gate"/> 内读写
    /// </remarks>
    private readonly List<Action<EndpointBuilder>> _finallyConventions = [];

    /// <summary>
    /// 建端点与改约定的互斥锁
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 上次建出的端点
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 表示要重建：还没建过，或建过之后加了约定
    /// </remarks>
    private IReadOnlyList<Endpoint>? _endpoints;

    /// <summary>
    /// 上次建端点之前取的登记表令牌
    /// </summary>
    /// <remarks>
    /// 触发了就说明登记表在那之后变过，端点要重建
    /// </remarks>
    private IChangeToken? _built;

    /// <summary>
    /// 创建端点集合
    /// </summary>
    /// <param name="registry">操作登记表</param>
    /// <param name="options">HTTP 选项</param>
    /// <param name="services">应用容器</param>
    /// <param name="handle">端点执行的委托</param>
    /// <param name="binderOf">按操作给出绑定委托</param>
    /// <param name="writer">写出成功响应的委托</param>
    public OperationEndpointDataSource(OperationRegistry registry, HttpProtocolOptions options, IServiceProvider services, Func<HttpContext, OperationDescriptor, Task> handle, Func<OperationDescriptor, OperationPayloadBinder> binderOf, OperationResponseWriter writer)
    {
        _registry = registry;
        _options = options;
        _services = services;
        _handle = handle;
        _binderOf = binderOf;
        _writer = writer;
        Conventions = new ConventionBuilder(this);
    }

    /// <summary>
    /// 这组端点的约定构建器
    /// </summary>
    public IEndpointConventionBuilder Conventions { get; }

    /// <inheritdoc />
    public override IReadOnlyList<Endpoint> Endpoints
    {
        get
        {
            lock (_gate)
            {
                if (_endpoints is null || _built is null || _built.HasChanged)
                {
                    // 先取令牌再读登记表：读的过程中登记表又变了，令牌已经触发，下次读时再建
                    _built = _registry.GetChangeToken();
                    _endpoints = Build();
                }

                return _endpoints;
            }
        }
    }

    /// <inheritdoc />
    public override IChangeToken GetChangeToken() => _registry.GetChangeToken();

    /// <summary>
    /// 两个操作推导出同一条路由时的错误
    /// </summary>
    /// <remarks>
    /// 经 <c>OperationRegistry.EnforceAdditions</c> 挂上：新增的一组与已登记的、组内各项之间比较。只看发布给 HTTP 且不是 <see cref="Stability.Internal"/> 的操作；都不冲突为 <see langword="null"/>
    /// </remarks>
    /// <param name="options">HTTP 选项</param>
    /// <param name="existing">已登记的操作</param>
    /// <param name="added">新增的操作</param>
    public static Error? RouteConflictOf(HttpProtocolOptions options, IReadOnlyList<OperationDescriptor> existing, IReadOnlyList<OperationDescriptor> added)
    {
        var routes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var descriptor in existing.Where(IsPublished))
        {
            routes.TryAdd(options.RouteFor(descriptor).ToString(), descriptor.Id);
        }

        foreach (var descriptor in added.Where(IsPublished))
        {
            var route = options.RouteFor(descriptor).ToString();
            if (!routes.TryAdd(route, descriptor.Id))
            {
                return Error.Conflict(HttpErrors.DuplicateRoute, HttpMessages.RouteDuplicate, MessageArguments.Of(("route", route), ("existing", routes[route]), ("id", descriptor.Id)));
            }
        }

        return null;
    }

    /// <summary>
    /// 是否发布为 HTTP 端点
    /// </summary>
    /// <remarks>
    /// 内部稳定性的操作只供进程内调用，不对外发布
    /// </remarks>
    private static bool IsPublished(OperationDescriptor descriptor)
        => descriptor.Contract.IsExposedTo(Expose.Http) && descriptor.Contract.Stability != Stability.Internal;

    /// <summary>
    /// 加一条约定
    /// </summary>
    /// <remarks>
    /// 已建出的端点作废，下次读时带上它重建
    /// </remarks>
    private void AddConvention(Action<EndpointBuilder> convention, bool isFinally)
    {
        ArgumentNullException.ThrowIfNull(convention);
        lock (_gate)
        {
            (isFinally ? _finallyConventions : _conventions).Add(convention);
            _endpoints = null;
        }
    }

    /// <summary>
    /// 按登记表当前的内容建端点
    /// </summary>
    /// <remarks>
    /// 只在 <see cref="_gate"/> 内调用。路由冲突已由登记时的整组核对挡住，这里遇到同一条路由是登记表之外的缺陷，照实抛出
    /// </remarks>
    private List<Endpoint> Build()
    {
        var endpoints = new List<Endpoint>();
        foreach (var descriptor in _registry.ExposedTo(Expose.Http))
        {
            if (descriptor.Contract.Stability == Stability.Internal)
            {
                continue;
            }

            var route = _options.RouteFor(descriptor);
            var builder = new RouteEndpointBuilder(httpContext => _handle(httpContext, descriptor), RoutePatternFactory.Parse(route.Template), order: 0)
            {
                DisplayName = $"HTTP: {route}",
                ApplicationServices = _services,
            };
            foreach (var convention in _conventions)
            {
                convention(builder);
            }

            // 绑定与写出委托和描述一起挂在元数据上：UseXiHan() 据此在进管道前放入描述、在 Validate 阶段读请求体（ADR-0176），Idempotency 阶段回放时按同样的状态码写出
            builder.Metadata.Add(new HttpMethodMetadata([route.Method]));
            builder.Metadata.Add(descriptor);
            builder.Metadata.Add(_binderOf(descriptor));
            builder.Metadata.Add(_writer);
            builder.Metadata.Add(new EndpointNameMetadata(descriptor.Id));
            builder.Metadata.Add(new RouteNameMetadata(descriptor.Id));
            foreach (var convention in _finallyConventions)
            {
                convention(builder);
            }

            endpoints.Add(builder.Build());
        }

        return endpoints;
    }

    /// <summary>
    /// 把约定记到端点集合上的构建器
    /// </summary>
    /// <param name="owner">所属的端点集合</param>
    private sealed class ConventionBuilder(OperationEndpointDataSource owner) : IEndpointConventionBuilder
    {
        /// <inheritdoc />
        public void Add(Action<EndpointBuilder> convention) => owner.AddConvention(convention, isFinally: false);

        /// <inheritdoc />
        public void Finally(Action<EndpointBuilder> finallyConvention) => owner.AddConvention(finallyConvention, isFinally: true);
    }
}
