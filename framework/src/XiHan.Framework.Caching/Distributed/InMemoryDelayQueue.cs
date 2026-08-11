// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Caching.Distributed;

/// <summary>
/// 进程内延迟队列回退实现（Redis 未启用时使用）。
/// </summary>
/// <remarks>
/// 消息仅驻留当前进程内存，<b>不跨实例、进程退出即丢失</b>；多实例部署或需要消息持久化时启用 Redis 改用 <see cref="RedisDelayQueue{T}"/>。
/// 优先级为 <c>(到期时间戳ms, 入队序号)</c>，同一到期时刻按入队先后取出。注册为单例（每个封闭类型一个实例）。
/// </remarks>
/// <typeparam name="T">消息类型</typeparam>
public sealed class InMemoryDelayQueue<T> : IRedisDelayQueue<T>
{
    private readonly Lock _gate = new();
    private readonly PriorityQueue<T, (long DueAtMs, long Sequence)> _queue = new();

    private long _sequence;

    /// <summary>
    /// 延迟入队，消息在 <paramref name="delay"/> 之后才可被取出
    /// </summary>
    /// <param name="item">消息</param>
    /// <param name="delay">延迟时长</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task EnqueueAsync(T item, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        return EnqueueAtAsync(item, DateTimeOffset.UtcNow.Add(delay), cancellationToken);
    }

    /// <summary>
    /// 定时入队，消息到达 <paramref name="dueTime"/> 后才可被取出
    /// </summary>
    /// <param name="item">消息</param>
    /// <param name="dueTime">到期时刻</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task EnqueueAtAsync(T item, DateTimeOffset dueTime, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _queue.Enqueue(item, (dueTime.ToUnixTimeMilliseconds(), _sequence++));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 取出已到期的消息，最多 <paramref name="count"/> 条，取出的消息同时从队列移除
    /// </summary>
    /// <param name="count">最大取出条数，小于等于零时返回空集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已到期的消息集合</returns>
    public Task<IReadOnlyList<T>> DequeueDueAsync(int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return Task.FromResult<IReadOnlyList<T>>([]);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var due = new List<T>();

        lock (_gate)
        {
            while (due.Count < count && _queue.TryPeek(out _, out var priority) && priority.DueAtMs <= now)
            {
                due.Add(_queue.Dequeue());
            }
        }

        return Task.FromResult<IReadOnlyList<T>>(due);
    }

    /// <summary>
    /// 获取队列消息总数，含未到期的消息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>队列中的消息条数</returns>
    public Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult((long)_queue.Count);
        }
    }
}
