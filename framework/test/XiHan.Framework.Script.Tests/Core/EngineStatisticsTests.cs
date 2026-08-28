// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Script.Core;

namespace XiHan.Framework.Script.Tests.Core;

/// <summary>
/// 引擎统计信息计算测试
/// </summary>
/// <remarks>
/// 三个派生属性都带除零分支，是最容易在重构中被写成 <c>NaN</c> 的地方，这里把零样本与正常样本一起锁死。
/// </remarks>
public class EngineStatisticsTests
{
    /// <summary>
    /// 没有执行记录时成功率返回 0 而不是 NaN
    /// </summary>
    [Fact]
    public void SuccessRate_WhenNoExecution_IsZero()
    {
        var statistics = new EngineStatistics();

        Assert.Equal(0d, statistics.SuccessRate);
    }

    /// <summary>
    /// 成功率按成功次数占总次数的百分比计算
    /// </summary>
    [Theory]
    [InlineData(4, 1, 25d)]
    [InlineData(4, 2, 50d)]
    [InlineData(4, 4, 100d)]
    [InlineData(4, 0, 0d)]
    public void SuccessRate_IsPercentageOfTotalExecutions(long total, long successful, double expected)
    {
        var statistics = new EngineStatistics
        {
            TotalExecutions = total,
            SuccessfulExecutions = successful
        };

        Assert.Equal(expected, statistics.SuccessRate);
    }

    /// <summary>
    /// 没有缓存访问时命中率返回 0 而不是 NaN
    /// </summary>
    [Fact]
    public void CacheHitRate_WhenNoCacheAccess_IsZero()
    {
        var statistics = new EngineStatistics();

        Assert.Equal(0d, statistics.CacheHitRate);
    }

    /// <summary>
    /// 缓存命中率按命中次数占命中加未命中总数的百分比计算
    /// </summary>
    [Theory]
    [InlineData(3, 1, 75d)]
    [InlineData(1, 1, 50d)]
    [InlineData(0, 5, 0d)]
    [InlineData(5, 0, 100d)]
    public void CacheHitRate_IsPercentageOfCacheAccess(long hits, long misses, double expected)
    {
        var statistics = new EngineStatistics
        {
            CacheHits = hits,
            CacheMisses = misses
        };

        Assert.Equal(expected, statistics.CacheHitRate);
    }

    /// <summary>
    /// 运行时长按启动时间与当前时间之差计算
    /// </summary>
    [Fact]
    public void Uptime_IsMeasuredFromStartTime()
    {
        var statistics = new EngineStatistics
        {
            StartTime = DateTime.Now.AddSeconds(-5)
        };

        Assert.True(statistics.Uptime >= TimeSpan.FromSeconds(5));
        Assert.True(statistics.Uptime < TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// 新建统计对象的计数项全部归零
    /// </summary>
    [Fact]
    public void NewInstance_HasZeroedCounters()
    {
        var statistics = new EngineStatistics();

        Assert.Equal(0, statistics.TotalExecutions);
        Assert.Equal(0, statistics.SuccessfulExecutions);
        Assert.Equal(0, statistics.FailedExecutions);
        Assert.Equal(0, statistics.CacheHits);
        Assert.Equal(0, statistics.CacheMisses);
        Assert.Equal(0d, statistics.AverageExecutionTimeMs);
        Assert.Equal(0d, statistics.AverageCompilationTimeMs);
        Assert.Equal(0, statistics.CacheSize);
        Assert.Equal(0, statistics.TotalMemoryUsage);
    }
}
