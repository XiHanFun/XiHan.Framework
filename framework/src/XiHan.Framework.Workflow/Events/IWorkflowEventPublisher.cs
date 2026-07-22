// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.Events;

/// <summary>
/// 工作流事件发布器（生命周期事件的发布出口）
/// </summary>
/// <remarks>
/// 事件发布是观测性能力，发布失败仅记录日志、不阻断引擎执行。
/// </remarks>
public interface IWorkflowEventPublisher
{
    /// <summary>
    /// 发布事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="eventData">事件数据</param>
    /// <returns>任务</returns>
    Task PublishAsync<TEvent>(TEvent eventData) where TEvent : class;
}
