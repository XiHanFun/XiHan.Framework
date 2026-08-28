// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Extensions.Logging;
using XiHan.Framework.Core.Logging;
using XiHan.Framework.Core.Tests.Fakes;

namespace XiHan.Framework.Core.Tests.Extensions.Logging;

/// <summary>
/// 日志扩展方法测试
/// </summary>
/// <remarks>
/// 两组契约：级别分发（把运行期的 <see cref="LogLevel"/> 值翻译成对应的日志方法，
/// <see cref="LogLevel.None"/> 与 <see cref="LogLevel.Debug"/> 一起落到兜底分支）；
/// 异常分发（<c>LogException</c> 依次补充错误码、错误详情、自述日志与异常数据四段）。
/// 自述日志那段还要处理聚合异常的去重——同一个内部异常既是 <c>InnerException</c> 又在 <c>InnerExceptions</c> 里，
/// 去重失效会让同一条明细打两遍，因此单独立用例。
/// </remarks>
public class LoggerExtensionsTests
{
    /// <summary>
    /// 级别分发把每个级别翻译到对应的日志方法上
    /// </summary>
    /// <param name="requested">调用方给出的级别</param>
    /// <param name="expected">日志记录器实际收到的级别</param>
    [Theory]
    [InlineData(LogLevel.Critical, LogLevel.Critical)]
    [InlineData(LogLevel.Error, LogLevel.Error)]
    [InlineData(LogLevel.Warning, LogLevel.Warning)]
    [InlineData(LogLevel.Information, LogLevel.Information)]
    [InlineData(LogLevel.Trace, LogLevel.Trace)]
    [InlineData(LogLevel.Debug, LogLevel.Debug)]
    [InlineData(LogLevel.None, LogLevel.Debug)]
    public void LogWithLevel_DispatchesToMatchingLevel(LogLevel requested, LogLevel expected)
    {
        var logger = new CoreRecordingLogger();

        logger.LogWithLevel(requested, "分发测试");

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(expected, entry.Level);
        Assert.Equal("分发测试", entry.Message);
    }

    /// <summary>
    /// 带异常的级别分发同样按级别落位，并把消息带出来
    /// </summary>
    /// <param name="requested">调用方给出的级别</param>
    /// <param name="expected">日志记录器实际收到的级别</param>
    [Theory]
    [InlineData(LogLevel.Critical, LogLevel.Critical)]
    [InlineData(LogLevel.Error, LogLevel.Error)]
    [InlineData(LogLevel.Warning, LogLevel.Warning)]
    [InlineData(LogLevel.Information, LogLevel.Information)]
    [InlineData(LogLevel.Trace, LogLevel.Trace)]
    [InlineData(LogLevel.Debug, LogLevel.Debug)]
    [InlineData(LogLevel.None, LogLevel.Debug)]
    public void LogWithLevel_WithException_DispatchesToMatchingLevel(LogLevel requested, LogLevel expected)
    {
        var logger = new CoreRecordingLogger();

        logger.LogWithLevel(requested, "分发测试", new InvalidOperationException("底层失败"));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(expected, entry.Level);
        Assert.Contains("分发测试", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 未显式给级别时按异常自身的日志级别契约记录
    /// </summary>
    [Fact]
    public void LogException_WithoutExplicitLevel_UsesExceptionLogLevel()
    {
        var logger = new CoreRecordingLogger();

        logger.LogException(new BusinessException(message: "余额不足", logLevel: LogLevel.Information));

        Assert.NotEmpty(logger.Entries);
        Assert.All(logger.Entries, entry => Assert.Equal(LogLevel.Information, entry.Level));
    }

    /// <summary>
    /// 异常不带级别契约时按默认的错误级别记录
    /// </summary>
    [Fact]
    public void LogException_WithPlainException_UsesErrorLevel()
    {
        var logger = new CoreRecordingLogger();

        logger.LogException(new InvalidOperationException("底层失败"));

        Assert.All(logger.Entries, entry => Assert.Equal(LogLevel.Error, entry.Level));
    }

    /// <summary>
    /// 显式给出的级别覆盖异常自身的级别
    /// </summary>
    [Fact]
    public void LogException_WithExplicitLevel_OverridesExceptionLogLevel()
    {
        var logger = new CoreRecordingLogger();

        logger.LogException(new BusinessException(message: "余额不足"), LogLevel.Critical);

        Assert.All(logger.Entries, entry => Assert.Equal(LogLevel.Critical, entry.Level));
    }

    /// <summary>
    /// 带错误码与错误详情的异常会额外补两段说明
    /// </summary>
    [Fact]
    public void LogException_WithErrorCodeAndDetails_WritesBothSections()
    {
        var logger = new CoreRecordingLogger();

        logger.LogException(new BusinessException("XH-1001", "余额不足", "当前余额 3 元"));

        Assert.Contains(logger.Entries, entry => entry.Message.Contains("异常代码:XH-1001", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("异常详情:当前余额 3 元", StringComparison.Ordinal));
    }

    /// <summary>
    /// 异常数据非空时输出一段数据清单
    /// </summary>
    [Fact]
    public void LogException_WithExceptionData_WritesDataSection()
    {
        var logger = new CoreRecordingLogger();
        var exception = new BusinessException(message: "余额不足").WithData("userId", 42);

        logger.LogException(exception);

        Assert.Contains(logger.Entries, entry => entry.Message.Contains("---------- 异常数据 ----------", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("userId = 42", StringComparison.Ordinal));
    }

    /// <summary>
    /// 异常数据为空时不产生数据清单
    /// </summary>
    [Fact]
    public void LogException_WithoutExceptionData_OmitsDataSection()
    {
        var logger = new CoreRecordingLogger();

        logger.LogException(new BusinessException(message: "余额不足"));

        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("异常数据", StringComparison.Ordinal));
    }

    /// <summary>
    /// 实现自述日志契约的异常会被回调一次
    /// </summary>
    [Fact]
    public void LogException_WithSelfLoggingException_InvokesSelfLogOnce()
    {
        var logger = new CoreRecordingLogger();
        var exception = new SelfLoggingTestException("模型校验失败");

        logger.LogException(exception);

        Assert.Equal(1, exception.LogCallCount);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("自述:模型校验失败", StringComparison.Ordinal));
    }

    /// <summary>
    /// 聚合异常里的自述日志异常各被回调一次，不因既是内部异常又在集合里而重复
    /// </summary>
    /// <remarks>
    /// 聚合异常的 <c>InnerException</c> 就是 <c>InnerExceptions[0]</c>，两条采集路径必然撞在一起，
    /// 去重一旦失效第一条明细会打两遍，日志里看起来像发生了两次校验失败。
    /// </remarks>
    [Fact]
    public void LogException_WithAggregateException_DeduplicatesSelfLoggingInners()
    {
        var logger = new CoreRecordingLogger();
        var first = new SelfLoggingTestException("第一条失败");
        var second = new SelfLoggingTestException("第二条失败");

        logger.LogException(new AggregateException(first, second));

        Assert.Equal(1, first.LogCallCount);
        Assert.Equal(1, second.LogCallCount);
    }

    /// <summary>
    /// 聚合异常里没有自述日志异常时不产生额外回调
    /// </summary>
    [Fact]
    public void LogException_WithAggregateOfPlainExceptions_WritesNoSelfLog()
    {
        var logger = new CoreRecordingLogger();

        logger.LogException(new AggregateException(new InvalidOperationException("甲失败"), new TimeoutException("乙失败")));

        Assert.NotEmpty(logger.Entries);
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("自述:", StringComparison.Ordinal));
    }

    /// <summary>
    /// 普通异常既不带错误码也不带错误详情时只留主消息那一段
    /// </summary>
    [Fact]
    public void LogException_WithPlainException_WritesOnlyMainEntry()
    {
        var logger = new CoreRecordingLogger();

        logger.LogException(new InvalidOperationException("底层失败"));

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("底层失败", entry.Message, StringComparison.Ordinal);
    }
}

/// <summary>
/// 实现自述日志契约的测试异常
/// </summary>
/// <remarks>
/// 核心库自己没有实现 <see cref="IExceptionWithSelfLogging"/> 的异常（那是留给上层的扩展点），
/// 因此这里手写一个最小实现来验证分发逻辑，同时记录被回调的次数以便断言去重。
/// </remarks>
public sealed class SelfLoggingTestException : Exception, IExceptionWithSelfLogging
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">异常消息</param>
    public SelfLoggingTestException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 自述日志被回调的次数
    /// </summary>
    public int LogCallCount { get; private set; }

    /// <summary>
    /// 记录自述日志
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public void Log(ILogger logger)
    {
        LogCallCount++;
        logger.LogWarning("自述:{Detail}", Message);
    }
}
