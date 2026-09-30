// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.EventBus.Distributed;

/// <summary>
/// 发件箱投递目标轮转扫描器
/// </summary>
/// <remarks>
/// 宿主布局与目录中的全部投递目标组成一个循环，每轮从上一轮停下的位置继续，同一目标在一轮内至多访问一次。
/// 每个目标在独立的服务作用域内切换到其租户上下文后领取、投递并删除，访问结束即还原租户上下文。
/// 单轮领取总数不超过传入的预算；单轮耗时达到 <see cref="EventBoxProcessingOptions.OutboxRoundTimeLimitMilliseconds"/> 后不再访问新目标。
/// 目录读取失败时保留游标，按指数退避暂停读取目录，退避期间每轮只扫描宿主布局。
/// 游标只保存在进程内存中，进程重启后从目录首页开始。同一发件箱配置的各轮须顺序调用。
/// </remarks>
public class OutboxDeliveryTargetScanner
{
    private const long HostKey = 0;

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IOptions<EventBoxProcessingOptions> _processingOptions;
    private readonly ILogger<OutboxDeliveryTargetScanner> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, RotationState> _states = new(StringComparer.Ordinal);

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceScopeFactory">服务作用域工厂</param>
    /// <param name="processingOptions">事件盒后台处理配置</param>
    /// <param name="logger">日志器</param>
    /// <param name="timeProvider">时间提供器，为 null 时使用系统时间</param>
    public OutboxDeliveryTargetScanner(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<EventBoxProcessingOptions> processingOptions,
        ILogger<OutboxDeliveryTargetScanner> logger,
        TimeProvider? timeProvider = null)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _processingOptions = processingOptions;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// 对指定发件箱执行一轮扫描
    /// </summary>
    /// <param name="outboxConfig">发件箱配置</param>
    /// <param name="budget">本轮最多领取的事件总数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本轮领取的事件总数</returns>
    public virtual async Task<int> SendRoundAsync(
        OutboxConfig outboxConfig,
        int budget,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outboxConfig);

        if (budget <= 0)
        {
            return 0;
        }

        var options = _processingOptions.Value;
        var state = _states.GetOrAdd(outboxConfig.Name, _ => new RotationState());
        var deadline = _timeProvider.GetUtcNow() +
            TimeSpan.FromMilliseconds(Math.Max(1, options.OutboxRoundTimeLimitMilliseconds));
        var visited = new HashSet<long>();
        var remaining = budget;

        while (remaining > 0 && _timeProvider.GetUtcNow() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (found, target) = await NextTargetAsync(state, options, deadline, cancellationToken);
            if (!found)
            {
                break;
            }

            if (!visited.Add(target?.TenantId ?? HostKey))
            {
                PutBack(state, target);
                break;
            }

            remaining -= await DeliverAsync(outboxConfig, target, remaining, cancellationToken);
        }

        return budget - remaining;
    }

    /// <summary>
    /// 取循环中的下一个目标
    /// </summary>
    /// <param name="state">轮转状态</param>
    /// <param name="options">事件盒后台处理配置</param>
    /// <param name="deadline">本轮扫描的截止时刻</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否取到目标；取到时目标为 null 表示宿主布局</returns>
    private async Task<(bool Found, OutboxDeliveryTarget? Target)> NextTargetAsync(
        RotationState state,
        EventBoxProcessingOptions options,
        DateTimeOffset deadline,
        CancellationToken cancellationToken)
    {
        if (state.HostPending)
        {
            state.HostPending = false;
            return (true, null);
        }

        if (TryTakeBuffered(state, out var buffered))
        {
            return (true, buffered);
        }

        if (state.EndOfDirectory)
        {
            StartNewCycle(state);
            return (true, null);
        }

        while (true)
        {
            if (_timeProvider.GetUtcNow() < state.DirectoryRetryAt)
            {
                return (true, null);
            }

            if (_timeProvider.GetUtcNow() >= deadline)
            {
                return (false, null);
            }

            OutboxDeliveryTargetPage page;
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var provider = scope.ServiceProvider.GetService<IOutboxDeliveryTargetProvider>();

                page = provider is null
                    ? OutboxDeliveryTargetPage.Empty
                    : await provider.GetPageAsync(state.Cursor, Math.Max(1, options.OutboxTargetPageSize), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                state.DirectoryFailures++;
                var backoff = GetDirectoryBackoff(state.DirectoryFailures, options);
                state.DirectoryRetryAt = _timeProvider.GetUtcNow() + backoff;
                _logger.LogError(ex, "读取发件箱投递目标目录失败，游标保持不变，{Backoff} 后重试。", backoff);
                return (true, null);
            }

            state.DirectoryFailures = 0;
            state.DirectoryRetryAt = DateTimeOffset.MinValue;
            state.Cursor = page.NextCursor;
            state.EndOfDirectory = page.NextCursor is null;

            foreach (var target in page.Targets)
            {
                state.Buffer.AddLast(target);
            }

            if (TryTakeBuffered(state, out buffered))
            {
                return (true, buffered);
            }

            if (state.EndOfDirectory)
            {
                StartNewCycle(state);
                return (true, null);
            }
        }
    }

    /// <summary>
    /// 在目标的租户上下文中领取、投递并删除一批事件
    /// </summary>
    /// <param name="outboxConfig">发件箱配置</param>
    /// <param name="target">投递目标，为 null 表示宿主布局</param>
    /// <param name="quota">本次最多领取的条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次领取的条数；处理失败时为失败前已领取的条数</returns>
    private async Task<int> DeliverAsync(
        OutboxConfig outboxConfig,
        OutboxDeliveryTarget? target,
        int quota,
        CancellationToken cancellationToken)
    {
        var claimedCount = 0;

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var services = scope.ServiceProvider;
            var currentTenant = services.GetService<ICurrentTenant>();

            if (target is not null && currentTenant is null)
            {
                throw new InvalidOperationException("未注册 ICurrentTenant，无法切换到投递目标的租户上下文。");
            }

            using var tenantScope = currentTenant?.Change(target?.TenantId, target?.TenantName);

            if (services.GetService(outboxConfig.ImplementationType) is not IEventOutbox outbox ||
                services.GetService<IDistributedEventBus>() is not ISupportsEventBoxes eventBoxes)
            {
                return 0;
            }

            var waitingEvents = await outbox.GetWaitingEventsAsync(quota, cancellationToken: cancellationToken);
            claimedCount = waitingEvents.Count;

            if (claimedCount == 0)
            {
                return 0;
            }

            await eventBoxes.PublishManyFromOutboxAsync(waitingEvents, outboxConfig);
            await outbox.DeleteManyAsync(waitingEvents.Select(item => item.Id));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理发件箱投递目标 {TenantId} 失败，已跳过该目标。", target?.TenantId);
        }

        return claimedCount;
    }

    /// <summary>
    /// 从缓冲中取出下一个目标
    /// </summary>
    /// <param name="state">轮转状态</param>
    /// <param name="target">取出的目标</param>
    /// <returns>缓冲非空时为 true</returns>
    private static bool TryTakeBuffered(RotationState state, out OutboxDeliveryTarget? target)
    {
        if (state.Buffer.First is { } node)
        {
            state.Buffer.RemoveFirst();
            target = node.Value;
            return true;
        }

        target = null;
        return false;
    }

    /// <summary>
    /// 开始新一个循环：游标回到目录首页
    /// </summary>
    /// <param name="state">轮转状态</param>
    private static void StartNewCycle(RotationState state)
    {
        state.EndOfDirectory = false;
        state.Cursor = null;
    }

    /// <summary>
    /// 把本轮未访问的目标放回循环最前面
    /// </summary>
    /// <param name="state">轮转状态</param>
    /// <param name="target">目标，为 null 表示宿主布局</param>
    private static void PutBack(RotationState state, OutboxDeliveryTarget? target)
    {
        if (target is null)
        {
            state.HostPending = true;
        }
        else
        {
            state.Buffer.AddFirst(target);
        }
    }

    /// <summary>
    /// 计算目录读取失败后的退避时长
    /// </summary>
    /// <param name="failures">连续失败次数</param>
    /// <param name="options">事件盒后台处理配置</param>
    /// <returns>退避时长</returns>
    private static TimeSpan GetDirectoryBackoff(int failures, EventBoxProcessingOptions options)
    {
        var baseMilliseconds = Math.Max(200d, options.PollingIntervalMilliseconds);
        var maxMilliseconds = Math.Max(baseMilliseconds, options.OutboxTargetDirectoryMaxBackoffMilliseconds);
        var milliseconds = Math.Min(maxMilliseconds, baseMilliseconds * Math.Pow(2, Math.Min(failures - 1, 20)));

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    /// <summary>
    /// 单个发件箱配置的轮转状态
    /// </summary>
    private sealed class RotationState
    {
        /// <summary>
        /// 下一个访问的是否为宿主布局
        /// </summary>
        public bool HostPending { get; set; } = true;

        /// <summary>
        /// 下一页的目录游标，为 null 表示首页
        /// </summary>
        public string? Cursor { get; set; }

        /// <summary>
        /// 最近读取的一页是否为目录末页
        /// </summary>
        public bool EndOfDirectory { get; set; }

        /// <summary>
        /// 已读取但尚未访问的目标
        /// </summary>
        public LinkedList<OutboxDeliveryTarget> Buffer { get; } = new();

        /// <summary>
        /// 目录连续读取失败次数
        /// </summary>
        public int DirectoryFailures { get; set; }

        /// <summary>
        /// 目录下次允许读取的时刻
        /// </summary>
        public DateTimeOffset DirectoryRetryAt { get; set; } = DateTimeOffset.MinValue;
    }
}
