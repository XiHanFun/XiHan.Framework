// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Plugins;

/// <summary>
/// 宿主授予插件的权限集合。插件清单声明所需权限，宿主配置决定授予哪些，运行期由宿主强制。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class PermissionGrant
{
    private readonly HashSet<string> _granted;

    /// <summary>
    /// 创建授权集合。
    /// </summary>
    public PermissionGrant(IEnumerable<string> granted)
    {
        ArgumentNullException.ThrowIfNull(granted);
        _granted = new HashSet<string>(granted, StringComparer.Ordinal);
    }

    /// <summary>
    /// 空授权。
    /// </summary>
    public static PermissionGrant Empty { get; } = new([]);

    /// <summary>
    /// 已授予的权限。
    /// </summary>
    public IReadOnlySet<string> Granted => _granted;

    /// <summary>
    /// 是否已授予指定权限。
    /// </summary>
    public bool IsGranted(string permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        return _granted.Contains(permission);
    }

    /// <summary>
    /// 清单声明但未授予的权限。
    /// </summary>
    public IReadOnlyList<string> Missing(Manifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return manifest.Permissions.Where(p => !_granted.Contains(p)).ToList();
    }
}

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

/// <summary>
/// 插件契约。运行期加载、隔离；失败不影响宿主。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IPlugin
{
    /// <summary>
    /// 插件清单；<see cref="Manifest.Kind"/> 必须是 <see cref="ManifestKind.Plugin"/>。
    /// </summary>
    Manifest Manifest { get; }

    /// <summary>
    /// 配置插件自己的服务与贡献点。
    /// </summary>
    void Configure(PluginContext context);

    /// <summary>
    /// 启动。
    /// </summary>
    Task StartAsync(IServiceProvider services, CancellationToken cancellationToken);

    /// <summary>
    /// 停止。
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}

/// <summary>
/// 插件来源：清单与程序集所在目录，以及宿主授予的权限。
/// </summary>
/// <param name="Directory">包含 <c>xihan.manifest.json</c> 的目录。</param>
/// <param name="Permissions">授予的权限。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record PluginSource(string Directory, PermissionGrant Permissions)
{
    /// <summary>
    /// 目录。
    /// </summary>
    public string Directory { get; } = string.IsNullOrWhiteSpace(Directory) ? throw new ArgumentException("插件目录不能为空。", nameof(Directory)) : Directory;

    /// <summary>
    /// 授予的权限。
    /// </summary>
    public PermissionGrant Permissions { get; } = Permissions ?? throw new ArgumentNullException(nameof(Permissions));
}

/// <summary>
/// 插件状态快照。
/// </summary>
/// <param name="Manifest">清单。</param>
/// <param name="State">生命周期状态。</param>
/// <param name="Error">失败原因。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record PluginStatus(Manifest Manifest, LifecycleState State, Error? Error = null);

/// <summary>
/// 插件宿主：加载、启动、停止、卸载插件并上报状态。具体隔离机制由 <c>Plugins.*</c> 包实现。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IPluginHost
{
    /// <summary>
    /// 当前已加载的插件。
    /// </summary>
    IReadOnlyList<PluginStatus> Plugins { get; }

    /// <summary>
    /// 加载并启动插件。失败以 <see cref="Result{T}"/> 返回，不抛出。
    /// </summary>
    Task<Result<PluginStatus>> LoadAsync(PluginSource source, CancellationToken cancellationToken);

    /// <summary>
    /// 停止并卸载插件。
    /// </summary>
    Task<Result> UnloadAsync(string manifestId, CancellationToken cancellationToken);
}
