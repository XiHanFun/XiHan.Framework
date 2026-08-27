// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Exceptions;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Exceptions;

/// <summary>
/// 业务规则验证异常测试
/// </summary>
/// <remarks>
/// 三个构造重载的语义差异是关键：只给规则时消息取自规则，
/// 给了自定义消息时 Message 用自定义值而 Details 仍保留规则原文，
/// 只给消息时没有被违反的规则实例。
/// </remarks>
public class BusinessRuleValidationExceptionTests
{
    /// <summary>
    /// 仅传规则时异常消息取自规则消息
    /// </summary>
    [Fact]
    public void Constructor_WithRuleOnly_UsesRuleMessage()
    {
        var rule = new SampleBusinessRule("余额不足", true);

        var exception = new BusinessRuleValidationException(rule);

        Assert.Equal("余额不足", exception.Message);
        Assert.Equal("余额不足", exception.Details);
        Assert.Same(rule, exception.BrokenRule);
    }

    /// <summary>
    /// 传规则与自定义消息时消息取自定义值、详情保留规则原文
    /// </summary>
    [Fact]
    public void Constructor_WithRuleAndMessage_KeepsBothTexts()
    {
        var rule = new SampleBusinessRule("余额不足", true);

        var exception = new BusinessRuleValidationException(rule, "支付被拒绝");

        Assert.Equal("支付被拒绝", exception.Message);
        Assert.Equal("余额不足", exception.Details);
        Assert.Same(rule, exception.BrokenRule);
    }

    /// <summary>
    /// 仅传消息时不携带被违反的规则实例
    /// </summary>
    [Fact]
    public void Constructor_WithMessageOnly_LeavesBrokenRuleNull()
    {
        var exception = new BusinessRuleValidationException("批量校验失败");

        Assert.Equal("批量校验失败", exception.Message);
        Assert.Equal("批量校验失败", exception.Details);
        Assert.Null(exception.BrokenRule);
    }

    /// <summary>
    /// 携带规则时字符串表示暴露规则类型名
    /// </summary>
    [Fact]
    public void ToString_WithBrokenRule_ExposesRuleTypeName()
    {
        var rule = new SampleBusinessRule("余额不足", true);

        var text = new BusinessRuleValidationException(rule).ToString();

        Assert.StartsWith($"Business Rule Broken: {nameof(SampleBusinessRule)}", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 不携带规则时字符串表示退化为通用提示
    /// </summary>
    [Fact]
    public void ToString_WithoutBrokenRule_UsesGenericPrefix()
    {
        var text = new BusinessRuleValidationException("批量校验失败").ToString();

        Assert.StartsWith("Business Rule Validation Failed", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 业务规则验证异常是领域异常的子类，可被领域异常统一处理
    /// </summary>
    [Fact]
    public void BusinessRuleValidationException_IsDomainException()
    {
        var exception = new BusinessRuleValidationException("失败");

        Assert.IsAssignableFrom<DomainException>(exception);
    }
}
