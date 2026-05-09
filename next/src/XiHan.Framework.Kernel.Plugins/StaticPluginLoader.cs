// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Kernel.Plugins;

/// <summary>
/// 静态插件加载器。
/// 编译时通过源生成器注册，完全 AOT 兼容。
/// 不需要 AssemblyLoadContext 或运行时反射。
/// </summary>
[ApiLevel(Stability.Preview, "1.0")]
public sealed class StaticPluginLoader
{
    private readonly Dictionary<string, Type> _pluginTypes = [];

    /// <summary>
    /// 注册一个静态插件类型。
    /// </summary>
    public StaticPluginLoader Register<TPlugin>() where TPlugin : IPlugin
    {
        _pluginTypes[typeof(TPlugin).Name] = typeof(TPlugin);
        return this;
    }

    /// <summary>
    /// 将所有已注册的静态插件加载到服务集合中。
    /// </summary>
    public void LoadAll(IServiceCollection services)
    {
        foreach (var (_, type) in _pluginTypes)
        {
            var plugin = (IPlugin)Activator.CreateInstance(type)!;
            services.AddSingleton<IPlugin>(plugin);
        }
    }
}
