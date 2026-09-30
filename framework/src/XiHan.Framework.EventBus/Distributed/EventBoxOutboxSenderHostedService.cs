// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Distributed;

/// <summary>
/// 事件发件箱发送后台服务
/// </summary>
/// <remarks>
/// 注册了 <see cref="IOutboxDeliveryTargetProvider"/> 且发件箱实现 <see cref="ITenantScopedEventOutbox"/> 时，由 <see cref="OutboxDeliveryTargetScanner"/> 按投递目标轮转扫描；否则只在当前（无租户）上下文领取。
/// </remarks>
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

            try
            {
                await DelayAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
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
        var scanner = scope.ServiceProvider.GetService<OutboxDeliveryTargetScanner>();
        var hasTargetProvider = scope.ServiceProvider.GetService<IServiceProviderIsService>()?.IsService(typeof(IOutboxDeliveryTargetProvider)) == true;

        foreach (var outboxConfig in outboxConfigs)
        {
            var outbox = scope.ServiceProvider.GetService(outboxConfig.ImplementationType) as IEventOutbox;
            if (outbox is null)
            {
                continue;
            }

            // 注册了投递目标目录且发件箱按租户定位存储时，按目标轮转扫描
            if (outbox is ITenantScopedEventOutbox && hasTargetProvider && scanner is not null)
            {
                await scanner.SendRoundAsync(outboxConfig, batchSize, cancellationToken);
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
