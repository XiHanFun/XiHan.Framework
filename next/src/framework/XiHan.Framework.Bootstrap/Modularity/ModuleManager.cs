using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Bootstrap.Abstractions.Application;
using XiHan.Framework.Bootstrap.Abstractions.Modularity;

namespace XiHan.Framework.Bootstrap.Modularity;

/// <summary>
/// 提供默认模块管理器实现。
/// </summary>
public sealed class ModuleManager : IModuleManager
{
    private readonly IReadOnlyList<IModuleDescriptor> _modules;

    /// <summary>
    /// 使用指定模块集合初始化管理器。
    /// </summary>
    /// <param name="modules">模块集合。</param>
    public ModuleManager(IEnumerable<IModuleDescriptor> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        _modules = modules.ToArray();
    }

    /// <inheritdoc />
    public async Task InitializeModulesAsync(ApplicationInitializationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var module in _modules)
        {
            if (module.Instance is IOnPreApplicationInitialization onPre)
            {
                await onPre.OnPreApplicationInitializationAsync(context);
            }
        }

        foreach (var module in _modules)
        {
            if (module.Instance is IOnApplicationInitialization onInit)
            {
                await onInit.OnApplicationInitializationAsync(context);
            }
        }

        foreach (var module in _modules)
        {
            if (module.Instance is IOnPostApplicationInitialization onPost)
            {
                await onPost.OnPostApplicationInitializationAsync(context);
            }
        }
    }

    /// <inheritdoc />
    public void InitializeModules(ApplicationInitializationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var module in _modules)
        {
            if (module.Instance is IOnPreApplicationInitialization onPre)
            {
                onPre.OnPreApplicationInitialization(context);
            }
        }

        foreach (var module in _modules)
        {
            if (module.Instance is IOnApplicationInitialization onInit)
            {
                onInit.OnApplicationInitialization(context);
            }
        }

        foreach (var module in _modules)
        {
            if (module.Instance is IOnPostApplicationInitialization onPost)
            {
                onPost.OnPostApplicationInitialization(context);
            }
        }
    }

    /// <inheritdoc />
    public async Task ShutdownModulesAsync(ApplicationShutdownContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var module in _modules.Reverse())
        {
            if (module.Instance is IOnApplicationShutdown onShutdown)
            {
                await onShutdown.OnApplicationShutdownAsync(context);
            }
        }
    }

    /// <inheritdoc />
    public void ShutdownModules(ApplicationShutdownContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var module in _modules.Reverse())
        {
            if (module.Instance is IOnApplicationShutdown onShutdown)
            {
                onShutdown.OnApplicationShutdown(context);
            }
        }
    }
}
