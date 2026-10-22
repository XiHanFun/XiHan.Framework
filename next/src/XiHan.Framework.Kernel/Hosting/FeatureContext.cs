// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 传给 <see cref="IFeature.Configure"/> 的上下文：服务集合、配置、管道、已解析的绑定与权限注册表。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class FeatureContext
{
    /// <summary>
    /// 创建特性上下文。只由 <see cref="AppBuilder"/> 在配置阶段按拓扑顺序创建。
    /// </summary>
    /// <param name="node">本特性在已解析图中的节点。</param>
    /// <param name="services">宿主的服务集合。</param>
    /// <param name="configuration">宿主配置。</param>
    /// <param name="pipeline">应用的管道构建器。</param>
    /// <param name="bindings">宿主解析出的全部绑定。</param>
    /// <param name="permissions">权限注册表。</param>
    /// <param name="operations">应用级操作登记表。</param>
    internal FeatureContext(
        ResolvedNode node,
        IServiceCollection services,
        IConfiguration configuration,
        PipelineBuilder pipeline,
        IReadOnlyDictionary<string, BindingValue> bindings,
        PermissionRegistry permissions,
        OperationRegistry operations)
    {
        Node = node;
        Services = services;
        Configuration = configuration;
        Pipeline = pipeline;
        Bindings = bindings;
        Permissions = permissions;
        Operations = operations;
    }

    /// <summary>
    /// 本特性在已解析图中的节点。
    /// </summary>
    public ResolvedNode Node { get; }

    /// <summary>
    /// 本特性的清单。
    /// </summary>
    public Manifest Manifest => Node.Manifest;

    /// <summary>
    /// 宿主的服务集合。
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// 宿主配置。
    /// </summary>
    public IConfiguration Configuration { get; }

    /// <summary>
    /// 应用的管道构建器。
    /// </summary>
    public PipelineBuilder Pipeline { get; }

    /// <summary>
    /// 宿主解析出的全部绑定。
    /// </summary>
    public IReadOnlyDictionary<string, BindingValue> Bindings { get; }

    /// <summary>
    /// 权限注册表。
    /// </summary>
    public PermissionRegistry Permissions { get; }

    /// <summary>
    /// 应用级操作登记表。
    /// </summary>
    public OperationRegistry Operations { get; }

    /// <summary>
    /// 把中间件注册为单例并加入管道指定阶段，归属为本特性的清单 ID。
    /// </summary>
    public FeatureContext AddMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>(PipelineStage stage, Placement placement = Placement.End)
        where TMiddleware : class, IPipelineMiddleware
    {
        Services.TryAddSingleton<TMiddleware>();
        Pipeline.UseAt<TMiddleware>(stage, placement, Manifest.Id);
        return this;
    }

    /// <summary>
    /// 登记一个操作：实现按作用域注册到容器，描述以本特性为归属进入登记表；重复 ID 或超限抛出对应错误。
    /// </summary>
    /// <typeparam name="TOperation">操作实现。</typeparam>
    /// <typeparam name="TRequest">请求类型。</typeparam>
    /// <typeparam name="TResponse">响应类型。</typeparam>
    /// <param name="contract">契约。</param>
    /// <param name="requestInfo">请求类型的序列化元数据（源生成）。</param>
    /// <param name="responseInfo">响应类型的序列化元数据（源生成）。</param>
    /// <param name="schema">请求与响应的 JSON Schema；由 <c>Kernel.Generators</c> 生成的登记代码提供，手写登记可省略。</param>
    public FeatureContext AddOperation<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TOperation, TRequest, TResponse>(
        OperationContract contract,
        JsonTypeInfo<TRequest> requestInfo,
        JsonTypeInfo<TResponse> responseInfo,
        OperationSchema? schema = null)
        where TOperation : class, IOperation<TRequest, TResponse>
    {
        ArgumentNullException.ThrowIfNull(contract);
        Services.TryAddScoped<TOperation>();
        Operations.Add(new OperationDescriptor<TOperation, TRequest, TResponse>(contract, Manifest.Id, requestInfo, responseInfo, schema)).ThrowIfFailure();
        return this;
    }

    /// <summary>
    /// 取清单声明的绑定；缺失抛出 <see cref="KernelErrors.MissingBinding"/>。
    /// </summary>
    public BindingValue GetBinding(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Bindings.TryGetValue(name, out var binding)
            ? binding
            : throw new FrameworkException(KernelErrors.MissingBinding, $"Binding '{name}' required by feature '{Manifest.Id}' is not provided.");
    }

    /// <summary>
    /// 取可选绑定。
    /// </summary>
    public Maybe<BindingValue> TryGetBinding(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Bindings.TryGetValue(name, out var binding) ? Maybe.Some(binding) : default;
    }

    /// <summary>
    /// 登记本特性定义的权限；重复或超限抛出对应错误。
    /// </summary>
    public FeatureContext AddPermission(PermissionDefinition permission)
    {
        Permissions.Add(permission).ThrowIfFailure();
        return this;
    }
}
