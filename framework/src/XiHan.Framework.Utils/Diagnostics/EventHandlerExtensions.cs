// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Diagnostics;

/// <summary>
/// 事件处理程序扩展
/// </summary>
public static class EventHandlerExtensions
{
    /// <summary>
    /// 使用给定的参数安全地引发给定的事件
    /// </summary>
    /// <param name="eventHandler">事件处理程序</param>
    /// <param name="sender">事件来源</param>
    public static void InvokeSafely(this EventHandler eventHandler, object sender)
    {
        eventHandler.InvokeSafely(sender, EventArgs.Empty);
    }

    /// <summary>
    /// 使用给定的参数安全地引发给定的事件
    /// </summary>
    /// <param name="eventHandler">事件处理程序</param>
    /// <param name="sender">事件来源</param>
    /// <param name="e">事件参数</param>
    public static void InvokeSafely(this EventHandler? eventHandler, object sender, EventArgs e)
    {
        eventHandler?.Invoke(sender, e);
    }

    /// <summary>
    /// 使用给定的参数安全地引发给定的事件
    /// </summary>
    /// <typeparam name="TEventArgs"><see cref="EventArgs"/></typeparam> 的类型
    /// <param name="eventHandler">事件处理程序</param>
    /// <param name="sender">事件来源</param>
    /// <param name="e">事件参数</param>
    public static void InvokeSafely<TEventArgs>(this EventHandler<TEventArgs>? eventHandler, object sender, TEventArgs e)
        where TEventArgs : EventArgs
    {
        eventHandler?.Invoke(sender, e);
    }
}
