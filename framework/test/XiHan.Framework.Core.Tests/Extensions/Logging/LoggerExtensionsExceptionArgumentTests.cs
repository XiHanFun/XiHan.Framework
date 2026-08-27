// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Extensions.Logging;
using XiHan.Framework.Core.Tests.Fakes;

namespace XiHan.Framework.Core.Tests.Extensions.Logging;

/// <summary>
/// 日志扩展方法的异常传递测试
/// </summary>
/// <remarks>
/// <c>ILogger.Log</c> 有一个专用的 <c>exception</c> 形参，结构化接收端（Serilog / OTel / AppInsights）
/// 正是从这个形参上取异常类型与堆栈来建索引的。把异常塞进消息模板的格式化参数里，
/// 渲染出来的文本看着"有异常"，接收端拿到的 <c>exception</c> 却是 null，异常从此不可检索。
/// <para>
/// 既有用例只断言"级别正确 + 消息文本出现"，这两条在异常放错位置时照样是绿的，
/// 所以这里单独把"异常必须落在 exception 形参上、且不得混进消息文本"锁死。
/// </para>
/// </remarks>
public class LoggerExtensionsExceptionArgumentTests
{
    /// <summary>
    /// 各级别都把异常交给日志记录器的异常形参，而不是当成消息的格式化参数
    /// </summary>
    /// <param name="requested">调用方给出的级别</param>
    [Theory]
    [InlineData(LogLevel.Critical)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.None)]
    public void LogWithLevel_WithException_PassesExceptionToLoggerExceptionParameter(LogLevel requested)
    {
        var logger = new CoreRecordingLogger();
        var exception = new InvalidOperationException("底层失败");

        logger.LogWithLevel(requested, "分发测试", exception);

        var entry = Assert.Single(logger.Entries);
        Assert.Same(exception, entry.Exception);
    }

    /// <summary>
    /// 消息文本只保留调用方给的消息，异常不再以 ToString() 混进来
    /// </summary>
    /// <param name="requested">调用方给出的级别</param>
    [Theory]
    [InlineData(LogLevel.Critical)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.None)]
    public void LogWithLevel_WithException_KeepsMessageFreeOfExceptionText(LogLevel requested)
    {
        var logger = new CoreRecordingLogger();

        logger.LogWithLevel(requested, "分发测试", new InvalidOperationException("底层失败"));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("分发测试", entry.Message);
        Assert.DoesNotContain(nameof(InvalidOperationException), entry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 反例：不带异常的重载不应凭空造出一个异常对象
    /// </summary>
    [Fact]
    public void LogWithLevel_WithoutException_LeavesExceptionNull()
    {
        var logger = new CoreRecordingLogger();

        logger.LogWithLevel(LogLevel.Error, "分发测试");

        var entry = Assert.Single(logger.Entries);
        Assert.Null(entry.Exception);
        Assert.Equal("分发测试", entry.Message);
    }

    /// <summary>
    /// 记录异常时主条目挂着异常对象本身，消息只是异常消息
    /// </summary>
    [Fact]
    public void LogException_AttachesExceptionObjectToMainEntry()
    {
        var logger = new CoreRecordingLogger();
        var exception = new InvalidOperationException("底层失败");

        logger.LogException(exception);

        var entry = Assert.Single(logger.Entries);
        Assert.Same(exception, entry.Exception);
        Assert.Equal("底层失败", entry.Message);
    }

    /// <summary>
    /// 边界：补充说明段（错误码、错误详情）走的是不带异常的重载，不应重复挂异常
    /// </summary>
    [Fact]
    public void LogException_WithErrorCodeAndDetails_OnlyMainEntryCarriesException()
    {
        var logger = new CoreRecordingLogger();
        var exception = new BusinessException("XH-1001", "余额不足", "当前余额 3 元");

        logger.LogException(exception);

        Assert.Contains(logger.Entries, entry => ReferenceEquals(entry.Exception, exception));
        Assert.Contains(
            logger.Entries,
            entry => entry.Exception is null && entry.Message.Contains("异常代码:XH-1001", StringComparison.Ordinal));
        Assert.Contains(
            logger.Entries,
            entry => entry.Exception is null && entry.Message.Contains("异常详情:当前余额 3 元", StringComparison.Ordinal));
    }
}
