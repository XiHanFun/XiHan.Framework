// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Plugins;

/// <summary>
/// 插件契约。声明插件的名称、版本和依赖。
/// </summary>
[ApiLevel(Stability.Preview, "1.0")]
public interface IPlugin
{
    /// <summary>
    /// 插件名称。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 插件版本。
    /// </summary>
    string Version { get; }

    /// <summary>
    /// 插件依赖的其他插件名称。
    /// </summary>
    IReadOnlyList<string> Dependencies { get; }
}

/// <summary>
/// 插件上下文，为插件提供隔离的执行环境。
/// </summary>
[ApiLevel(Stability.Preview, "1.0")]
public interface IPluginContext
{
    /// <summary>
    /// 插件名称。
    /// </summary>
    string PluginName { get; }

    /// <summary>
    /// 插件的隔离服务提供器。
    /// </summary>
    IServiceProvider Services { get; }
}

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
