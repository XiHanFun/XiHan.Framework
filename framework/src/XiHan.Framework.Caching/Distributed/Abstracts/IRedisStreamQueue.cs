#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IRedisStreamQueue
// Guid:3f4e5d6c-7a8b-491c-b1d2-3e4f5a6b7c8d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/06/16 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Caching.Distributed.Abstracts;

/// <summary>
/// 基于 Redis Streams + 消费组的泛型可靠消息队列（at-least-once）。
/// </summary>
/// <remarks>
/// 每个消息类型 <typeparamref name="T"/> 对应一个独立 Stream（键由类型派生），消息按 JSON 序列化存取。
/// 可靠性：消费用消费组读取（进入 PEL 待确认），处理成功 <see cref="AckAsync"/> 确认；消费者崩溃导致长时间未确认的消息，
/// 由 <see cref="ClaimStaleAsync"/> 认领重投（<see cref="RedisStreamMessage{T}.DeliveryCount"/> 超阈值可转死信）。
/// 入队即广播唤醒，消费者用 <see cref="WaitForSignalAsync"/> 阻塞等待，空闲不轮询、跨实例可用。
/// 注册为单例（每个封闭类型一个实例）。
/// </remarks>
/// <typeparam name="T">消息类型</typeparam>
public interface IRedisStreamQueue<T>
{
    /// <summary>
    /// 入队一条（并广播唤醒信号）。
    /// </summary>
    /// <param name="item">消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Stream 条目 ID</returns>
    Task<string> EnqueueAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>
    /// 以消费组读取一批新消息（进入待确认列表 PEL，需处理后 <see cref="AckAsync"/>）。
    /// </summary>
    /// <param name="consumer">消费者名（同组内唯一，如 机器名+实例）</param>
    /// <param name="count">最大读取条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息列表（空表示当前无新消息）</returns>
    Task<IReadOnlyList<RedisStreamMessage<T>>> ReadAsync(string consumer, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// 确认消息处理完成（从待确认列表移除）。
    /// </summary>
    /// <param name="messageIds">消息 ID 集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AckAsync(IEnumerable<string> messageIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 认领空闲超过 <paramref name="minIdle"/> 的待确认消息（消费者崩溃后重投给当前消费者），用于重试。
    /// </summary>
    /// <param name="consumer">认领的消费者名</param>
    /// <param name="minIdle">最小空闲时长（超过才认领，避免抢占正在处理的）</param>
    /// <param name="count">最大认领条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>认领到的消息（含已投递次数，可据此转死信）</returns>
    Task<IReadOnlyList<RedisStreamMessage<T>>> ClaimStaleAsync(string consumer, TimeSpan minIdle, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream 当前长度（积压估算）。
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task<long> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 阻塞等待"有新消息"信号：被入队唤醒、或超过 <paramref name="timeout"/>（兜底周期）后返回。
    /// 供消费者循环空闲时调用，替代轮询。
    /// </summary>
    /// <param name="timeout">最长等待时间（兜底周期）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WaitForSignalAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stream 消息。
/// </summary>
/// <typeparam name="T">消息类型</typeparam>
/// <param name="Id">Stream 条目 ID（用于 Ack）</param>
/// <param name="Value">消息内容</param>
/// <param name="DeliveryCount">已投递次数（首次为 1，每次认领重投 +1，可据此转死信）</param>
public readonly record struct RedisStreamMessage<T>(string Id, T? Value, long DeliveryCount);
