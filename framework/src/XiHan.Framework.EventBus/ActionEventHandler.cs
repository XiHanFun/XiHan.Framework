#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ActionEventHandler
// Guid:817f4f44-fa1f-4586-93b0-3615e3cfb80a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 4:54:23
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.EventBus.Abstractions.Local;

namespace XiHan.Framework.EventBus;

/// <summary>
/// 基于委托的事件处理器
/// 使用 Func&lt;TEvent, Task&gt; 委托来处理事件，提供轻量级的事件处理方式
/// 实现瞬时依赖注入生命周期
/// </summary>
/// <typeparam name="TEvent">事件类型，必须是引用类型</typeparam>
public class ActionEventHandler<TEvent> : ILocalEventHandler<TEvent>, ITransientDependency
{
    /// <summary>
    /// 初始化 ActionEventHandler&lt;TEvent&gt; 类的新实例
    /// </summary>
    /// <param name="handler">用于处理事件的委托方法</param>
    /// <exception cref="ArgumentNullException">当 handler 为 null 时</exception>
    public ActionEventHandler(Func<TEvent, Task> handler)
    {
        Action = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <summary>
    /// 获取事件处理委托
    /// 存储用于处理事件的异步方法
    /// </summary>
    /// <value>处理事件的异步委托方法</value>
    public Func<TEvent, Task> Action { get; }

    /// <summary>
    /// 异步处理事件
    /// 调用存储的委托方法来处理传入的事件数据
    /// </summary>
    /// <param name="eventData">事件数据对象</param>
    /// <returns>表示异步操作的任务</returns>
    /// <exception cref="ArgumentNullException">当 eventData 为 null 时</exception>
    public async Task HandleEventAsync(TEvent eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        await Action(eventData);
    }
}
