// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel.Capabilities;
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
    internal FeatureContext(
        ResolvedNode node,
        IServiceCollection services,
        IConfiguration configuration,
        PipelineBuilder pipeline,
        IReadOnlyDictionary<string, BindingValue> bindings,
        PermissionRegistry permissions)
    {
        Node = node;
        Services = services;
        Configuration = configuration;
        Pipeline = pipeline;
        Bindings = bindings;
        Permissions = permissions;
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
    /// 取清单声明的绑定；缺失抛出 <see cref="KernelErrors.MissingBinding"/>。
    /// </summary>
    public BindingValue GetBinding(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Bindings.TryGetValue(name, out var binding)
            ? binding
            : throw new FrameworkException(KernelErrors.MissingBinding, $"特性 '{Manifest.Id}' 需要的绑定 '{name}' 未提供。");
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
