// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Exceptions.Abstracts;
using XiHan.Framework.Core.Extensions.Exceptions;

namespace XiHan.Framework.Core.Tests.Exceptions;

/// <summary>
/// 业务异常测试
/// </summary>
/// <remarks>
/// 业务异常一次兑现四份契约：<see cref="IBusinessException"/>（可回显）、<see cref="IHasErrorCode"/>（错误码）、
/// <see cref="IHasErrorDetails"/>（明细）、<see cref="IHasLogLevel"/>（日志级别）。
/// 与框架异常最大的区别是<b>不加任何消息前缀</b>——消息会原样回给调用方，因此这条必须逐字锁死。
/// 默认日志级别是 Warning 而不是 Error，这决定了业务失败不会污染错误告警，同样锁死。
/// </remarks>
public class BusinessExceptionTests
{
    /// <summary>
    /// 全部参数省略时错误码、明细与本地化消息都为空，日志级别为警告
    /// </summary>
    [Fact]
    public void Constructor_Default_HasNoCodeAndWarningLevel()
    {
        var exception = new BusinessException();

        Assert.Null(exception.Code);
        Assert.Null(exception.Details);
        Assert.Null(exception.LocalizableMessage);
        Assert.Null(exception.InnerException);
        Assert.Equal(LogLevel.Warning, exception.LogLevel);
    }

    /// <summary>
    /// 传入消息时原样保留，不附加任何框架前缀
    /// </summary>
    [Fact]
    public void Constructor_WithMessage_KeepsMessageVerbatim()
    {
        var exception = new BusinessException(message: "余额不足");

        Assert.Equal("余额不足", exception.Message);
    }

    /// <summary>
    /// 全参构造把每个参数都落到对应属性上
    /// </summary>
    [Fact]
    public void Constructor_WithAllArguments_MapsEveryArgument()
    {
        var inner = new InvalidOperationException("底层失败");

        var exception = new BusinessException("XH-1001", "余额不足", "当前余额 3 元，需要 10 元", inner, LogLevel.Error);

        Assert.Equal("XH-1001", exception.Code);
        Assert.Equal("余额不足", exception.Message);
        Assert.Equal("当前余额 3 元，需要 10 元", exception.Details);
        Assert.Same(inner, exception.InnerException);
        Assert.Equal(LogLevel.Error, exception.LogLevel);
    }

    /// <summary>
    /// 三个契约属性在构造之后仍可改写
    /// </summary>
    [Fact]
    public void ContractProperties_AreWritableAfterConstruction()
    {
        var exception = new BusinessException(message: "余额不足")
        {
            Code = "XH-1002",
            Details = "补充明细",
            LogLevel = LogLevel.Critical,
            LocalizableMessage = "本地化占位"
        };

        Assert.Equal("XH-1002", ((IHasErrorCode)exception).Code);
        Assert.Equal("补充明细", ((IHasErrorDetails)exception).Details);
        Assert.Equal(LogLevel.Critical, ((IHasLogLevel)exception).LogLevel);
        Assert.Equal("本地化占位", exception.LocalizableMessage as string);
    }

    /// <summary>
    /// 写入数据返回自身，支持链式调用
    /// </summary>
    [Fact]
    public void WithData_ReturnsSameInstanceForChaining()
    {
        var exception = new BusinessException(message: "余额不足");

        var returned = exception.WithData("userId", 42).WithData("balance", 3);

        Assert.Same(exception, returned);
        Assert.Equal(42, (int)exception.Data["userId"]!);
        Assert.Equal(3, (int)exception.Data["balance"]!);
        Assert.Equal(2, exception.Data.Count);
    }

    /// <summary>
    /// 同名键重复写入时后写覆盖先写
    /// </summary>
    [Fact]
    public void WithData_WithDuplicateKey_OverwritesPreviousValue()
    {
        var exception = new BusinessException(message: "余额不足");

        exception.WithData("userId", 1).WithData("userId", 2);

        Assert.Equal(2, (int)exception.Data["userId"]!);
        Assert.Single(exception.Data);
    }

    /// <summary>
    /// 类型同时落在业务异常与三个描述性契约上
    /// </summary>
    [Fact]
    public void Type_ImplementsBusinessContracts()
    {
        var exception = new BusinessException();

        Assert.IsAssignableFrom<Exception>(exception);
        Assert.IsAssignableFrom<IBusinessException>(exception);
        Assert.IsAssignableFrom<IHasErrorCode>(exception);
        Assert.IsAssignableFrom<IHasErrorDetails>(exception);
        Assert.IsAssignableFrom<IHasLogLevel>(exception);
    }

    /// <summary>
    /// 业务异常不是框架异常，两条继承线互不相交
    /// </summary>
    /// <remarks>
    /// 框架异常表示"框架自己出错了"，业务异常表示"调用方的业务状态不满足"，
    /// 统一异常处理按这两条线分流，混在一起会让业务失败被当成框架故障告警。
    /// </remarks>
    [Fact]
    public void Type_IsNotXiHanException()
    {
        Assert.False(typeof(XiHanException).IsAssignableFrom(typeof(BusinessException)));
    }

    /// <summary>
    /// 框架扩展方法能透过日志级别契约读到构造期写入的级别
    /// </summary>
    [Fact]
    public void GetLogLevel_ReadsConfiguredLevelInsteadOfDefaultError()
    {
        Assert.Equal(LogLevel.Warning, new BusinessException().GetLogLevel());
        Assert.Equal(LogLevel.Information, new BusinessException(logLevel: LogLevel.Information).GetLogLevel());
    }
}
