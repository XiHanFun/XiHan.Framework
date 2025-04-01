#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EventHandlerExtensions
// Guid:1541cea1-9140-48ea-bab1-f7bedb079767
// Author:afand
// Email:me@zhaifanhua.com
// CreateTime:2025/4/1 21:04:08
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.System;

/// <summary>
/// 事件处理程序扩展
/// </summary>
public static class EventHandlerExtensions
{
    /// <summary>
    /// 使用给定的参数安全地引发给定的事件。
    /// </summary>
    /// <param name="eventHandler">事件处理程序</param>
    /// <param name="sender">事件来源</param>
    public static void InvokeSafely(this EventHandler eventHandler, object sender)
    {
        eventHandler.InvokeSafely(sender, EventArgs.Empty);
    }

    /// <summary>
    /// 使用给定的参数安全地引发给定的事件。
    /// </summary>
    /// <param name="eventHandler">事件处理程序</param>
    /// <param name="sender">事件来源</param>
    /// <param name="e">事件参数</param>
    public static void InvokeSafely(this EventHandler eventHandler, object sender, EventArgs e)
    {
        eventHandler?.Invoke(sender, e);
    }

    /// <summary>
    /// 使用给定的参数安全地引发给定的事件。
    /// </summary>
    /// <typeparam name="TEventArgs"><see cref="EventArgs"/></typeparam> 的类型
    /// <param name="eventHandler">事件处理程序</param>
    /// <param name="sender">事件来源</param>
    /// <param name="e">事件参数</param>
    public static void InvokeSafely<TEventArgs>(this EventHandler<TEventArgs> eventHandler, object sender, TEventArgs e)
        where TEventArgs : EventArgs
    {
        eventHandler?.Invoke(sender, e);
    }
}
