// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Extensions.Exceptions;

namespace XiHan.Framework.Core.Tests.Extensions.Exceptions;

/// <summary>
/// 异常扩展方法测试
/// </summary>
/// <remarks>
/// 四组扩展各自的边界：格式化消息要能吃 null 并沿内部异常链递归；取日志级别要区分「有契约」与「回落默认」；
/// 重新抛出必须保住原始堆栈（这正是它相对 <c>throw ex;</c> 存在的唯一理由）；条件抛出要保证条件为假时什么都不做。
/// </remarks>
public class ExceptionExtensionsTests
{
    /// <summary>
    /// 异常为空时格式化结果是空字符串而不是抛错
    /// </summary>
    [Fact]
    public void FormatMessage_WhenExceptionIsNull_ReturnsEmptyString()
    {
        Exception? exception = null;

        Assert.Equal(string.Empty, exception.FormatMessage());
    }

    /// <summary>
    /// 没有内部异常时原样返回消息
    /// </summary>
    [Fact]
    public void FormatMessage_WithoutInnerException_ReturnsMessage()
    {
        var exception = new InvalidOperationException("外层失败");

        Assert.Equal("外层失败", exception.FormatMessage());
    }

    /// <summary>
    /// 存在内部异常时沿异常链拼成一条可读线索
    /// </summary>
    [Fact]
    public void FormatMessage_WithNestedInnerExceptions_ChainsEveryLevel()
    {
        var deepest = new TimeoutException("连接超时");
        var middle = new InvalidOperationException("查询失败", deepest);
        var outer = new BusinessException(message: "下单失败", innerException: middle);

        var formatted = outer.FormatMessage();

        Assert.Equal("下单失败 --> 查询失败 --> 连接超时", formatted);
    }

    /// <summary>
    /// 开启隐藏开关后只返回最外层消息，不再展开内部异常
    /// </summary>
    /// <remarks>
    /// 参数名叫 <c>isHideStackTrace</c>，实际效果是「不展开内部异常链」，
    /// 这里按实际行为断言，同时把这层名实不符记在注释里，避免后来者按名字猜错语义。
    /// </remarks>
    [Fact]
    public void FormatMessage_WhenHideFlagIsSet_StopsAtOutermostMessage()
    {
        var outer = new InvalidOperationException("外层失败", new TimeoutException("连接超时"));

        Assert.Equal("外层失败", outer.FormatMessage(true));
    }

    /// <summary>
    /// 没有日志级别契约时回落到默认的错误级别
    /// </summary>
    [Fact]
    public void GetLogLevel_WithoutContract_ReturnsDefaultError()
    {
        Assert.Equal(LogLevel.Error, new InvalidOperationException("失败").GetLogLevel());
    }

    /// <summary>
    /// 没有日志级别契约时调用方给的默认级别生效
    /// </summary>
    [Fact]
    public void GetLogLevel_WithoutContract_HonorsCallerDefault()
    {
        Assert.Equal(LogLevel.Trace, new InvalidOperationException("失败").GetLogLevel(LogLevel.Trace));
    }

    /// <summary>
    /// 有日志级别契约时读契约值，忽略调用方给的默认级别
    /// </summary>
    /// <param name="configured">异常上配置的日志级别</param>
    [Theory]
    [InlineData(LogLevel.Critical)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.None)]
    public void GetLogLevel_WithContract_ReadsContractValue(LogLevel configured)
    {
        var exception = new BusinessException(message: "余额不足", logLevel: configured);

        Assert.Equal(configured, exception.GetLogLevel(LogLevel.Trace));
    }

    /// <summary>
    /// 重新抛出保住原始堆栈并且抛的是同一个实例
    /// </summary>
    /// <remarks>
    /// 这条是这个扩展方法存在的全部理由：直接 <c>throw ex;</c> 会把堆栈截断到重抛点，
    /// 线上排查会丢掉真正的失败位置。断言原始抛出方法名仍在堆栈里即可证明未被截断。
    /// </remarks>
    [Fact]
    public void ReThrow_PreservesOriginalStackTraceAndInstance()
    {
        InvalidOperationException? captured = null;
        try
        {
            ThrowFromNamedFrame();
        }
        catch (InvalidOperationException ex)
        {
            captured = ex;
        }

        Assert.NotNull(captured);
        var original = captured!;

        Assert.NotNull(original.StackTrace);
        Assert.Contains(nameof(ThrowFromNamedFrame), original.StackTrace!, StringComparison.Ordinal);

        var rethrown = Assert.Throws<InvalidOperationException>(() => original.ReThrow());

        Assert.Same(original, rethrown);
        Assert.NotNull(rethrown.StackTrace);
        Assert.Contains(nameof(ThrowFromNamedFrame), rethrown.StackTrace!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 条件为真时抛出的正是调用它的那个异常实例
    /// </summary>
    [Fact]
    public void ThrowIf_WhenConditionIsTrue_ThrowsSameInstance()
    {
        var exception = new InvalidOperationException("失败");

        var thrown = Assert.Throws<InvalidOperationException>(() => exception.ThrowIf(true));

        Assert.Same(exception, thrown);
    }

    /// <summary>
    /// 条件为假时什么都不做
    /// </summary>
    [Fact]
    public void ThrowIf_WhenConditionIsFalse_DoesNothing()
    {
        var exception = new InvalidOperationException("失败");

        exception.ThrowIf(false);
    }

    /// <summary>
    /// 委托返回真时抛出，并且委托只被求值一次
    /// </summary>
    [Fact]
    public void ThrowIf_WithPredicate_EvaluatesOnceAndThrowsWhenTrue()
    {
        var exception = new InvalidOperationException("失败");
        var evaluationCount = 0;

        var thrown = Assert.Throws<InvalidOperationException>(() => exception.ThrowIf(() =>
        {
            evaluationCount++;
            return true;
        }));

        Assert.Same(exception, thrown);
        Assert.Equal(1, evaluationCount);
    }

    /// <summary>
    /// 委托返回假时不抛出，但委托仍被求值
    /// </summary>
    [Fact]
    public void ThrowIf_WithPredicate_DoesNotThrowWhenFalse()
    {
        var exception = new InvalidOperationException("失败");
        var evaluationCount = 0;

        exception.ThrowIf(() =>
        {
            evaluationCount++;
            return false;
        });

        Assert.Equal(1, evaluationCount);
    }

    /// <summary>
    /// 从一个可被堆栈识别的具名方法里抛出异常
    /// </summary>
    /// <exception cref="InvalidOperationException">固定抛出</exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowFromNamedFrame()
    {
        throw new InvalidOperationException("底层失败");
    }
}
