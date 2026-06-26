#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IRedisDelayQueue
// Guid:5b6c7d8e-9f0a-4123-b4c5-6d7e8f9a0b1c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/06/16 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Caching.Distributed.Abstracts;

/// <summary>
/// 基于 Redis Sorted Set 的泛型延迟队列。
/// </summary>
/// <remarks>
/// 每个消息类型 <typeparamref name="T"/> 对应一个独立 ZSET（键由类型派生），<c>score = 到期时间戳(ms)</c>，消息按 JSON 序列化。
/// 消费者周期性调用 <see cref="DequeueDueAsync"/> 取出"已到期"的消息（Lua 原子领取，多消费者不重复）。
/// 延迟精度 ≈ 消费者轮询周期（到期项不主动唤醒，需定期检查）。注册为单例（每个封闭类型一个实例）。
/// </remarks>
/// <typeparam name="T">消息类型</typeparam>
public interface IRedisDelayQueue<T>
{
    /// <summary>
    /// 延迟入队：<paramref name="delay"/> 之后才可被取出。
    /// </summary>
    /// <param name="item">消息</param>
    /// <param name="delay">延迟时长</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task EnqueueAsync(T item, TimeSpan delay, CancellationToken cancellationToken = default);

    /// <summary>
    /// 定时入队：到达 <paramref name="dueTime"/> 后才可被取出。
    /// </summary>
    /// <param name="item">消息</param>
    /// <param name="dueTime">到期时刻</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task EnqueueAtAsync(T item, DateTimeOffset dueTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取出已到期（score ≤ 当前时间）的消息，最多 <paramref name="count"/> 条；原子领取，已取出的即从队列移除。
    /// </summary>
    /// <param name="count">最大取出条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已到期的消息集合</returns>
    Task<IReadOnlyList<T>> DequeueDueAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// 队列总数（含未到期）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<long> CountAsync(CancellationToken cancellationToken = default);
}
