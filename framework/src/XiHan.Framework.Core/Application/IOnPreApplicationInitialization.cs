#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IOnPreApplicationInitialization
// Guid:04e332fc-09cc-4788-b227-2e4821ef8f18
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:46:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 程序初始化前接口
/// </summary>
public interface IOnPreApplicationInitialization
{
    /// <summary>
    /// 程序初始化前，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task OnPreApplicationInitializationAsync(ApplicationInitializationContext context);

    /// <summary>
    /// 程序初始化前
    /// </summary>
    /// <param name="context"></param>
    void OnPreApplicationInitialization(ApplicationInitializationContext context);
}