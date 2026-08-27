// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Logging.Services;
using XiHan.Framework.Logging.Tests.Fakes;

namespace XiHan.Framework.Logging.Tests.Services;

/// <summary>
/// 结构化日志器测试
/// </summary>
/// <remarks>
/// 结构化数据是通过 Serilog 的环境上下文推入的，脱离配置好的 Serilog 管道就观察不到；
/// 这里能稳定断言的是消息模板、级别、异常与事件/业务这两类附加属性名，覆盖到调用方真正依赖的部分。
/// </remarks>
public class StructuredLoggerTests
{
    /// <summary>
    /// 信息级结构化日志按信息级写入并保留原始消息
    /// </summary>
    [Fact]
    public void LogInformation_WritesInformationEntryWithOriginalMessage()
    {
        var (logger, sink) = Create();

        logger.LogInformation("订单已创建", new { OrderId = 1 });

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("订单已创建", entry.Message);
        Assert.Null(entry.Exception);
    }

    /// <summary>
    /// 警告级结构化日志按警告级写入
    /// </summary>
    [Fact]
    public void LogWarning_WritesWarningEntry()
    {
        var (logger, sink) = Create();

        logger.LogWarning("库存不足", new { Sku = "A1" });

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("库存不足", entry.Message);
    }

    /// <summary>
    /// 错误级结构化日志按错误级写入
    /// </summary>
    [Fact]
    public void LogError_WithoutException_WritesErrorEntry()
    {
        var (logger, sink) = Create();

        logger.LogError("扣款失败", new { Amount = 10 });

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Null(entry.Exception);
    }

    /// <summary>
    /// 带异常的结构化错误日志把异常对象透传给下游
    /// </summary>
    [Fact]
    public void LogError_WithException_PassesExceptionThrough()
    {
        var (logger, sink) = Create();
        var exception = new InvalidOperationException("boom");

        logger.LogError(exception, "扣款失败", new { Amount = 10 });

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
        Assert.Equal("扣款失败", entry.Message);
    }

    /// <summary>
    /// 自定义级别的结构化日志按调用方指定级别写入
    /// </summary>
    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    public void Log_WritesAtRequestedLevel(LogLevel level)
    {
        var (logger, sink) = Create();

        logger.Log(level, "自定义级别", new { A = 1 });

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(level, entry.Level);
        Assert.Equal("自定义级别", entry.Message);
    }

    /// <summary>
    /// 事件日志把事件名同时写进消息与结构化属性
    /// </summary>
    [Fact]
    public void LogEvent_RecordsEventNameAsMessageAndProperty()
    {
        var (logger, sink) = Create();

        logger.LogEvent("order-created", new { OrderId = 1 });

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("Event: order-created", entry.Message);
        Assert.Equal("order-created", entry.GetProperty("EventName"));
    }

    /// <summary>
    /// 业务日志把业务动作同时写进消息与结构化属性
    /// </summary>
    [Fact]
    public void LogBusiness_RecordsBusinessActionAsMessageAndProperty()
    {
        var (logger, sink) = Create();

        logger.LogBusiness("pay", new { Amount = 10 });

        var entry = Assert.Single(sink.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("Business Action: pay", entry.Message);
        Assert.Equal("pay", entry.GetProperty("BusinessAction"));
    }

    /// <summary>
    /// 结构化数据为 null 时不应抛异常
    /// </summary>
    /// <remarks>
    /// 结构化数据往往来自上游可空对象，推空值是常见调用形态，不能因此中断业务线程。
    /// </remarks>
    [Fact]
    public void AllMethods_WithNullData_DoNotThrow()
    {
        var (logger, sink) = Create();

        logger.LogInformation("i", null!);
        logger.LogWarning("w", null!);
        logger.LogError("e", null!);
        logger.LogError(new InvalidOperationException("x"), "e", null!);
        logger.Log(LogLevel.Debug, "d", null!);
        logger.LogEvent("evt", null!);
        logger.LogBusiness("biz", null!);

        Assert.Equal(7, sink.Entries.Count);
    }

    private static (StructuredLogger Logger, RecordingLogger<StructuredLogger> Sink) Create()
    {
        var sink = new RecordingLogger<StructuredLogger>();
        return (new StructuredLogger(sink), sink);
    }
}
