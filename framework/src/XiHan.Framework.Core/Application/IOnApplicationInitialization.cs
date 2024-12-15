#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IOnApplicationInitialization
// Guid:61d5c274-bf53-4a85-98ed-55fe5f34730e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:46:57
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 程序初始化接口
/// </summary>
public interface IOnApplicationInitialization
{
    /// <summary>
    /// 程序初始化，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task OnApplicationInitializationAsync(ApplicationInitializationContext context);

    /// <summary>
    /// 程序初始化
    /// </summary>
    /// <param name="context"></param>
    void OnApplicationInitialization(ApplicationInitializationContext context);
}
