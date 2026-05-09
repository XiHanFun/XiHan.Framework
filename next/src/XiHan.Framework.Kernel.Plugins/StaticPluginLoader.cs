// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Kernel.Plugins;

/// <summary>
/// 静态插件加载器。编译时注册，要求插件有无参构造函数。
/// 完整 AOT 兼容版本需要源生成器生成强类型工厂以消除 Activator.CreateInstance。
/// </summary>
[ApiLevel(Stability.Preview, "1.0")]
public sealed class StaticPluginLoader
{
    private readonly Dictionary<string, Type> _pluginTypes = [];

    /// <summary>
    /// 注册一个静态插件类型。插件必须有无参构造函数。
    /// </summary>
    public StaticPluginLoader Register<TPlugin>() where TPlugin : IPlugin, new()
    {
        _pluginTypes[typeof(TPlugin).Name] = typeof(TPlugin);
        return this;
    }

    /// <summary>
    /// 将所有已注册的静态插件加载到服务集合中。
    /// </summary>
    public void LoadAll(IServiceCollection services)
    {
        foreach (var type in _pluginTypes.Values)
        {
            var plugin = (IPlugin)Activator.CreateInstance(type)!;
            services.AddSingleton<IPlugin>(plugin);
        }
    }
}
