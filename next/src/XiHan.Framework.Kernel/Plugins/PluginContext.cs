// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Plugins;

/// <summary>
/// 插件的配置上下文。插件拥有隔离的服务集合；是否允许贡献中间件由隔离级别决定。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class PluginContext
{
    /// <summary>
    /// 创建插件上下文。
    /// </summary>
    public PluginContext(Manifest manifest, IServiceCollection services, IConfiguration configuration, PermissionGrant permissions, PipelineBuilder? pipeline)
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        Permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        Pipeline = pipeline;
    }

    /// <summary>
    /// 插件清单。
    /// </summary>
    public Manifest Manifest { get; }

    /// <summary>
    /// 插件自己的服务集合。
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// 宿主配置的只读视图。
    /// </summary>
    public IConfiguration Configuration { get; }

    /// <summary>
    /// 已授予的权限。
    /// </summary>
    public PermissionGrant Permissions { get; }

    /// <summary>
    /// 可贡献中间件的管道；进程外插件为 <see langword="null"/>。
    /// </summary>
    public PipelineBuilder? Pipeline { get; }
}
