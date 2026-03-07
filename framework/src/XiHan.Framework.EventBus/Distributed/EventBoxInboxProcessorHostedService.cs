#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EventBoxInboxProcessorHostedService
// Guid:e31bc09c-4c31-4454-9678-f8fcbf1a63ab
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 20:54:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Distributed;

/// <summary>
/// 事件收件箱处理后台服务
/// </summary>
public class EventBoxInboxProcessorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IOptions<XiHanDistributedEventBusOptions> _distributedOptions;
    private readonly IOptions<EventBoxProcessingOptions> _processingOptions;
    private readonly ILogger<EventBoxInboxProcessorHostedService> _logger;
    private readonly ConcurrentDictionary<Guid, int> _retryCounter = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceScopeFactory"></param>
    /// <param name="distributedOptions"></param>
    /// <param name="processingOptions"></param>
    /// <param name="logger"></param>
    public EventBoxInboxProcessorHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<XiHanDistributedEventBusOptions> distributedOptions,
        IOptions<EventBoxProcessingOptions> processingOptions,
        ILogger<EventBoxInboxProcessorHostedService> logger)
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
                await ProcessWaitingEventsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理事件收件箱时发生异常。");
            }

            await DelayAsync(stoppingToken);
        }
    }

    private async Task ProcessWaitingEventsAsync(CancellationToken cancellationToken)
    {
        var inboxConfigs = _distributedOptions.Value.Inboxes.Values
            .Where(config => config.IsProcessingEnabled)
            .ToArray();

        if (inboxConfigs.Length == 0)
        {
            return;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        if (scope.ServiceProvider.GetService<IDistributedEventBus>() is not ISupportsEventBoxes eventBoxes)
        {
            return;
        }

        var batchSize = Math.Max(1, _processingOptions.Value.InboxBatchSize);
        foreach (var inboxConfig in inboxConfigs)
        {
            var inbox = scope.ServiceProvider.GetService(inboxConfig.ImplementationType) as IEventInbox;
            if (inbox is null)
            {
                continue;
            }

            var waitingEvents = await inbox.GetWaitingEventsAsync(batchSize, cancellationToken: cancellationToken);
            if (waitingEvents.Count == 0)
            {
                await inbox.DeleteOldEventsAsync();
                continue;
            }

            foreach (var incomingEvent in waitingEvents)
            {
                try
                {
                    await eventBoxes.ProcessFromInboxAsync(incomingEvent, inboxConfig);
                    await inbox.MarkAsProcessedAsync(incomingEvent.Id);
                    _retryCounter.TryRemove(incomingEvent.Id, out _);
                }
                catch (Exception ex)
                {
                    await HandleProcessFailureAsync(inbox, incomingEvent.Id, ex);
                }
            }

            await inbox.DeleteOldEventsAsync();
        }
    }

    private async Task HandleProcessFailureAsync(IEventInbox inbox, Guid eventId, Exception exception)
    {
        var retryCount = _retryCounter.AddOrUpdate(eventId, 1, (_, current) => current + 1);
        var maxRetryCount = Math.Max(1, _processingOptions.Value.MaxInboxRetryCount);

        if (retryCount >= maxRetryCount)
        {
            await inbox.MarkAsDiscardAsync(eventId);
            _retryCounter.TryRemove(eventId, out _);
            _logger.LogWarning(
                exception,
                "收件箱事件处理失败并已丢弃。EventId: {EventId}, RetryCount: {RetryCount}",
                eventId,
                retryCount);
            return;
        }

        var retryDelaySeconds = Math.Max(1, _processingOptions.Value.InboxRetryDelaySeconds);
        var nextRetryTime = DateTime.UtcNow.AddSeconds(retryDelaySeconds);
        await inbox.RetryLaterAsync(eventId, retryCount, nextRetryTime);
        _logger.LogWarning(
            exception,
            "收件箱事件处理失败，将重试。EventId: {EventId}, RetryCount: {RetryCount}",
            eventId,
            retryCount);
    }

    private async Task DelayAsync(CancellationToken cancellationToken)
    {
        var delayMs = Math.Max(200, _processingOptions.Value.PollingIntervalMilliseconds);
        await Task.Delay(delayMs, cancellationToken);
    }
}
