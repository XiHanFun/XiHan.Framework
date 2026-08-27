// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Extensions.Exceptions;

namespace XiHan.Framework.Core.Tests.Extensions.Exceptions;

/// <summary>
/// 异常消息拼接开关的语义测试
/// </summary>
/// <remarks>
/// <c>FormatMessage</c> 的开关原名 <c>isHideStackTrace</c>，文档也写成"隐藏异常规模信息"，
/// 可它压根没碰过堆栈：这个方法只拼消息文本，开关真正决定的是"还要不要沿内部异常链继续往下拼"。
/// 按原名理解的调用方以为传 true 只是少打堆栈，实际却把内部异常这条根因线索整条丢掉。
/// <para>
/// 这里刻意用具名实参调用，把改名后的形参名一并锁进用例：形参名是公开 API 的一部分，
/// 再被改回去或改成别的名字，这几条会直接编译不过而不是悄悄放行。
/// </para>
/// </remarks>
public class ExceptionExtensionsInnerExceptionSwitchTests
{
    /// <summary>
    /// 开关为真时只返回最外层消息，内部异常不再展开
    /// </summary>
    [Fact]
    public void FormatMessage_WhenHideInnerException_ReturnsOuterMessageOnly()
    {
        var outer = new InvalidOperationException("外层失败", new TimeoutException("内层超时"));

        Assert.Equal("外层失败", outer.FormatMessage(isHideInnerException: true));
    }

    /// <summary>
    /// 开关为假时沿整条内部异常链逐级展开
    /// </summary>
    [Fact]
    public void FormatMessage_WhenNotHidden_ExpandsWholeInnerChain()
    {
        var deepest = new TimeoutException("最内层超时");
        var middle = new InvalidOperationException("中层失败", deepest);
        var outer = new InvalidOperationException("外层失败", middle);

        Assert.Equal("外层失败 --> 中层失败 --> 最内层超时", outer.FormatMessage(isHideInnerException: false));
    }

    /// <summary>
    /// 默认不隐藏：不传开关时等价于展开内部异常链
    /// </summary>
    [Fact]
    public void FormatMessage_ByDefault_ExpandsInnerException()
    {
        var outer = new InvalidOperationException("外层失败", new TimeoutException("内层超时"));

        Assert.Equal(outer.FormatMessage(isHideInnerException: false), outer.FormatMessage());
    }

    /// <summary>
    /// 反例：开关与堆栈无关，两种取值下返回的都只有消息文本，不含任何堆栈内容
    /// </summary>
    [Fact]
    public void FormatMessage_WhicheverSwitchValue_NeverContainsStackTrace()
    {
        var captured = CaptureThrownException();

        // 前提自检：异常确实带上了堆栈，否则这条用例证明不了什么
        Assert.False(string.IsNullOrEmpty(captured.StackTrace), "异常应已带上堆栈");

        Assert.Equal("带堆栈的失败", captured.FormatMessage(isHideInnerException: true));
        Assert.Equal("带堆栈的失败", captured.FormatMessage(isHideInnerException: false));
    }

    /// <summary>
    /// 边界：没有内部异常时，开关取任何值结果都一样
    /// </summary>
    [Fact]
    public void FormatMessage_WhenNoInnerException_SwitchMakesNoDifference()
    {
        var exception = new InvalidOperationException("只有一层");

        Assert.Equal("只有一层", exception.FormatMessage(isHideInnerException: true));
        Assert.Equal("只有一层", exception.FormatMessage(isHideInnerException: false));
    }

    /// <summary>
    /// 真正抛一次再接住，让异常带上堆栈
    /// </summary>
    /// <returns>带堆栈的异常</returns>
    private static InvalidOperationException CaptureThrownException()
    {
        try
        {
            throw new InvalidOperationException("带堆栈的失败");
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }
}
