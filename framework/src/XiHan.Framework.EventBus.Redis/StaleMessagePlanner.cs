// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Redis;

/// <summary>
/// 待处理消息快照
/// </summary>
/// <param name="MessageId">消息标识</param>
/// <param name="IdleTimeMilliseconds">自上次投递以来的空闲时长</param>
/// <param name="DeliveryCount">已投递次数</param>
public readonly record struct PendingMessageSnapshot(string MessageId, long IdleTimeMilliseconds, int DeliveryCount);

/// <summary>
/// 滞留消息处置计划
/// </summary>
/// <param name="ToClaim">需接管重投的消息标识</param>
/// <param name="ToDeadLetter">需转入死信的消息</param>
public readonly record struct StaleMessagePlan(
    IReadOnlyList<string> ToClaim,
    IReadOnlyList<PendingMessageSnapshot> ToDeadLetter);

/// <summary>
/// 滞留消息处置判定
/// </summary>
/// <remarks>
/// 从消费流程中抽出的纯判定，不触碰 Redis，便于单独校验其规则。
/// </remarks>
public static class StaleMessagePlanner
{
    /// <summary>
    /// 判定一批待处理消息的处置方式
    /// </summary>
    /// <param name="pending">待处理消息快照</param>
    /// <param name="minIdleMilliseconds">判定为滞留的最小空闲时长</param>
    /// <param name="maxDeliveryCount">最大投递次数</param>
    /// <returns>处置计划</returns>
    public static StaleMessagePlan Plan(
        IReadOnlyList<PendingMessageSnapshot> pending,
        long minIdleMilliseconds,
        int maxDeliveryCount)
    {
        ArgumentNullException.ThrowIfNull(pending);

        var toClaim = new List<string>();
        var toDeadLetter = new List<PendingMessageSnapshot>();

        foreach (var message in pending)
        {
            // 空闲不足阈值的可能仍在被其他消费者处理，不接管，避免制造重复处理
            if (message.IdleTimeMilliseconds < minIdleMilliseconds)
            {
                continue;
            }

            if (message.DeliveryCount >= maxDeliveryCount)
            {
                toDeadLetter.Add(message);
                continue;
            }

            toClaim.Add(message.MessageId);
        }

        return new StaleMessagePlan(toClaim, toDeadLetter);
    }
}
