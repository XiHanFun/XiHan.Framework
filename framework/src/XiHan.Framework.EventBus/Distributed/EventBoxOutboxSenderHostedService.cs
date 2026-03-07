#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EventBoxOutboxSenderHostedService
// Guid:85174f03-f1aa-4576-bd9e-a9e00f978516
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 20:51:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Distributed;

/// <summary>
/// 事件发件箱发送后台服务
/// </summary>
public class EventBoxOutboxSenderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IOptions<XiHanDistributedEventBusOptions> _distributedOptions;
    private readonly IOptions<EventBoxProcessingOptions> _processingOptions;
    private readonly ILogger<EventBoxOutboxSenderHostedService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceScopeFactory"></param>
    /// <param name="distributedOptions"></param>
    /// <param name="processingOptions"></param>
    /// <param name="logger"></param>
    public EventBoxOutboxSenderHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<XiHanDistributedEventBusOptions> distributedOptions,
        IOptions<EventBoxProcessingOptions> processingOptions,
        ILogger<EventBoxOutboxSenderHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _distributedOptions = distributedOptions;
        _processingOptions = processingOptions;
        _logger = logger;
    }

    /// <summary>
    /// 执行后台循环
    /// </summary>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendWaitingEventsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理事件发件箱时发生异常。");
            }

            await DelayAsync(stoppingToken);
        }
    }

    private async Task SendWaitingEventsAsync(CancellationToken cancellationToken)
    {
        var outboxConfigs = _distributedOptions.Value.Outboxes.Values
            .Where(config => config.IsSendingEnabled)
            .ToArray();

        if (outboxConfigs.Length == 0)
        {
            return;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        if (scope.ServiceProvider.GetService<IDistributedEventBus>() is not ISupportsEventBoxes eventBoxes)
        {
            return;
        }

        var batchSize = Math.Max(1, _processingOptions.Value.OutboxBatchSize);
        foreach (var outboxConfig in outboxConfigs)
        {
            var outbox = scope.ServiceProvider.GetService(outboxConfig.ImplementationType) as IEventOutbox;
            if (outbox is null)
            {
                continue;
            }

            var waitingEvents = await outbox.GetWaitingEventsAsync(batchSize, cancellationToken: cancellationToken);
            if (waitingEvents.Count == 0)
            {
                continue;
            }

            await eventBoxes.PublishManyFromOutboxAsync(waitingEvents, outboxConfig);
            await outbox.DeleteManyAsync(waitingEvents.Select(item => item.Id));
        }
    }

    private async Task DelayAsync(CancellationToken cancellationToken)
    {
        var delayMs = Math.Max(200, _processingOptions.Value.PollingIntervalMilliseconds);
        await Task.Delay(delayMs, cancellationToken);
    }
}
