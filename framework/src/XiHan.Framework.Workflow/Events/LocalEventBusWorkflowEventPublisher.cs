#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LocalEventBusWorkflowEventPublisher
// Guid:f18c62a5-940e-4d73-b5a6-31c08de97f42
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 10:52:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.EventBus.Abstractions.Local;

namespace XiHan.Framework.Workflow.Events;

/// <summary>
/// 基于本地事件总线的工作流事件发布器
/// </summary>
/// <remarks>
/// 事件立即发布（不等待工作单元完成）；容器内未注册本地事件总线时静默降级为不发布，
/// 发布异常仅记录日志，二者均不影响引擎执行。
/// </remarks>
public class LocalEventBusWorkflowEventPublisher : IWorkflowEventPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LocalEventBusWorkflowEventPublisher> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="logger">日志记录器</param>
    public LocalEventBusWorkflowEventPublisher(
        IServiceProvider serviceProvider,
        ILogger<LocalEventBusWorkflowEventPublisher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent eventData) where TEvent : class
    {
        var eventBus = _serviceProvider.GetService<ILocalEventBus>();
        if (eventBus is null)
        {
            _logger.LogDebug("本地事件总线未注册，跳过工作流事件 {EventType} 的发布", typeof(TEvent).Name);
            return;
        }

        try
        {
            await eventBus.PublishAsync(eventData, onUnitOfWorkComplete: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "工作流事件 {EventType} 发布失败", typeof(TEvent).Name);
        }
    }
}
