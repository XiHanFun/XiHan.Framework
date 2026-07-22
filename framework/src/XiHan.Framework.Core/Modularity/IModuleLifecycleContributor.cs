// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 模块生命周期贡献者接口
/// </summary>
public interface IModuleLifecycleContributor : ITransientDependency
{
    /// <summary>
    /// 初始化，异步
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    /// <returns></returns>
    Task InitializeAsync(ApplicationInitializationContext context, IXiHanModule module);

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    void Initialize(ApplicationInitializationContext context, IXiHanModule module);

    /// <summary>
    /// 关闭，异步
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    /// <returns></returns>
    Task ShutdownAsync(ApplicationShutdownContext context, IXiHanModule module);

    /// <summary>
    /// 关闭
    /// </summary>
    /// <param name="context"></param>
    /// <param name="module"></param>
    void Shutdown(ApplicationShutdownContext context, IXiHanModule module);
}
