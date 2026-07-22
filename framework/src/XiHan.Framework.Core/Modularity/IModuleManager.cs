// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 模块管理器接口
/// </summary>
public interface IModuleManager
{
    /// <summary>
    /// 初始化模块，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task InitializeModulesAsync(ApplicationInitializationContext context);

    /// <summary>
    /// 初始化模块
    /// </summary>
    /// <param name="context"></param>
    void InitializeModules(ApplicationInitializationContext context);

    /// <summary>
    /// 关闭模块，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task ShutdownModulesAsync(ApplicationShutdownContext context);

    /// <summary>
    /// 关闭模块
    /// </summary>
    /// <param name="context"></param>
    void ShutdownModules(ApplicationShutdownContext context);
}
