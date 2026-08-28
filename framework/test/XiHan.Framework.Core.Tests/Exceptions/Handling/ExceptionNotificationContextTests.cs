// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Exceptions.Handling;

namespace XiHan.Framework.Core.Tests.Exceptions.Handling;

/// <summary>
/// 异常通知上下文测试
/// </summary>
/// <remarks>
/// 上下文的日志级别有三态：显式传入 &gt; 异常自带的日志级别契约 &gt; 默认的 Error。
/// 这条优先级链决定了订阅者看到的级别，也决定了告警是否会被触发，因此三态各立一条用例。
/// </remarks>
public class ExceptionNotificationContextTests
{
    /// <summary>
    /// 异常为空时抛出参数空异常并带上参数名
    /// </summary>
    [Fact]
    public void Constructor_WhenExceptionIsNull_ThrowsArgumentNullException()
    {
        var thrown = Assert.Throws<ArgumentNullException>(() => new ExceptionNotificationContext(null!));

        Assert.Equal("exception", thrown.ParamName);
    }

    /// <summary>
    /// 未显式给日志级别且异常不带级别契约时回落到错误级别
    /// </summary>
    [Fact]
    public void Constructor_WithPlainException_FallsBackToErrorLevel()
    {
        var exception = new InvalidOperationException("底层失败");

        var context = new ExceptionNotificationContext(exception);

        Assert.Same(exception, context.Exception);
        Assert.Equal(LogLevel.Error, context.LogLevel);
    }

    /// <summary>
    /// 未显式给日志级别时读取异常自带的日志级别
    /// </summary>
    [Fact]
    public void Constructor_WithLogLevelAwareException_ReadsExceptionLogLevel()
    {
        var exception = new BusinessException(message: "余额不足", logLevel: LogLevel.Information);

        var context = new ExceptionNotificationContext(exception);

        Assert.Equal(LogLevel.Information, context.LogLevel);
    }

    /// <summary>
    /// 显式给出的日志级别优先于异常自带的级别
    /// </summary>
    [Fact]
    public void Constructor_WithExplicitLogLevel_OverridesExceptionLogLevel()
    {
        var exception = new BusinessException(message: "余额不足", logLevel: LogLevel.Information);

        var context = new ExceptionNotificationContext(exception, LogLevel.Critical);

        Assert.Equal(LogLevel.Critical, context.LogLevel);
    }

    /// <summary>
    /// 默认视为已处理，可显式标记为未处理
    /// </summary>
    [Fact]
    public void Handled_DefaultsToTrueAndCanBeOverridden()
    {
        var exception = new InvalidOperationException("底层失败");

        Assert.True(new ExceptionNotificationContext(exception).Handled);
        Assert.False(new ExceptionNotificationContext(exception, handled: false).Handled);
    }

    /// <summary>
    /// 三个属性都只读，订阅者不能改写上下文
    /// </summary>
    /// <remarks>
    /// 订阅者之间彼此独立，任何一个改写了级别或处理标记都会影响后面的订阅者，
    /// 因此这里把不可变性锁死。
    /// </remarks>
    [Fact]
    public void Properties_AreReadOnly()
    {
        var type = typeof(ExceptionNotificationContext);

        Assert.Null(type.GetProperty(nameof(ExceptionNotificationContext.Exception))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(ExceptionNotificationContext.LogLevel))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(ExceptionNotificationContext.Handled))!.SetMethod);
    }
}
