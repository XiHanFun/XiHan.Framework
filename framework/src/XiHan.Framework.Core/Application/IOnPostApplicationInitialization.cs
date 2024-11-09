#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IOnPostApplicationInitialization
// Guid:6bed82f8-eb81-476a-a71b-dceea178d6bc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:47:44
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 程序初始化后接口
/// </summary>
public interface IOnPostApplicationInitialization
{
    /// <summary>
    /// 程序初始化后，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task OnPostApplicationInitializationAsync(ApplicationInitializationContext context);

    /// <summary>
    /// 程序初始化后
    /// </summary>
    /// <param name="context"></param>
    void OnPostApplicationInitialization(ApplicationInitializationContext context);
}