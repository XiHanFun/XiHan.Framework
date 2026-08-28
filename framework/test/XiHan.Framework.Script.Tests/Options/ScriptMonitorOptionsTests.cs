// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Script.Options;

namespace XiHan.Framework.Script.Tests.Options;

/// <summary>
/// 脚本监控选项测试
/// </summary>
/// <remarks>
/// 两个阈值(慢执行毫秒、高内存字节)直接决定 <c>ScriptMonitor</c> 何时抛性能警告事件，
/// <c>LogScriptCode</c> 决定脚本原文是否被落到日志里(涉及敏感信息)，都属于必须锁死的语义。
/// </remarks>
public class ScriptMonitorOptionsTests
{
    /// <summary>
    /// 默认开启日志但不记录脚本原文
    /// </summary>
    [Fact]
    public void Default_LogsWithoutScriptBody()
    {
        var options = ScriptMonitorOptions.Default;

        Assert.True(options.EnableLogging);
        Assert.False(options.LogScriptCode);
        Assert.Equal(10000, options.MaxLogEntries);
        Assert.Equal(5000, options.SlowExecutionThresholdMs);
        Assert.Equal(100 * 1024 * 1024, options.HighMemoryUsageThresholdBytes);
        Assert.True(options.EnableLogCleanup);
        Assert.Equal(24, options.LogRetentionHours);
    }

    /// <summary>
    /// 默认选项每次返回新实例
    /// </summary>
    [Fact]
    public void Default_ReturnsIndependentInstances()
    {
        var first = ScriptMonitorOptions.Default;
        var second = ScriptMonitorOptions.Default;

        Assert.NotSame(first, second);

        first.MaxLogEntries = 1;

        Assert.Equal(10000, second.MaxLogEntries);
    }

    /// <summary>
    /// 高性能预设收紧容量与阈值，并缩短保留时间
    /// </summary>
    [Fact]
    public void HighPerformance_TightensCapacityAndThresholds()
    {
        var options = ScriptMonitorOptions.HighPerformance();

        Assert.True(options.EnableLogging);
        Assert.False(options.LogScriptCode);
        Assert.Equal(5000, options.MaxLogEntries);
        Assert.Equal(1000, options.SlowExecutionThresholdMs);
        Assert.Equal(50 * 1024 * 1024, options.HighMemoryUsageThresholdBytes);
        Assert.True(options.EnableLogCleanup);
        Assert.Equal(12, options.LogRetentionHours);
    }

    /// <summary>
    /// 详细预设放开容量与阈值，并开始记录脚本原文
    /// </summary>
    [Fact]
    public void Verbose_LoosensCapacityAndRecordsScriptBody()
    {
        var options = ScriptMonitorOptions.Verbose();

        Assert.True(options.EnableLogging);
        Assert.True(options.LogScriptCode);
        Assert.Equal(50000, options.MaxLogEntries);
        Assert.Equal(10000, options.SlowExecutionThresholdMs);
        Assert.Equal(500L * 1024 * 1024, options.HighMemoryUsageThresholdBytes);
        Assert.True(options.EnableLogCleanup);
        Assert.Equal(72, options.LogRetentionHours);
    }

    /// <summary>
    /// 高性能预设的慢执行阈值必须严于详细预设
    /// </summary>
    [Fact]
    public void HighPerformance_IsStricterThanVerbose()
    {
        var highPerformance = ScriptMonitorOptions.HighPerformance();
        var verbose = ScriptMonitorOptions.Verbose();

        Assert.True(highPerformance.SlowExecutionThresholdMs < verbose.SlowExecutionThresholdMs);
        Assert.True(highPerformance.HighMemoryUsageThresholdBytes < verbose.HighMemoryUsageThresholdBytes);
        Assert.True(highPerformance.MaxLogEntries < verbose.MaxLogEntries);
    }
}
