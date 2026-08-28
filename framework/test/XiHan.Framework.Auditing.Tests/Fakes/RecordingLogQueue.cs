// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using XiHan.Framework.Auditing.Queues;

namespace XiHan.Framework.Auditing.Tests.Fakes;

/// <summary>
/// 记录调用轨迹的日志队列替身
/// </summary>
/// <remarks>
/// 供采集管道测试使用：把「走了哪个入队方法」「队列是否满」「入队时收到的取消令牌」全部暴露出来，
/// 使断言落在管道的编排选择上，而不是落在 Channel 的实现行为上（后者由 ChannelLogQueueTests 单独覆盖）。
/// </remarks>
/// <typeparam name="TRecord">日志记录类型</typeparam>
public sealed class RecordingLogQueue<TRecord> : ILogQueue<TRecord>
{
    private readonly List<TRecord> _enqueued = [];

    /// <summary>
    /// 已成功入队的记录（按入队顺序）
    /// </summary>
    public IReadOnlyList<TRecord> Enqueued => _enqueued;

    /// <summary>
    /// <see cref="TryEnqueue"/> 的调用次数
    /// </summary>
    public int TryEnqueueCallCount { get; private set; }

    /// <summary>
    /// <see cref="EnqueueAsync"/> 的调用次数
    /// </summary>
    public int EnqueueAsyncCallCount { get; private set; }

    /// <summary>
    /// <see cref="TryEnqueue"/> 的返回值，设为 false 模拟队列已满
    /// </summary>
    public bool TryEnqueueResult { get; set; } = true;

    /// <summary>
    /// <see cref="EnqueueAsync"/> 要抛出的异常，为空表示正常入队
    /// </summary>
    public Exception? EnqueueAsyncException { get; set; }

    /// <summary>
    /// 最近一次 <see cref="EnqueueAsync"/> 收到的取消令牌
    /// </summary>
    public CancellationToken LastEnqueueAsyncToken { get; private set; }

    /// <summary>
    /// 队列数量
    /// </summary>
    public int Count => _enqueued.Count;

    /// <summary>
    /// 尝试入队
    /// </summary>
    /// <param name="record">日志记录</param>
    /// <returns>入队结果</returns>
    public bool TryEnqueue(TRecord record)
    {
        TryEnqueueCallCount++;
        if (!TryEnqueueResult)
        {
            return false;
        }

        _enqueued.Add(record);
        return true;
    }

    /// <summary>
    /// 入队
    /// </summary>
    /// <param name="record">日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>入队任务</returns>
    public ValueTask EnqueueAsync(TRecord record, CancellationToken cancellationToken = default)
    {
        EnqueueAsyncCallCount++;
        LastEnqueueAsyncToken = cancellationToken;

        if (EnqueueAsyncException is not null)
        {
            return ValueTask.FromException(EnqueueAsyncException);
        }

        _enqueued.Add(record);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 出队
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>日志记录的异步序列</returns>
    public IAsyncEnumerable<TRecord> DequeueAllAsync(CancellationToken cancellationToken = default)
    {
        return DrainAsync(cancellationToken);
    }

    private async IAsyncEnumerable<TRecord> DrainAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var record in _enqueued.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return record;
        }

        await Task.CompletedTask;
    }
}
