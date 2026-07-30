// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
