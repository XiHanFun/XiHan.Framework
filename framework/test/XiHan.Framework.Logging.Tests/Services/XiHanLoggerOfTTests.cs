// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Logging.Options;
using XiHan.Framework.Logging.Services;
using XiHan.Framework.Logging.Tests.Fakes;

namespace XiHan.Framework.Logging.Tests.Services;

/// <summary>
/// 泛型曦寒日志器测试
/// </summary>
/// <remarks>
/// XiHanLogger&lt;T&gt; 与非泛型版本是两份彼此独立的拷贝代码，一处改了另一处很容易漏改，
/// 因此对开关短路与级别映射这两条最容易走偏的行为做等价校验。
/// </remarks>
public class XiHanLoggerOfTTests
{
    /// <summary>
    /// 六个级别方法各自映射到对应的日志级别
    /// </summary>
    [Fact]
    public void LogMethods_MapToExpectedLogLevels()
    {
        var (logger, sink) = Create();

        logger.LogTrace("t");
        logger.LogDebug("d");
        logger.LogInfo("i");
        logger.LogWarn("w");
        logger.LogError("e");
        logger.LogCritical("c");

        Assert.Collection(
            sink.Entries,
            entry => Assert.Equal(LogLevel.Trace, entry.Level),
            entry => Assert.Equal(LogLevel.Debug, entry.Level),
            entry => Assert.Equal(LogLevel.Information, entry.Level),
            entry => Assert.Equal(LogLevel.Warning, entry.Level),
            entry => Assert.Equal(LogLevel.Error, entry.Level),
            entry => Assert.Equal(LogLevel.Critical, entry.Level));
    }

    /// <summary>
    /// 总开关关闭时所有写入方法都不落任何日志
    /// </summary>
    [Fact]
    public void LogMethods_WhenGloballyDisabled_WriteNothing()
    {
        var (logger, sink) = Create(options =>
        {
            options.IsEnabled = false;
            options.EnablePerformanceCounters = true;
        });

        logger.LogInfo("i");
        logger.LogError(new InvalidOperationException("x"), "e");
        logger.LogCritical(new InvalidOperationException("x"), "c");
        logger.LogStructured(LogLevel.Error, "s", new { A = 1 });
        logger.LogPerformance("op", TimeSpan.FromSeconds(1));

        Assert.Empty(sink.Entries);
        Assert.False(logger.IsEnabled(LogLevel.Critical));
    }

    /// <summary>
    /// 关闭结构化日志开关后结构化写入被短路
    /// </summary>
    [Fact]
    public void LogStructured_WhenStructuredLoggingDisabled_WritesNothing()
    {
        var (logger, sink) = Create(options => options.EnableStructuredLogging = false);

        logger.LogStructured(LogLevel.Warning, "s", new { A = 1 });

        Assert.Empty(sink.Entries);
    }

    /// <summary>
    /// 性能计数器默认关闭，开启后按毫秒记录
    /// </summary>
    [Fact]
    public void LogPerformance_IsGatedByPerformanceCounterSwitch()
    {
        var (disabledLogger, disabledSink) = Create();
        disabledLogger.LogPerformance("op", TimeSpan.FromMilliseconds(200));
        Assert.Empty(disabledSink.Entries);

        var (enabledLogger, enabledSink) = Create(options => options.EnablePerformanceCounters = true);
        enabledLogger.LogPerformance("op", TimeSpan.FromMilliseconds(200));

        var entry = Assert.Single(enabledSink.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("op", entry.GetProperty("OperationName"));
        Assert.Equal(200d, Assert.IsType<double>(entry.GetProperty("TotalMilliseconds")));
    }

    /// <summary>
    /// 带异常的错误日志把异常对象透传给下游
    /// </summary>
    [Fact]
    public void LogError_WithException_PassesExceptionThrough()
    {
        var (logger, sink) = Create();
        var exception = new InvalidOperationException("boom");

        logger.LogError(exception, "failed");

        var entry = Assert.Single(sink.Entries);
        Assert.Same(exception, entry.Exception);
    }

    /// <summary>
    /// 作用域直接下沉到下游日志器
    /// </summary>
    [Fact]
    public void BeginScope_DelegatesToUnderlyingLogger()
    {
        var (logger, sink) = Create();

        using (logger.BeginScope("tenant=t2"))
        {
            logger.LogInfo("in-scope");
        }

        var entry = Assert.Single(sink.Entries);
        Assert.Contains(entry.Scopes, scope => Equals(scope, "tenant=t2"));
    }

    private static (XiHanLogger<XiHanLoggerOfTTests> Logger, RecordingLogger<XiHanLoggerOfTTests> Sink) Create(
        Action<XiHanLoggingOptions>? configure = null)
    {
        var options = new XiHanLoggingOptions();
        configure?.Invoke(options);

        var sink = new RecordingLogger<XiHanLoggerOfTTests>();
        return (new XiHanLogger<XiHanLoggerOfTTests>(sink, Microsoft.Extensions.Options.Options.Create(options)), sink);
    }
}
