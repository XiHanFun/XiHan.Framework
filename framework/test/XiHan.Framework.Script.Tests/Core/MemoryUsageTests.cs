// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Script.Core;

namespace XiHan.Framework.Script.Tests.Core;

/// <summary>
/// 内存采样契约测试
/// </summary>
/// <remarks>
/// 采样值本身随运行时波动，不可断言具体数字，因此只锁两件事：
/// 增量必须是"后减前"的纯算术，<c>Complete</c> 必须把三代 GC 的绝对计数换算成本次执行的增量。
/// </remarks>
public class MemoryUsageTests
{
    /// <summary>
    /// 内存增量按执行后减执行前计算
    /// </summary>
    [Theory]
    [InlineData(100L, 300L, 200L)]
    [InlineData(300L, 300L, 0L)]
    [InlineData(300L, 100L, -200L)]
    public void MemoryIncrease_IsAfterMinusBefore(long before, long after, long expected)
    {
        var usage = new MemoryUsage
        {
            MemoryBefore = before,
            MemoryAfter = after
        };

        Assert.Equal(expected, usage.MemoryIncrease);
    }

    /// <summary>
    /// 新建对象的 GC 计数字典为空
    /// </summary>
    [Fact]
    public void NewInstance_HasEmptyGcCollections()
    {
        var usage = new MemoryUsage();

        Assert.Empty(usage.GcCollections);
        Assert.Equal(0, usage.MemoryIncrease);
    }

    /// <summary>
    /// 创建采样时记录执行前内存与三代 GC 的绝对计数
    /// </summary>
    [Fact]
    public void Create_CapturesBaselineForThreeGenerations()
    {
        var usage = MemoryUsage.Create();

        Assert.True(usage.MemoryBefore > 0);
        Assert.Equal(3, usage.GcCollections.Count);
        Assert.True(usage.GcCollections.ContainsKey(0));
        Assert.True(usage.GcCollections.ContainsKey(1));
        Assert.True(usage.GcCollections.ContainsKey(2));
        Assert.All(usage.GcCollections.Values, count => Assert.True(count >= 0));
    }

    /// <summary>
    /// 完成采样后 GC 计数被换算为增量
    /// </summary>
    [Fact]
    public void Complete_ConvertsGcCountsIntoDeltas()
    {
        var usage = MemoryUsage.Create();

        // 必须显式 Forced：无参 GC.Collect() 走 GCCollectionMode.Default，运行时可以判断「没必要」
        // 而不真回收，gen2 计数便不推进——用例随机红，且只在机器空闲/刚回收过时才复现
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        usage.Complete();

        Assert.True(usage.MemoryAfter > 0);
        Assert.All(usage.GcCollections.Values, count => Assert.True(count >= 0));
        Assert.True(usage.GcCollections[2] >= 1);
    }

    /// <summary>
    /// 在没有基线的对象上完成采样不会抛异常
    /// </summary>
    [Fact]
    public void Complete_WithoutBaseline_DoesNotThrow()
    {
        var usage = new MemoryUsage();

        usage.Complete();

        Assert.True(usage.MemoryAfter > 0);
        Assert.Empty(usage.GcCollections);
    }
}
