// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Redis.Tests;

/// <summary>
/// 滞留消息处置判定的测试
/// </summary>
public class StaleMessagePlannerTests
{
    /// <summary>
    /// 空闲不足阈值的不接管
    /// </summary>
    /// <remarks>
    /// 这类消息很可能仍在被其他消费者处理，接管会制造重复处理。
    /// </remarks>
    [Fact]
    public void Plan_WhenIdleBelowThreshold_IgnoresMessage()
    {
        var plan = StaleMessagePlanner.Plan([new PendingMessageSnapshot("1-0", 5_000, 1)], 60_000, 5);

        Assert.Empty(plan.ToClaim);
        Assert.Empty(plan.ToDeadLetter);
    }

    /// <summary>
    /// 空闲达到阈值的接管重投
    /// </summary>
    [Fact]
    public void Plan_WhenIdleReachesThreshold_ClaimsMessage()
    {
        var plan = StaleMessagePlanner.Plan([new PendingMessageSnapshot("1-0", 60_000, 1)], 60_000, 5);

        Assert.Equal(["1-0"], plan.ToClaim);
        Assert.Empty(plan.ToDeadLetter);
    }

    /// <summary>
    /// 投递次数达到上限的转入死信而非继续接管
    /// </summary>
    [Fact]
    public void Plan_WhenDeliveryCountReachesMax_MovesToDeadLetter()
    {
        var plan = StaleMessagePlanner.Plan([new PendingMessageSnapshot("1-0", 90_000, 5)], 60_000, 5);

        Assert.Empty(plan.ToClaim);
        Assert.Equal("1-0", Assert.Single(plan.ToDeadLetter).MessageId);
    }

    /// <summary>
    /// 投递次数未达上限的继续接管
    /// </summary>
    [Fact]
    public void Plan_WhenDeliveryCountBelowMax_ClaimsMessage()
    {
        var plan = StaleMessagePlanner.Plan([new PendingMessageSnapshot("1-0", 90_000, 4)], 60_000, 5);

        Assert.Single(plan.ToClaim);
        Assert.Empty(plan.ToDeadLetter);
    }

    /// <summary>
    /// 投递次数超上限但空闲不足时不动它
    /// </summary>
    /// <remarks>
    /// 空闲判定先于投递次数判定：正在被处理的消息不应被判死。
    /// </remarks>
    [Fact]
    public void Plan_WhenDeliveryCountHighButNotIdle_IgnoresMessage()
    {
        var plan = StaleMessagePlanner.Plan([new PendingMessageSnapshot("1-0", 1_000, 99)], 60_000, 5);

        Assert.Empty(plan.ToClaim);
        Assert.Empty(plan.ToDeadLetter);
    }

    /// <summary>
    /// 混合批次各归其位
    /// </summary>
    [Fact]
    public void Plan_ForMixedBatch_SplitsByRule()
    {
        var plan = StaleMessagePlanner.Plan(
            [
                new PendingMessageSnapshot("live", 1_000, 1),
                new PendingMessageSnapshot("stale", 70_000, 2),
                new PendingMessageSnapshot("poison", 70_000, 5)
            ],
            60_000,
            5);

        Assert.Equal(["stale"], plan.ToClaim);
        Assert.Equal("poison", Assert.Single(plan.ToDeadLetter).MessageId);
    }

    /// <summary>
    /// 空批次得到空计划
    /// </summary>
    [Fact]
    public void Plan_ForEmptyBatch_ReturnsEmptyPlan()
    {
        var plan = StaleMessagePlanner.Plan([], 60_000, 5);

        Assert.Empty(plan.ToClaim);
        Assert.Empty(plan.ToDeadLetter);
    }
}
