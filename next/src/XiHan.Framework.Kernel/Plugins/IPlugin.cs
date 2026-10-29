// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Plugins;

/// <summary>
/// 插件契约。
/// </summary>
/// <remarks>
/// 运行期加载、隔离；失败不影响宿主。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IPlugin
{
    /// <summary>
    /// 插件清单。
    /// </summary>
    /// <remarks>
    /// <see cref="Manifest.Kind"/> 必须是 <see cref="ManifestKind.Plugin"/>。
    /// </remarks>
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
