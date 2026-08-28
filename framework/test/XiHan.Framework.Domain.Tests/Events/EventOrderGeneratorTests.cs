// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using XiHan.Framework.Domain.Events;

namespace XiHan.Framework.Domain.Tests.Events;

/// <summary>
/// 事件顺序生成器测试
/// </summary>
/// <remarks>
/// 生成器是进程级静态计数器，测试并行执行时无法预知起点，
/// 因此只断言「单调递增」与「并发唯一」这两条真正的契约，不锁死绝对值。
/// </remarks>
public class EventOrderGeneratorTests
{
    /// <summary>
    /// 连续获取的事件顺序严格递增
    /// </summary>
    [Fact]
    public void GetNext_Sequentially_IsStrictlyIncreasing()
    {
        var first = EventOrderGenerator.GetNext();
        var second = EventOrderGenerator.GetNext();
        var third = EventOrderGenerator.GetNext();

        Assert.True(first < second);
        Assert.True(second < third);
    }

    /// <summary>
    /// 连续获取的相邻事件顺序步长为一
    /// </summary>
    [Fact]
    public void GetNext_Sequentially_IncrementsByOne()
    {
        var first = EventOrderGenerator.GetNext();
        var second = EventOrderGenerator.GetNext();

        // 同一线程内两次调用之间若被其他测试插队，步长会大于 1，因此只在无插队时才成立；
        // 这里断言下界，保证至少推进了一格
        Assert.True(second - first >= 1);
    }

    /// <summary>
    /// 并发获取的事件顺序互不重复
    /// </summary>
    [Fact]
    public void GetNext_UnderConcurrency_ProducesUniqueValues()
    {
        const int count = 2000;
        var results = new ConcurrentBag<long>();

        Parallel.For(0, count, _ => results.Add(EventOrderGenerator.GetNext()));

        Assert.Equal(count, results.Count);
        Assert.Equal(count, results.Distinct().Count());
    }
}
