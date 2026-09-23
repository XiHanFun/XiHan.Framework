// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Threading;

namespace XiHan.Framework.Utils.Tests.Threading;

/// <summary>
/// 异步屏障的阶段编号测试
/// </summary>
/// <remarks>
/// 屏障把阶段编号、参与者总数、当前计数打包进一个 long：
/// 阶段编号占 bit 32-63、总数占 bit 16-31、当前计数占 bit 0-15。
/// 更新计数时原先用掩码 <c>0xFFFFFFFF0000L</c> 保留其余位，
/// 而该掩码只覆盖 bit 16-47，每次更新都把阶段编号的高 16 位清零，
/// 阶段号跑到 65536 就回绕（实测跑满 70000 个阶段后读出 4463）。
/// </remarks>
public class AsyncBarrierPhaseTests
{
    /// <summary>
    /// 阶段编号随每次全员到达递增
    /// </summary>
    [Fact]
    public async Task SignalAndWaitAsync_AdvancesPhaseNumber()
    {
        using var barrier = new AsyncBarrier(1);

        Assert.Equal(0, barrier.CurrentPhaseNumber);

        for (var expected = 1; expected <= 5; expected++)
        {
            var info = await barrier.SignalAndWaitAsync();

            Assert.Equal(expected - 1, info.PhaseNumber);
            Assert.Equal(expected, barrier.CurrentPhaseNumber);
        }
    }

    /// <summary>
    /// 阶段编号越过 16 位边界后不回绕
    /// </summary>
    /// <remarks>
    /// 必须真的跑过 65536 个阶段才能触发：这是掩码少覆盖 16 位的直接后果，
    /// 少于这个数的循环无法把缺陷照出来。单参与者的同步路径很快，整体在秒级。
    /// </remarks>
    [Fact]
    public async Task SignalAndWaitAsync_PhaseNumberDoesNotWrapAtSixteenBitBoundary()
    {
        const int PhaseCount = 70_000;

        using var barrier = new AsyncBarrier(1);

        for (var i = 0; i < PhaseCount; i++)
        {
            await barrier.SignalAndWaitAsync();
        }

        Assert.Equal(PhaseCount, barrier.CurrentPhaseNumber);
    }

    /// <summary>
    /// 跨越边界时参与者总数不被掩码破坏
    /// </summary>
    [Fact]
    public async Task SignalAndWaitAsync_KeepsParticipantCountAcrossPhases()
    {
        const int PhaseCount = 70_000;

        using var barrier = new AsyncBarrier(1);

        for (var i = 0; i < PhaseCount; i++)
        {
            await barrier.SignalAndWaitAsync();
        }

        Assert.Equal(1, barrier.ParticipantCount);
        Assert.Equal(0, barrier.ParticipantsArrived);
        Assert.Equal(1, barrier.ParticipantsRemaining);
    }

    /// <summary>
    /// 多参与者时所有人都到达才放行，且拿到同一个阶段编号
    /// </summary>
    [Fact]
    public async Task SignalAndWaitAsync_WithMultipleParticipants_ReleasesTogether()
    {
        using var barrier = new AsyncBarrier(3);

        var arrivals = await Task.WhenAll(
            barrier.SignalAndWaitAsync(),
            barrier.SignalAndWaitAsync(),
            barrier.SignalAndWaitAsync()).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.All(arrivals, info => Assert.Equal(0, info.PhaseNumber));
        Assert.Equal(1, barrier.CurrentPhaseNumber);
        Assert.Equal(0, barrier.ParticipantsArrived);
    }

    /// <summary>
    /// 重置后阶段编号回到零，参与者数量按新值生效
    /// </summary>
    [Fact]
    public async Task Reset_RestoresInitialState()
    {
        using var barrier = new AsyncBarrier(1);

        await barrier.SignalAndWaitAsync();
        await barrier.SignalAndWaitAsync();
        Assert.Equal(2, barrier.CurrentPhaseNumber);

        barrier.Reset(2);

        Assert.Equal(0, barrier.CurrentPhaseNumber);
        Assert.Equal(2, barrier.ParticipantCount);
        Assert.Equal(0, barrier.ParticipantsArrived);
    }
}
