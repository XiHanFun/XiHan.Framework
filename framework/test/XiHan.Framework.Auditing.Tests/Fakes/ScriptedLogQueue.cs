// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;
using XiHan.Framework.Auditing.Queues;

namespace XiHan.Framework.Auditing.Tests.Fakes;

/// <summary>
/// 按剧本产出记录的日志队列替身
/// </summary>
/// <remarks>
/// 供后台消费者测试使用：先原样吐出预置记录，再根据 <c>blockAfterDrain</c> 决定
/// 「自然结束」（验证批量写入与收尾冲刷）还是「挂起等待取消」（验证优雅停止时的剩余批次冲刷）。
/// <see cref="Drained"/> 让测试能确定性地等到所有记录都已进入消费者的批次，避免靠 sleep 碰运气。
/// </remarks>
/// <typeparam name="TRecord">日志记录类型</typeparam>
public sealed class ScriptedLogQueue<TRecord> : ILogQueue<TRecord>
{
    private readonly IReadOnlyList<TRecord> _records;
    private readonly bool _blockAfterDrain;
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="records">要依次吐出的记录</param>
    /// <param name="blockAfterDrain">吐完后是否挂起直到取消</param>
    public ScriptedLogQueue(IReadOnlyList<TRecord> records, bool blockAfterDrain)
    {
        _records = records;
        _blockAfterDrain = blockAfterDrain;
    }

    /// <summary>
    /// 预置记录全部被消费方取走后完成
    /// </summary>
    public Task Drained => _drained.Task;

    /// <summary>
    /// 消费方实际开始枚举队列的次数
    /// </summary>
    public int DequeueCallCount { get; private set; }

    /// <summary>
    /// 队列数量
    /// </summary>
    public int Count => _records.Count;

    /// <summary>
    /// 尝试入队，本替身只用于消费侧，不支持写入
    /// </summary>
    /// <param name="record">日志记录</param>
    /// <returns>不返回，始终抛出</returns>
    public bool TryEnqueue(TRecord record)
    {
        throw new NotSupportedException("ScriptedLogQueue 只用于消费侧测试。");
    }

    /// <summary>
    /// 入队，本替身只用于消费侧，不支持写入
    /// </summary>
    /// <param name="record">日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>不返回，始终抛出</returns>
    public ValueTask EnqueueAsync(TRecord record, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("ScriptedLogQueue 只用于消费侧测试。");
    }

    /// <summary>
    /// 出队
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>日志记录的异步序列</returns>
    public async IAsyncEnumerable<TRecord> DequeueAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        DequeueCallCount++;

        foreach (var record in _records)
        {
            yield return record;
        }

        _drained.TrySetResult();

        if (_blockAfterDrain)
        {
            // 模拟真实 Channel：队列空了就一直等，直到 host 停止令牌触发
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }
}
