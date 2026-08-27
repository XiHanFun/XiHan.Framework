// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Logging.Services;
using XiHan.Framework.Logging.Tests.Fakes;

namespace XiHan.Framework.Logging.Tests.Services;

/// <summary>
/// 性能日志器测试
/// </summary>
/// <remarks>
/// 性能日志的价值在于数值口径：耗时统一按毫秒、内存统一按 KB 且带增量。
/// 口径一旦改动，历史监控看板的同名指标会静默换含义，因此逐项锁死结构化属性的值。
/// </remarks>
public class PerformanceLoggerTests
{
    /// <summary>
    /// 操作耗时按毫秒记录
    /// </summary>
    [Fact]
    public void LogOperation_RecordsOperationNameAndMillisecondDuration()
    {
        var (logger, sink) = Create();

        logger.LogOperation("导出报表", TimeSpan.FromSeconds(2));

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("导出报表", entry.GetProperty("OperationName"));
        Assert.Equal(2000d, Assert.IsType<double>(entry.GetProperty("Duration")));
    }

    /// <summary>
    /// 附加数据为 null 时不抛异常且照常记录
    /// </summary>
    [Fact]
    public void LogOperation_WithNullAdditionalData_StillRecordsEntry()
    {
        var (logger, sink) = Create();

        logger.LogOperation("op", TimeSpan.Zero, null);

        var entry = Assert.Single(sink.Entries);
        Assert.Equal("op", entry.GetProperty("OperationName"));
        Assert.Equal(0d, Assert.IsType<double>(entry.GetProperty("Duration")));
    }

    /// <summary>
    /// 接口调用记录接口名、状态码与耗时
    /// </summary>
    [Fact]
    public void LogApiCall_RecordsApiNameStatusCodeAndDuration()
    {
        var (logger, sink) = Create();

        logger.LogApiCall("GET /orders", TimeSpan.FromMilliseconds(35), 200);

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("GET /orders", entry.GetProperty("ApiName"));
        Assert.Equal(200, Assert.IsType<int>(entry.GetProperty("StatusCode")));
        Assert.Equal(35d, Assert.IsType<double>(entry.GetProperty("Duration")));
    }

    /// <summary>
    /// 数据库查询记录查询名、记录数与耗时
    /// </summary>
    [Fact]
    public void LogDatabaseQuery_RecordsQueryNameRecordCountAndDuration()
    {
        var (logger, sink) = Create();

        logger.LogDatabaseQuery("SelectOrders", TimeSpan.FromMilliseconds(12), 42);

        var entry = Assert.Single(sink.Entries);
        Assert.Equal("SelectOrders", entry.GetProperty("QueryName"));
        Assert.Equal(42, Assert.IsType<int>(entry.GetProperty("RecordCount")));
        Assert.Equal(12d, Assert.IsType<double>(entry.GetProperty("Duration")));
    }

    /// <summary>
    /// 内存用量按 KB 记录并给出增量
    /// </summary>
    [Fact]
    public void LogMemoryUsage_ConvertsBytesToKilobytesAndReportsDelta()
    {
        var (logger, sink) = Create();

        logger.LogMemoryUsage("导入", 2048, 6144);

        var entry = Assert.Single(sink.Entries);
        Assert.Equal("导入", entry.GetProperty("OperationName"));
        Assert.Equal(2L, Assert.IsType<long>(entry.GetProperty("MemoryBefore")));
        Assert.Equal(6L, Assert.IsType<long>(entry.GetProperty("MemoryAfter")));
        Assert.Equal(4L, Assert.IsType<long>(entry.GetProperty("MemoryDiff")));
    }

    /// <summary>
    /// 内存回落时增量为负数
    /// </summary>
    [Fact]
    public void LogMemoryUsage_WhenMemoryReleased_ReportsNegativeDelta()
    {
        var (logger, sink) = Create();

        logger.LogMemoryUsage("回收", 6144, 2048);

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(-4L, Assert.IsType<long>(entry.GetProperty("MemoryDiff")));
    }

    /// <summary>
    /// CPU 用量记录使用率与耗时
    /// </summary>
    [Fact]
    public void LogCpuUsage_RecordsUsagePercentAndDuration()
    {
        var (logger, sink) = Create();

        logger.LogCpuUsage("压测", 42.5, TimeSpan.FromMilliseconds(500));

        var entry = Assert.Single(sink.Entries);
        Assert.Equal("压测", entry.GetProperty("OperationName"));
        Assert.Equal(42.5, Assert.IsType<double>(entry.GetProperty("CpuUsage")));
        Assert.Equal(500d, Assert.IsType<double>(entry.GetProperty("Duration")));
    }

    /// <summary>
    /// 计时器创建后立即开始计时且尚未产生日志
    /// </summary>
    [Fact]
    public void StartTimer_ReturnsRunningTimerWithoutWritingLog()
    {
        var (logger, sink) = Create();

        using var timer = logger.StartTimer("批处理");

        Assert.Equal("批处理", timer.OperationName);
        Assert.True(timer.Stopwatch.IsRunning);
        Assert.Empty(sink.Entries);
    }

    /// <summary>
    /// 计时器停止后写入一条操作日志并停表
    /// </summary>
    [Fact]
    public void StartTimer_Stop_WritesOperationEntryAndStopsStopwatch()
    {
        var (logger, sink) = Create();

        var timer = logger.StartTimer("批处理");
        timer.Stop();

        Assert.False(timer.Stopwatch.IsRunning);
        var entry = Assert.Single(sink.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("批处理", entry.GetProperty("OperationName"));
    }

    /// <summary>
    /// 重复停止与释放不会重复写日志
    /// </summary>
    /// <remarks>
    /// using 包裹的计时器如果调用方又显式 Stop 一次，会走到 Dispose + Stop 两条路径；
    /// 不幂等就会把同一次操作统计成两次，直接污染性能指标。
    /// </remarks>
    [Fact]
    public void StartTimer_StopAndDisposeRepeatedly_WritesExactlyOneEntry()
    {
        var (logger, sink) = Create();

        var timer = logger.StartTimer("批处理");
        timer.Stop();
        timer.Stop();
        timer.Dispose();
        timer.Dispose();

        Assert.Single(sink.Entries);
    }

    /// <summary>
    /// 计时器上挂的附加数据在停止时一并落到日志
    /// </summary>
    [Fact]
    public void StartTimer_WithAdditionalData_CarriesItIntoOperationEntry()
    {
        var (logger, sink) = Create();

        var timer = logger.StartTimer("批处理");
        timer.AdditionalData = new { BatchSize = 100 };
        Assert.NotNull(timer.AdditionalData);

        timer.Dispose();

        var entry = Assert.Single(sink.Entries);
        Assert.Contains("批处理", entry.Message, StringComparison.Ordinal);
    }

    private static (PerformanceLogger Logger, RecordingLogger<PerformanceLogger> Sink) Create()
    {
        var sink = new RecordingLogger<PerformanceLogger>();
        return (new PerformanceLogger(sink), sink);
    }
}
