// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 应用初始化前模块生命周期贡献者
/// </summary>
public class OnApplicationInitializationModuleLifecycleContributor : ModuleLifecycleContributorBase
{
    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    /// <returns></returns>
    public override async Task InitializeAsync(ApplicationInitializationContext context, IXiHanModule module)
    {
        if (module is IOnApplicationInitialization onApplicationInitialization)
        {
            await onApplicationInitialization.OnApplicationInitializationAsync(context);
        }
    }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    public override void Initialize(ApplicationInitializationContext context, IXiHanModule module)
    {
        (module as IOnApplicationInitialization)?.OnApplicationInitialization(context);
    }
}

/// <summary>
/// 应用程序关闭生命周期贡献者
/// </summary>
public class OnApplicationShutdownModuleLifecycleContributor : ModuleLifecycleContributorBase
{
    /// <summary>
    /// 关闭
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    /// <returns></returns>
    public override async Task ShutdownAsync(ApplicationShutdownContext context, IXiHanModule module)
    {
        if (module is IOnApplicationShutdown onApplicationShutdown)
        {
            await onApplicationShutdown.OnApplicationShutdownAsync(context);
        }
    }

    /// <summary>
    /// 关闭
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    public override void Shutdown(ApplicationShutdownContext context, IXiHanModule module)
    {
        (module as IOnApplicationShutdown)?.OnApplicationShutdown(context);
    }
}

/// <summary>
/// 在应用程序初始化后的模块生命周期贡献者
/// </summary>
public class OnPostApplicationInitializationModuleLifecycleContributor : ModuleLifecycleContributorBase
{
    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    /// <returns></returns>
    public override async Task InitializeAsync(ApplicationInitializationContext context, IXiHanModule module)
    {
        if (module is IOnPostApplicationInitialization onPostApplicationInitialization)
        {
            await onPostApplicationInitialization.OnPostApplicationInitializationAsync(context);
        }
    }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    public override void Initialize(ApplicationInitializationContext context, IXiHanModule module)
    {
        (module as IOnPostApplicationInitialization)?.OnPostApplicationInitialization(context);
    }
}

/// <summary>
/// 在应用程序初始化之前的模块生命周期贡献者
/// </summary>
public class OnPreApplicationInitializationModuleLifecycleContributor : ModuleLifecycleContributorBase
{
    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    /// <returns></returns>
    public override async Task InitializeAsync(ApplicationInitializationContext context, IXiHanModule module)
    {
        if (module is IOnPreApplicationInitialization onPreApplicationInitialization)
        {
            await onPreApplicationInitialization.OnPreApplicationInitializationAsync(context);
        }
    }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    public override void Initialize(ApplicationInitializationContext context, IXiHanModule module)
    {
        (module as IOnPreApplicationInitialization)?.OnPreApplicationInitialization(context);
    }
}
