// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Plugins;

/// <summary>
/// 插件宿主。管理插件的加载、卸载和生命周期。
/// </summary>
[ApiLevel(Stability.Preview, "1.0")]
public interface IPluginHost
{
    /// <summary>
    /// 加载一个静态插件（编译时已知，AOT 兼容）。
    /// </summary>
    Task LoadAsync<TPlugin>(CancellationToken cancellationToken = default) where TPlugin : IPlugin;

    /// <summary>
    /// 卸载一个已加载的插件。
    /// </summary>
    Task UnloadAsync(string pluginName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取已加载的插件列表。
    /// </summary>
    IReadOnlyList<IPlugin> GetLoadedPlugins();
}
