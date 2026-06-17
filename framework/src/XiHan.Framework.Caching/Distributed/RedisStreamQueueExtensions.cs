#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RedisStreamQueueExtensions
// Guid:7d8e9f0a-1b2c-4345-b6c7-8d9e0f1a2b3c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/06/16 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Caching.Distributed;

/// <summary>
/// <see cref="IRedisStreamQueue{T}"/> 便捷扩展。
/// </summary>
public static class RedisStreamQueueExtensions
{
    /// <summary>
    /// 批量入队。
    /// </summary>
    /// <typeparam name="T">消息类型</typeparam>
    /// <param name="queue">队列</param>
    /// <param name="items">消息集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="ArgumentNullException">队列或集合为空时抛出</exception>
    public static async Task EnqueueRangeAsync<T>(this IRedisStreamQueue<T> queue, IEnumerable<T> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items)
        {
            await queue.EnqueueAsync(item, cancellationToken);
        }
    }

    /// <summary>
    /// 确认单条消息。
    /// </summary>
    /// <typeparam name="T">消息类型</typeparam>
    /// <param name="queue">队列</param>
    /// <param name="messageId">消息 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static Task AckAsync<T>(this IRedisStreamQueue<T> queue, string messageId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);
        return queue.AckAsync([messageId], cancellationToken);
    }

    /// <summary>
    /// 读取一批新消息并逐条处理：成功自动 <c>ACK</c>，失败不确认（留待重投）。返回成功处理条数。
    /// </summary>
    /// <typeparam name="T">消息类型</typeparam>
    /// <param name="queue">队列</param>
    /// <param name="consumer">消费者名</param>
    /// <param name="count">最大读取条数</param>
    /// <param name="handler">处理委托（抛异常即视为失败，消息留在 PEL 由后续认领重投）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功处理（已确认）的条数</returns>
    public static async Task<int> ProcessBatchAsync<T>(
        this IRedisStreamQueue<T> queue,
        string consumer,
        int count,
        Func<T, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(handler);

        var messages = await queue.ReadAsync(consumer, count, cancellationToken);
        var processed = 0;
        foreach (var message in messages)
        {
            if (await TryHandleAsync(queue, message, handler, maxDeliveryCount: 0, onDeadLetter: null, cancellationToken))
            {
                processed++;
            }
        }

        return processed;
    }

    /// <summary>
    /// 一行式可靠消费循环：认领重投 → 读新消息 → 处理并自动 ACK → 空闲等唤醒；直到取消。
    /// </summary>
    /// <remarks>
    /// 适合在 BackgroundService 里直接 <c>await queue.ConsumeAsync("consumer", handler)</c>。
    /// 失败的消息不确认，由下一轮 <see cref="IRedisStreamQueue{T}.ClaimStaleAsync"/> 重投；
    /// 投递次数超过 <see cref="RedisStreamConsumeOptions.MaxDeliveryCount"/> 时调用 <paramref name="onDeadLetter"/>（如有）并确认丢弃。
    /// </remarks>
    /// <typeparam name="T">消息类型</typeparam>
    /// <param name="queue">队列</param>
    /// <param name="consumer">消费者名（同组唯一）</param>
    /// <param name="handler">消息处理委托</param>
    /// <param name="options">消费选项（批次/空闲等待/重投空闲阈值/最大投递次数）</param>
    /// <param name="onDeadLetter">超过最大投递次数时的死信回调（可空）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task ConsumeAsync<T>(
        this IRedisStreamQueue<T> queue,
        string consumer,
        Func<T, CancellationToken, Task> handler,
        RedisStreamConsumeOptions? options = null,
        Func<RedisStreamMessage<T>, CancellationToken, Task>? onDeadLetter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(handler);
        options ??= new RedisStreamConsumeOptions();

        while (!cancellationToken.IsCancellationRequested)
        {
            // 1) 认领崩溃残留的未确认消息重投
            var stale = await queue.ClaimStaleAsync(consumer, options.MinIdle, options.BatchSize, cancellationToken);
            foreach (var message in stale)
            {
                await TryHandleAsync(queue, message, handler, options.MaxDeliveryCount, onDeadLetter, cancellationToken);
            }

            // 2) 读取并处理新消息
            var messages = await queue.ReadAsync(consumer, options.BatchSize, cancellationToken);
            foreach (var message in messages)
            {
                await TryHandleAsync(queue, message, handler, options.MaxDeliveryCount, onDeadLetter, cancellationToken);
            }

            // 3) 都没活则阻塞等唤醒（被入队唤醒或兜底超时）
            if (stale.Count == 0 && messages.Count == 0)
            {
                await queue.WaitForSignalAsync(options.IdleWait, cancellationToken);
            }
        }
    }

    private static async Task<bool> TryHandleAsync<T>(
        IRedisStreamQueue<T> queue,
        RedisStreamMessage<T> message,
        Func<T, CancellationToken, Task> handler,
        int maxDeliveryCount,
        Func<RedisStreamMessage<T>, CancellationToken, Task>? onDeadLetter,
        CancellationToken cancellationToken)
    {
        // 坏消息（反序列化失败）直接确认丢弃，避免无限重投
        if (message.Value is null)
        {
            await queue.AckAsync(message.Id, cancellationToken);
            return false;
        }

        // 超过最大投递次数 → 死信回调 + 确认移除
        if (maxDeliveryCount > 0 && message.DeliveryCount > maxDeliveryCount)
        {
            if (onDeadLetter is not null)
            {
                await onDeadLetter(message, cancellationToken);
            }

            await queue.AckAsync(message.Id, cancellationToken);
            return false;
        }

        try
        {
            await handler(message.Value, cancellationToken);
            await queue.AckAsync(message.Id, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // 不确认：留在 PEL，由后续 ClaimStaleAsync 重投
            return false;
        }
    }
}

/// <summary>
/// <see cref="RedisStreamQueueExtensions.ConsumeAsync"/> 的消费选项。
/// </summary>
public sealed class RedisStreamConsumeOptions
{
    /// <summary>
    /// 单次读取/认领的批次大小。
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// 空闲时等待唤醒的最长时长（兜底补扫周期）。
    /// </summary>
    public TimeSpan IdleWait { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 认领重投的最小空闲阈值（消息未确认且空闲超过此值才会被重投，避免抢占正在处理的）。
    /// </summary>
    public TimeSpan MinIdle { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 最大投递次数；超过则转死信并确认丢弃。0 表示不限（永久重投）。
    /// </summary>
    public int MaxDeliveryCount { get; set; } = 5;
}
