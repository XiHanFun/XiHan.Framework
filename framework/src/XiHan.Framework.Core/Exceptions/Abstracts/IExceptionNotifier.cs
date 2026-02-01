#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IExceptionNotifier
// Guid:88ba0f7b-0ee4-44be-8f03-52bf1e40c51a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/04/23 01:02:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Exceptions.Handling;

namespace XiHan.Framework.Core.Exceptions.Abstracts;

/// <summary>
/// 异常通知器
/// </summary>
public interface IExceptionNotifier
{
    /// <summary>
    /// 通知
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task NotifyAsync(ExceptionNotificationContext context);
}
