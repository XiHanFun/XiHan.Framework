#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ILogQueue
// Guid:ad7cb0b3-6c3f-4c0b-8f1a-29cda4d2b287
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 22:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Logging.Queues;

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
    /// 尝试入队
    /// </summary>
    /// <param name="record"></param>
    /// <returns></returns>
    bool TryEnqueue(TRecord record);

    /// <summary>
    /// 入队
    /// </summary>
    /// <param name="record"></param>
    /// <param name="cancellationToken"></param>
    ValueTask EnqueueAsync(TRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// 出队
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    IAsyncEnumerable<TRecord> DequeueAllAsync(CancellationToken cancellationToken = default);
}
