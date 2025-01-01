#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IOnApplicationShutdown
// Guid:a28d14a2-1a5b-44b3-977a-a0496ca957ff
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 19:48:04
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Application;

/// <summary>
/// 程序关闭时接口
/// </summary>
public interface IOnApplicationShutdown
{
    /// <summary>
    /// 程序关闭时，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task OnApplicationShutdownAsync(ApplicationShutdownContext context);

    /// <summary>
    /// 程序关闭时
    /// </summary>
    /// <param name="context"></param>
    void OnApplicationShutdown(ApplicationShutdownContext context);
}
