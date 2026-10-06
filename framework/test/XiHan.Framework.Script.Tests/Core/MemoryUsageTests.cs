// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Script.Core;

namespace XiHan.Framework.Script.Tests.Core;

/// <summary>
/// 内存采样契约测试
/// </summary>
/// <remarks>
/// 采样值本身随运行时波动，不可断言具体数字，因此只锁三件事：
/// 分配量必须是"后减前"的纯算术且恒为非负，<c>Complete</c> 必须把三代 GC 的绝对计数换算成本次执行的增量，
/// 以及"确实分配了内存就能测出来"。
/// <para>
/// 早先这里断言 <c>usage.MemoryAfter &gt; 0</c> 而随机红：当时的读数取自 <c>GC.GetTotalMemory(false)</c>，
/// 实测 .NET 10 工作站 GC 下强制一次阻塞 gen2 回收后，该方法会连续数百次返回负数（曾稳定读到 -1143688，
/// 即 dotnet/runtime#130888 的无符号下溢），用例锁的是运行时并不保证的性质。
/// 现已改用单调不减的 <c>GC.GetTotalAllocatedBytes</c>，非负成为真正的契约，可以正面断言。
/// </para>
/// </remarks>
public class MemoryUsageTests
{
    /// <summary>
    /// 分配量按执行后减执行前计算
    /// </summary>
    [Theory]
    [InlineData(100L, 300L, 200L)]
    [InlineData(300L, 300L, 0L)]
    public void AllocatedBytes_IsAfterMinusBefore(long before, long after, long expected)
    {
        var usage = new MemoryUsage
        {
            AllocatedBytesBefore = before,
            AllocatedBytesAfter = after
        };

        Assert.Equal(expected, usage.AllocatedBytes);
    }

    /// <summary>
    /// 新建对象的 GC 计数字典为空
    /// </summary>
    [Fact]
    public void NewInstance_HasEmptyGcCollections()
    {
        var usage = new MemoryUsage();

        Assert.Empty(usage.GcCollections);
        Assert.Equal(0, usage.AllocatedBytes);
    }

    /// <summary>
    /// 创建采样时记录执行前的累计分配量与三代 GC 的绝对计数
    /// </summary>
    [Fact]
    public void Create_CapturesBaselineForThreeGenerations()
    {
        var usage = MemoryUsage.Create();

        // 累计分配量单调不减，进程跑到这里必然已分配过内存
        Assert.True(usage.AllocatedBytesBefore > 0);
        Assert.Equal(3, usage.GcCollections.Count);
        Assert.True(usage.GcCollections.ContainsKey(0));
        Assert.True(usage.GcCollections.ContainsKey(1));
        Assert.True(usage.GcCollections.ContainsKey(2));
        Assert.All(usage.GcCollections.Values, count => Assert.True(count >= 0));
    }

    /// <summary>
    /// 完成采样后 GC 计数被换算为增量
    /// </summary>
    /// <remarks>
    /// 强制一次阻塞式 gen2 回收，三代计数都必然推进，因此增量必然都不小于 1。
    /// 这一条是运行时保证的：<c>GC.Collect(2, GCCollectionMode.Forced, blocking: true)</c>
    /// 立即执行一次涵盖 0~2 代的阻塞回收，返回前计数已推进。
    /// 这里不放宽成"增量 &gt;= 0"——那样一个把绝对计数原样写回的实现也能通过，
    /// 用例就丧失了它存在的意义；纯算术层面的"确实做了相减"由
    /// <see cref="Complete_SubtractsBaselineFromCurrentCounts"/> 另行锁定。
    /// </remarks>
    [Fact]
    public void Complete_ConvertsGcCountsIntoDeltas()
    {
        var usage = MemoryUsage.Create();

        // 必须显式 Forced：无参 GC.Collect() 走 GCCollectionMode.Default，运行时可以判断「没必要」
        // 而不真回收，gen2 计数便不推进
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        usage.Complete();

        Assert.Equal(3, usage.GcCollections.Count);
        Assert.All(usage.GcCollections.Values, count => Assert.True(count >= 1));
    }

    /// <summary>
    /// 完成采样时用当前计数减去基线，而不是写回绝对计数
    /// </summary>
    /// <remarks>
    /// 不依赖真实回收：给一个绝不可能是真实回收次数的负基线，
    /// 期望增量就是"当前绝对计数 + 100 万"，远高于任何真实绝对计数，
    /// 一旦实现退化成"直接写当前计数"，断言立刻失败。
    /// <c>Complete</c> 内部读计数的时刻夹在本用例前后两次读数之间，
    /// 而回收次数单调不减，所以增量必然落在这个闭区间里，与并发无关。
    /// </remarks>
    [Fact]
    public void Complete_SubtractsBaselineFromCurrentCounts()
    {
        const int SyntheticBaseline = -1_000_000;

        var usage = new MemoryUsage
        {
            GcCollections = new Dictionary<int, int>
            {
                { 0, SyntheticBaseline },
                { 1, SyntheticBaseline },
                { 2, SyntheticBaseline }
            }
        };

        var lowerBound = ReadCollectionCounts();
        usage.Complete();
        var upperBound = ReadCollectionCounts();

        Assert.Equal(3, usage.GcCollections.Count);
        for (var generation = 0; generation < 3; generation++)
        {
            Assert.InRange(
                usage.GcCollections[generation],
                lowerBound[generation] - SyntheticBaseline,
                upperBound[generation] - SyntheticBaseline);
        }
    }

    /// <summary>
    /// 完成采样会重新读取累计分配量
    /// </summary>
    [Fact]
    public void Complete_ResamplesAllocatedBytesAfter()
    {
        var usage = MemoryUsage.Create();
        usage.AllocatedBytesAfter = long.MinValue;

        usage.Complete();

        Assert.True(usage.AllocatedBytesAfter >= usage.AllocatedBytesBefore);
    }

    /// <summary>
    /// 采样区间内的分配能被测出来，且分配量恒为非负
    /// </summary>
    /// <remarks>
    /// 这是换用 <c>GC.GetTotalAllocatedBytes</c> 之后才成立的契约。
    /// 用例刻意分配<b>可回收的垃圾</b>再强制回收：
    /// 累计分配量会如实计入这 8 MB，而旧实现取的堆占用差值在回收之后归零甚至为负，
    /// 把"这段代码消耗了多少内存"记成了零消耗。
    /// 若改成让对象存活，两种实现的差值都是 +8 MB，用例就分辨不出实现是否退化。
    /// <para>
    /// 断言门槛取实际分配量的一半：<c>precise: false</c> 不统计尚未结算的分配上下文，
    /// 读数可能比实际偏低若干 KB（实测约 8 KB），卡在整数边界上会随机红。
    /// </para>
    /// </remarks>
    [Fact]
    public void AllocatedBytes_CountsCollectedGarbage()
    {
        const int BlockSize = 512 * 1024;
        const int BlockCount = 16;

        var usage = MemoryUsage.Create();

        // 每块都大于大对象堆阈值，强制回收后必然被释放，不会留在堆上
        var checksum = 0;
        for (var i = 0; i < BlockCount; i++)
        {
            var garbage = new byte[BlockSize];
            garbage[0] = (byte)i;
            checksum += garbage[0];
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();

        usage.Complete();

        Assert.Equal(BlockCount * (BlockCount - 1) / 2, checksum);
        Assert.True(usage.AllocatedBytes >= BlockSize * BlockCount / 2);
    }

    /// <summary>
    /// 没有发生分配时分配量为零而不是负数
    /// </summary>
    [Fact]
    public void AllocatedBytes_IsNeverNegative()
    {
        var usage = MemoryUsage.Create();
        usage.Complete();

        Assert.True(usage.AllocatedBytes >= 0);
    }

    /// <summary>
    /// 在没有基线的对象上完成采样不会抛异常
    /// </summary>
    [Fact]
    public void Complete_WithoutBaseline_DoesNotThrow()
    {
        var usage = new MemoryUsage();

        usage.Complete();

        Assert.True(usage.AllocatedBytesAfter > 0);
        Assert.Empty(usage.GcCollections);
    }

    /// <summary>
    /// 读取三代 GC 的绝对回收次数
    /// </summary>
    private static int[] ReadCollectionCounts()
    {
        return [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
    }
}
