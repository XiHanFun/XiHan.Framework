// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Distributed.Abstracts;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// Stream 可靠队列替身
/// </summary>
/// <remarks>
/// 读取与认领都从预置批次里出队，取完即返回空批次，用于驱动消费循环的确认、重投与死信分支，全程不连 Redis。
/// </remarks>
/// <typeparam name="T">消息类型</typeparam>
internal sealed class FakeRedisStreamQueue<T> : IRedisStreamQueue<T>
{
    private readonly Queue<IReadOnlyList<RedisStreamMessage<T>>> _readBatches = new();
    private readonly Queue<IReadOnlyList<RedisStreamMessage<T>>> _claimBatches = new();

    /// <summary>
    /// 已入队消息
    /// </summary>
    public List<T> Enqueued { get; } = [];

    /// <summary>
    /// 已确认的消息 ID
    /// </summary>
    public List<string> Acked { get; } = [];

    /// <summary>
    /// 读取调用次数
    /// </summary>
    public int ReadCount { get; private set; }

    /// <summary>
    /// 认领调用次数
    /// </summary>
    public int ClaimCount { get; private set; }

    /// <summary>
    /// 等待唤醒调用次数
    /// </summary>
    public int WaitCount { get; private set; }

    /// <summary>
    /// 最近一次收到的消费者名
    /// </summary>
    public string? LastConsumer { get; private set; }

    /// <summary>
    /// 最近一次收到的批次大小
    /// </summary>
    public int LastBatchSize { get; private set; }

    /// <summary>
    /// 最近一次收到的认领空闲阈值
    /// </summary>
    public TimeSpan LastMinIdle { get; private set; }

    /// <summary>
    /// 等待唤醒时执行的回调，供用例结束消费循环
    /// </summary>
    public Action? WaitCallback { get; set; }

    /// <summary>
    /// 预置一批可读消息
    /// </summary>
    /// <param name="messages">消息集合</param>
    public void EnqueueReadBatch(params RedisStreamMessage<T>[] messages)
    {
        _readBatches.Enqueue(messages);
    }

    /// <summary>
    /// 预置一批可认领消息
    /// </summary>
    /// <param name="messages">消息集合</param>
    public void EnqueueClaimBatch(params RedisStreamMessage<T>[] messages)
    {
        _claimBatches.Enqueue(messages);
    }

    /// <summary>
    /// 入队
    /// </summary>
    /// <param name="item">消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>条目 ID</returns>
    public Task<string> EnqueueAsync(T item, CancellationToken cancellationToken = default)
    {
        Enqueued.Add(item);

        return Task.FromResult($"{Enqueued.Count}-0");
    }

    /// <summary>
    /// 读取一批新消息
    /// </summary>
    /// <param name="consumer">消费者名</param>
    /// <param name="count">最大条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息集合</returns>
    public Task<IReadOnlyList<RedisStreamMessage<T>>> ReadAsync(string consumer, int count, CancellationToken cancellationToken = default)
    {
        ReadCount++;
        LastConsumer = consumer;
        LastBatchSize = count;

        IReadOnlyList<RedisStreamMessage<T>> empty = [];

        return Task.FromResult(_readBatches.Count > 0 ? _readBatches.Dequeue() : empty);
    }

    /// <summary>
    /// 确认消息
    /// </summary>
    /// <param name="messageIds">消息 ID 集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    public Task AckAsync(IEnumerable<string> messageIds, CancellationToken cancellationToken = default)
    {
        Acked.AddRange(messageIds);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 认领空闲的待确认消息
    /// </summary>
    /// <param name="consumer">消费者名</param>
    /// <param name="minIdle">最小空闲时长</param>
    /// <param name="count">最大条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息集合</returns>
    public Task<IReadOnlyList<RedisStreamMessage<T>>> ClaimStaleAsync(string consumer, TimeSpan minIdle, int count, CancellationToken cancellationToken = default)
    {
        ClaimCount++;
        LastConsumer = consumer;
        LastMinIdle = minIdle;

        IReadOnlyList<RedisStreamMessage<T>> empty = [];

        return Task.FromResult(_claimBatches.Count > 0 ? _claimBatches.Dequeue() : empty);
    }

    /// <summary>
    /// 队列长度
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已入队条数</returns>
    public Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult((long)Enqueued.Count);
    }

    /// <summary>
    /// 等待唤醒
    /// </summary>
    /// <param name="timeout">最长等待时长</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    public Task WaitForSignalAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        WaitCount++;
        WaitCallback?.Invoke();

        return Task.CompletedTask;
    }
}
