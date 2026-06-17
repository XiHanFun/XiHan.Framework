#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RedisDelayQueueExtensions
// Guid:8e9f0a1b-2c3d-4456-b7c8-9e0f1a2b3c4d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/06/16 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Caching.Distributed;

/// <summary>
/// <see cref="IRedisDelayQueue{T}"/> 便捷扩展。
/// </summary>
public static class RedisDelayQueueExtensions
{
    /// <summary>
    /// 批量延迟入队（同一延迟）。
    /// </summary>
    /// <typeparam name="T">消息类型</typeparam>
    /// <param name="queue">队列</param>
    /// <param name="items">消息集合</param>
    /// <param name="delay">延迟时长</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="ArgumentNullException">队列或集合为空时抛出</exception>
    public static async Task EnqueueRangeAsync<T>(this IRedisDelayQueue<T> queue, IEnumerable<T> items, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items)
        {
            await queue.EnqueueAsync(item, delay, cancellationToken);
        }
    }

    /// <summary>
    /// 取出已到期的消息并逐条处理。返回处理条数。
    /// </summary>
    /// <typeparam name="T">消息类型</typeparam>
    /// <param name="queue">队列</param>
    /// <param name="count">最大取出条数</param>
    /// <param name="handler">处理委托</param>
    /// <param name="retryDelay">处理失败时重新延迟入队的时长（防丢）；为空则失败即丢弃（由调用方负责日志）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功处理条数</returns>
    public static async Task<int> ProcessDueAsync<T>(
        this IRedisDelayQueue<T> queue,
        int count,
        Func<T, CancellationToken, Task> handler,
        TimeSpan? retryDelay = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(handler);

        var due = await queue.DequeueDueAsync(count, cancellationToken);
        var processed = 0;
        foreach (var item in due)
        {
            if (item is null)
            {
                continue;
            }

            try
            {
                await handler(item, cancellationToken);
                processed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // 延迟队列取出即移除：失败按 retryDelay 重新入队避免丢（无则丢弃）
                if (retryDelay is { } delay)
                {
                    await queue.EnqueueAsync(item, delay, cancellationToken);
                }
            }
        }

        return processed;
    }

    /// <summary>
    /// 一行式到期消费循环：周期性取出已到期消息并处理，直到取消。
    /// </summary>
    /// <remarks>
    /// 适合在 BackgroundService 里直接 <c>await queue.ConsumeDueAsync(handler)</c>。
    /// 延迟精度 ≈ <see cref="RedisDelayConsumeOptions.PollInterval"/>。处理失败按 <see cref="RedisDelayConsumeOptions.RetryDelay"/> 重投。
    /// </remarks>
    /// <typeparam name="T">消息类型</typeparam>
    /// <param name="queue">队列</param>
    /// <param name="handler">消息处理委托</param>
    /// <param name="options">消费选项（批次/轮询周期/失败重投延迟）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task ConsumeDueAsync<T>(
        this IRedisDelayQueue<T> queue,
        Func<T, CancellationToken, Task> handler,
        RedisDelayConsumeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(handler);
        options ??= new RedisDelayConsumeOptions();

        while (!cancellationToken.IsCancellationRequested)
        {
            var processed = await queue.ProcessDueAsync(options.BatchSize, handler, options.RetryDelay, cancellationToken);

            // 本轮取空 → 等一个轮询周期再查（延迟项不会主动唤醒，需周期检查）
            if (processed == 0)
            {
                await Task.Delay(options.PollInterval, cancellationToken);
            }
        }
    }
}

/// <summary>
/// <see cref="RedisDelayQueueExtensions.ConsumeDueAsync"/> 的消费选项。
/// </summary>
public sealed class RedisDelayConsumeOptions
{
    /// <summary>
    /// 单次取出的批次大小。
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// 轮询周期（决定延迟精度：到期后最多此时长内被取走）。
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 处理失败时重新延迟入队的时长（防丢）；为空则失败即丢弃。
    /// </summary>
    public TimeSpan? RetryDelay { get; set; }
}
