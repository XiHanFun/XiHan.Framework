// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing.Queues;

/// <summary>
/// 日志队列
/// </summary>
/// <typeparam name="TRecord">日志记录类型</typeparam>
public interface ILogQueue<TRecord>
{
    /// <summary>
    /// 队列数量
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 尝试入队（不等待）
    /// </summary>
    /// <param name="record">日志记录</param>
    /// <returns>入队成功返回 true；队列已满返回 false（记录未入队）</returns>
    bool TryEnqueue(TRecord record);

    /// <summary>
    /// 入队（队列满时等待，直到有空位或被取消）
    /// </summary>
    /// <param name="record">日志记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask EnqueueAsync(TRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// 出队
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    IAsyncEnumerable<TRecord> DequeueAllAsync(CancellationToken cancellationToken = default);
}
