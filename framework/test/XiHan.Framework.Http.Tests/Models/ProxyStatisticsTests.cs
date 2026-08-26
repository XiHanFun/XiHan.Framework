// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Http.Models;

namespace XiHan.Framework.Http.Tests.Models;

/// <summary>
/// <see cref="ProxyStatistics"/> 的统计逻辑测试
/// </summary>
public class ProxyStatisticsTests
{
    /// <summary>
    /// 无请求时成功率为零且默认可用
    /// </summary>
    [Fact]
    public void SuccessRate_IsZero_WhenNoRequests()
    {
        var stats = new ProxyStatistics();

        Assert.Equal(0d, stats.SuccessRate);
        Assert.True(stats.IsAvailable);
    }

    /// <summary>
    /// 成功请求更新计数并计算平均响应时间
    /// </summary>
    [Fact]
    public void RecordRequest_Success_UpdatesCountersAndAverage()
    {
        var stats = new ProxyStatistics();

        stats.RecordRequest(true, 100L);
        stats.RecordRequest(true, 300L);

        Assert.Equal(2L, stats.TotalRequests);
        Assert.Equal(2L, stats.SuccessCount);
        Assert.Equal(0L, stats.FailureCount);
        Assert.Equal(200d, stats.AverageResponseTime);
        Assert.Equal(0, stats.ConsecutiveFailures);
    }

    /// <summary>
    /// 失败请求递增失败数与连续失败数，成功率随之下降
    /// </summary>
    [Fact]
    public void RecordRequest_Failure_IncrementsFailureAndConsecutive()
    {
        var stats = new ProxyStatistics();
        stats.RecordRequest(true, 100L);
        stats.RecordRequest(false, 0L);
        stats.RecordRequest(false, 0L);

        Assert.Equal(3L, stats.TotalRequests);
        Assert.Equal(1L, stats.SuccessCount);
        Assert.Equal(2L, stats.FailureCount);
        Assert.Equal(2, stats.ConsecutiveFailures);
        Assert.Equal(1d / 3d, stats.SuccessRate);
    }

    /// <summary>
    /// 验证结果与重置正确更新状态
    /// </summary>
    [Fact]
    public void RecordValidation_And_Reset_UpdateState()
    {
        var stats = new ProxyStatistics();
        stats.RecordRequest(true, 50L);
        stats.RecordValidation(false);

        Assert.False(stats.IsAvailable);
        Assert.Equal(1, stats.ConsecutiveFailures);
        Assert.NotNull(stats.LastValidatedAt);

        stats.RecordValidation(true);
        Assert.True(stats.IsAvailable);
        Assert.Equal(0, stats.ConsecutiveFailures);

        stats.Reset();
        Assert.Equal(0L, stats.TotalRequests);
        Assert.Equal(0L, stats.SuccessCount);
        Assert.Equal(0L, stats.FailureCount);
        Assert.Equal(0d, stats.AverageResponseTime);
        Assert.Equal(0, stats.ConsecutiveFailures);
    }
}
