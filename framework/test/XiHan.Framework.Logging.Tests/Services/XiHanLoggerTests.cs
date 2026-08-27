// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Logging.Options;
using XiHan.Framework.Logging.Services;
using XiHan.Framework.Logging.Tests.Fakes;

namespace XiHan.Framework.Logging.Tests.Services;

/// <summary>
/// 曦寒日志器测试
/// </summary>
/// <remarks>
/// XiHanLogger 本身不产出任何返回值，它的全部职责是「按开关短路」与「把参数原样交给下游 ILogger」。
/// 因此断言全部落在手写记录器回放出来的级别、结构化属性与异常对象上。
/// </remarks>
public class XiHanLoggerTests
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
    /// 消息与参数分别落在独立的结构化属性上
    /// </summary>
    /// <remarks>
    /// 这里锁的是「参数没有被提前拼进消息串」：一旦被拼接，下游结构化后端就再也拿不到原始参数。
    /// </remarks>
    [Fact]
    public void LogInfo_KeepsMessageAndArgsAsSeparateProperties()
    {
        var (logger, sink) = Create();

        logger.LogInfo("hello", "a", "b");

        var entry = Assert.Single(sink.Entries);
        Assert.Equal("hello", entry.GetProperty("Message"));
        var args = Assert.IsType<object[]>(entry.GetProperty("Args"));
        Assert.Equal(new object[] { "a", "b" }, args);
    }

    /// <summary>
    /// 不传参数时参数属性是空数组而不是 null
    /// </summary>
    [Fact]
    public void LogInfo_WithoutArgs_RecordsEmptyArgsArray()
    {
        var (logger, sink) = Create();

        logger.LogInfo("hello");

        var entry = Assert.Single(sink.Entries);
        Assert.Empty(Assert.IsType<object[]>(entry.GetProperty("Args")));
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
            options.EnableStructuredLogging = true;
            options.EnablePerformanceCounters = true;
        });

        logger.LogTrace("t");
        logger.LogDebug("d");
        logger.LogInfo("i");
        logger.LogWarn("w");
        logger.LogError("e");
        logger.LogError(new InvalidOperationException("x"), "e");
        logger.LogCritical("c");
        logger.LogCritical(new InvalidOperationException("x"), "c");
        logger.LogStructured(LogLevel.Error, "s", new { A = 1 });
        logger.LogPerformance("op", TimeSpan.FromSeconds(1));

        Assert.Empty(sink.Entries);
    }

    /// <summary>
    /// 总开关关闭时任何级别都视为未启用
    /// </summary>
    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Critical)]
    public void IsEnabled_WhenGloballyDisabled_ReturnsFalseForEveryLevel(LogLevel level)
    {
        var (logger, _) = Create(options => options.IsEnabled = false);

        Assert.False(logger.IsEnabled(level));
    }

    /// <summary>
    /// 总开关打开时级别判断交给下游日志器
    /// </summary>
    [Fact]
    public void IsEnabled_WhenGloballyEnabled_DelegatesToUnderlyingLogger()
    {
        var (logger, sink) = Create();
        sink.MinLevel = LogLevel.Warning;

        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
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
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
        Assert.Equal("failed", entry.GetProperty("Message"));
    }

    /// <summary>
    /// 带异常的严重错误日志把异常对象透传给下游
    /// </summary>
    [Fact]
    public void LogCritical_WithException_PassesExceptionThrough()
    {
        var (logger, sink) = Create();
        var exception = new InvalidOperationException("boom");

        logger.LogCritical(exception, "fatal");

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(LogLevel.Critical, entry.Level);
        Assert.Same(exception, entry.Exception);
    }

    /// <summary>
    /// 关闭结构化日志开关后结构化写入被短路
    /// </summary>
    [Fact]
    public void LogStructured_WhenStructuredLoggingDisabled_WritesNothing()
    {
        var (logger, sink) = Create(options => options.EnableStructuredLogging = false);

        logger.LogStructured(LogLevel.Error, "s", new { A = 1 });

        Assert.Empty(sink.Entries);
    }

    /// <summary>
    /// 结构化日志按调用方指定的级别写入
    /// </summary>
    /// <remarks>
    /// 顺带锁住一条容易被误解的边界：结构化载荷只推给 Serilog 的环境上下文，
    /// 不会进入通用日志抽象的结构化状态，因此换成非 Serilog 的日志后端会看不到这些字段。
    /// </remarks>
    [Theory]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    public void LogStructured_WhenEnabled_WritesAtRequestedLevel(LogLevel level)
    {
        var (logger, sink) = Create();

        logger.LogStructured(level, "结构化消息", new { OrderId = 1 });

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(level, entry.Level);
        Assert.Equal("结构化消息", entry.GetProperty("Message"));
        Assert.False(entry.HasProperty("OrderId"));
    }

    /// <summary>
    /// 性能计数器默认关闭，性能日志被短路
    /// </summary>
    [Fact]
    public void LogPerformance_WhenCountersDisabledByDefault_WritesNothing()
    {
        var (logger, sink) = Create();

        logger.LogPerformance("op", TimeSpan.FromMilliseconds(120));

        Assert.Empty(sink.Entries);
    }

    /// <summary>
    /// 开启性能计数器后按毫秒记录操作耗时
    /// </summary>
    [Fact]
    public void LogPerformance_WhenCountersEnabled_RecordsOperationAndMilliseconds()
    {
        var (logger, sink) = Create(options => options.EnablePerformanceCounters = true);

        logger.LogPerformance("查询订单", TimeSpan.FromMilliseconds(1500));

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("查询订单", entry.GetProperty("OperationName"));
        Assert.Equal(1500d, Assert.IsType<double>(entry.GetProperty("TotalMilliseconds")));
    }

    /// <summary>
    /// 作用域直接下沉到下游日志器
    /// </summary>
    [Fact]
    public void BeginScope_DelegatesToUnderlyingLogger()
    {
        var (logger, sink) = Create();

        using (logger.BeginScope("tenant=t1"))
        {
            logger.LogInfo("in-scope");
        }

        logger.LogInfo("out-of-scope");

        Assert.Equal(2, sink.Entries.Count);
        Assert.Contains(sink.Entries[0].Scopes, scope => Equals(scope, "tenant=t1"));
        Assert.Empty(sink.Entries[1].Scopes);
    }

    private static (XiHanLogger Logger, RecordingLogger<XiHanLogger> Sink) Create(Action<XiHanLoggingOptions>? configure = null)
    {
        var options = new XiHanLoggingOptions();
        configure?.Invoke(options);

        var sink = new RecordingLogger<XiHanLogger>();
        return (new XiHanLogger(sink, Microsoft.Extensions.Options.Options.Create(options)), sink);
    }
}
