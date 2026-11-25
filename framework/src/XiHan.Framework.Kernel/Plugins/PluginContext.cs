// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Plugins;

/// <summary>
/// 插件的配置上下文
/// </summary>
/// <remarks>
/// 插件拥有隔离的服务集合；是否允许贡献中间件由隔离级别决定
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class PluginContext
{
    /// <summary>
    /// 创建插件上下文
    /// </summary>
    /// <param name="manifest">插件清单</param>
    /// <param name="services">插件自己的服务集合</param>
    /// <param name="configuration">宿主配置的只读视图</param>
    /// <param name="permissions">已授予的权限</param>
    /// <param name="pipeline">可贡献中间件的管道；不开放时为 <see langword="null"/></param>
    /// <param name="serviceBindingRoot">宿主解析绑定所用的 Service Binding 根目录；<see langword="null"/> 时取环境变量 <c>SERVICE_BINDING_ROOT</c></param>
    public PluginContext(Manifest manifest, IServiceCollection services, IConfiguration configuration, PluginPermissions permissions, PipelineBuilder? pipeline, string? serviceBindingRoot = null)
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        Permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        Pipeline = pipeline;
        ServiceBindingRoot = serviceBindingRoot;
    }

    /// <summary>
    /// 插件清单
    /// </summary>
    public Manifest Manifest { get; }

    /// <summary>
    /// 插件自己的服务集合
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// 宿主配置的只读视图
    /// </summary>
    public IConfiguration Configuration { get; }

    /// <summary>
    /// 宿主解析绑定所用的 Service Binding 根目录
    /// </summary>
    /// <remarks>
    /// 应用指定的根目录（与 <see cref="Hosting.FeatureContext.ServiceBindingRoot"/> 同一个值）；没有指定时为 <see langword="null"/>，这时根目录取环境变量 <c>SERVICE_BINDING_ROOT</c>。插件自己解析绑定时把它与 <see cref="Configuration"/> 交给 <see cref="Hosting.BindingResolver.Resolve(IConfiguration, string?)"/>，读到的与宿主加载前核对的是同一套来源（<c>spec/plugin/lifecycle.md</c> §2，ADR-0301）
    /// </remarks>
    public string? ServiceBindingRoot { get; }

    /// <summary>
    /// 已授予的权限
    /// </summary>
    public PluginPermissions Permissions { get; }

    /// <summary>
    /// 可贡献中间件的管道
    /// </summary>
    /// <remarks>
    /// 当前的插件宿主都传 <see langword="null"/>：进程内插件拿不到宿主管道，不能贡献中间件
    /// </remarks>
    public PipelineBuilder? Pipeline { get; }
}
