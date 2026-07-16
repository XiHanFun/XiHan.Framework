#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IWorkflowEventPublisher
// Guid:d5720e94-3cb1-4f68-a0d2-87e19c53b4f0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:51:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
