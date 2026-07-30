// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 模块生命周期贡献者基类
/// </summary>
public abstract class ModuleLifecycleContributorBase : IModuleLifecycleContributor
{
    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    /// <returns></returns>
    public virtual Task InitializeAsync(ApplicationInitializationContext context, IXiHanModule module)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    public virtual void Initialize(ApplicationInitializationContext context, IXiHanModule module)
    {
    }

    /// <summary>
    /// 关闭
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    /// <returns></returns>
    public virtual Task ShutdownAsync(ApplicationShutdownContext context, IXiHanModule module)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 关闭
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    public virtual void Shutdown(ApplicationShutdownContext context, IXiHanModule module)
    {
    }
}
