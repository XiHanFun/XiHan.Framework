#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IModuleManager
// Guid:f89a3aa9-b851-4a3b-b715-595a9b72ad06
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:38:44
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
