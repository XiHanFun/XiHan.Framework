#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IExceptionSubscriber
// Guid:7413c0fb-b0ae-40ed-b39f-a9ae194ba891
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/23 1:11:16
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;

namespace XiHan.Framework.Core.Exceptions.Handling.Abstracts;

/// <summary>
/// 异常订阅者接口
/// </summary>
public interface IExceptionSubscriber
{
    /// <summary>
    /// 处理异常，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task HandleAsync([NotNull] ExceptionNotificationContext context);
}