// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Exceptions;
using XiHan.Framework.Domain.Rules;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Rules;

/// <summary>
/// 业务规则扩展方法测试
/// </summary>
/// <remarks>
/// 单条 CheckRule 与批量 CheckRules 的失败语义不同：前者抛出携带规则实例的异常，
/// 后者把所有违反项用「; 」拼成一条消息，且不携带规则实例。
/// </remarks>
public class BusinessRuleExtensionsTests
{
    /// <summary>
    /// 规则未被违反时检查静默通过
    /// </summary>
    [Fact]
    public void CheckRule_WhenRuleIsSatisfied_DoesNotThrow()
    {
        IBusinessRule rule = new SampleBusinessRule("ok", false);

        rule.CheckRule();
    }

    /// <summary>
    /// 规则被违反时抛出携带规则实例的异常
    /// </summary>
    [Fact]
    public void CheckRule_WhenRuleIsBroken_ThrowsWithRule()
    {
        IBusinessRule rule = new SampleBusinessRule("坏了", true);

        var exception = Assert.Throws<BusinessRuleValidationException>(rule.CheckRule);

        Assert.Equal("坏了", exception.Message);
        Assert.Same(rule, exception.BrokenRule);
    }

    /// <summary>
    /// 规则为空时抛出参数异常
    /// </summary>
    [Fact]
    public void CheckRule_WhenRuleIsNull_Throws()
    {
        IBusinessRule? rule = null;

        Assert.Throws<ArgumentNullException>(() => rule!.CheckRule());
    }

    /// <summary>
    /// 批量检查全部通过时静默返回
    /// </summary>
    [Fact]
    public void CheckRules_WhenAllSatisfied_DoesNotThrow()
    {
        var rules = new List<IBusinessRule>
        {
            new SampleBusinessRule("a", false),
            new SampleBusinessRule("b", false)
        };

        rules.CheckRules();
    }

    /// <summary>
    /// 批量检查把所有违反项合并为一条消息
    /// </summary>
    [Fact]
    public void CheckRules_WhenMultipleBroken_CombinesMessages()
    {
        var rules = new List<IBusinessRule>
        {
            new SampleBusinessRule("坏了一", true),
            new SampleBusinessRule("ok", false),
            new SampleBusinessRule("坏了二", true)
        };

        var exception = Assert.Throws<BusinessRuleValidationException>(rules.CheckRules);

        Assert.Equal("坏了一; 坏了二", exception.Message);
        Assert.Null(exception.BrokenRule);
    }

    /// <summary>
    /// 批量检查不快速失败，所有规则都会被求值
    /// </summary>
    [Fact]
    public void CheckRules_EvaluatesEveryRule()
    {
        var first = new SampleBusinessRule("坏了一", true);
        var second = new SampleBusinessRule("坏了二", true);
        var rules = new List<IBusinessRule> { first, second };

        Assert.Throws<BusinessRuleValidationException>(rules.CheckRules);

        Assert.Equal(1, first.CheckedCount);
        Assert.Equal(1, second.CheckedCount);
    }

    /// <summary>
    /// 空规则集合不触发异常
    /// </summary>
    [Fact]
    public void CheckRules_WithEmptyCollection_DoesNotThrow()
    {
        var rules = new List<IBusinessRule>();

        rules.CheckRules();
    }

    /// <summary>
    /// 规则集合为空引用时抛出参数异常
    /// </summary>
    [Fact]
    public void CheckRules_WhenCollectionIsNull_Throws()
    {
        IEnumerable<IBusinessRule>? rules = null;

        Assert.Throws<ArgumentNullException>(() => rules!.CheckRules());
    }

    /// <summary>
    /// 异步检查通过时正常完成
    /// </summary>
    [Fact]
    public async Task CheckRuleAsync_WhenRuleIsSatisfied_Completes()
    {
        IBusinessRule rule = new SampleBusinessRule("ok", false);

        await rule.CheckRuleAsync();

        Assert.Equal(1, ((SampleBusinessRule)rule).CheckedCount);
    }

    /// <summary>
    /// 异步检查被违反时把异常原样透传
    /// </summary>
    [Fact]
    public async Task CheckRuleAsync_WhenRuleIsBroken_Throws()
    {
        IBusinessRule rule = new SampleBusinessRule("坏了", true);

        var exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(() => rule.CheckRuleAsync());

        Assert.Equal("坏了", exception.Message);
    }

    /// <summary>
    /// 异步批量检查把合并消息原样透传
    /// </summary>
    [Fact]
    public async Task CheckRulesAsync_WhenBroken_ThrowsCombinedMessage()
    {
        var rules = new List<IBusinessRule>
        {
            new SampleBusinessRule("坏了一", true),
            new SampleBusinessRule("坏了二", true)
        };

        var exception = await Assert.ThrowsAsync<BusinessRuleValidationException>(() => rules.CheckRulesAsync());

        Assert.Equal("坏了一; 坏了二", exception.Message);
    }

    /// <summary>
    /// 验证通过的规则返回成功结果
    /// </summary>
    [Fact]
    public void Validate_WhenRuleIsSatisfied_ReturnsSuccess()
    {
        IBusinessRule rule = new SampleBusinessRule("ok", false);

        var result = rule.Validate();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// 验证被违反的规则返回带消息的失败结果
    /// </summary>
    [Fact]
    public void Validate_WhenRuleIsBroken_ReturnsFailureWithMessage()
    {
        IBusinessRule rule = new SampleBusinessRule("坏了", true);

        var result = rule.Validate();

        Assert.False(result.IsValid);
        Assert.Equal("坏了", Assert.Single(result.Errors));
    }

    /// <summary>
    /// 规则为空时验证抛出参数异常
    /// </summary>
    [Fact]
    public void Validate_WhenRuleIsNull_Throws()
    {
        IBusinessRule? rule = null;

        Assert.Throws<ArgumentNullException>(() => { _ = rule!.Validate(); });
    }

    /// <summary>
    /// 批量验证收集全部违反项而不抛异常
    /// </summary>
    [Fact]
    public void ValidateAll_WhenSomeBroken_CollectsEveryError()
    {
        var rules = new List<IBusinessRule>
        {
            new SampleBusinessRule("坏了一", true),
            new SampleBusinessRule("ok", false),
            new SampleBusinessRule("坏了二", true)
        };

        var result = rules.ValidateAll();

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains("坏了一", result.Errors);
        Assert.Contains("坏了二", result.Errors);
    }

    /// <summary>
    /// 批量验证全部通过时返回成功结果
    /// </summary>
    [Fact]
    public void ValidateAll_WhenAllSatisfied_ReturnsSuccess()
    {
        var rules = new List<IBusinessRule>
        {
            new SampleBusinessRule("a", false),
            new SampleBusinessRule("b", false)
        };

        var result = rules.ValidateAll();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// 空集合的批量验证结果为成功
    /// </summary>
    [Fact]
    public void ValidateAll_WithEmptyCollection_ReturnsSuccess()
    {
        var rules = new List<IBusinessRule>();

        Assert.True(rules.ValidateAll().IsValid);
    }

    /// <summary>
    /// 规则集合为空引用时批量验证抛出参数异常
    /// </summary>
    [Fact]
    public void ValidateAll_WhenCollectionIsNull_Throws()
    {
        IEnumerable<IBusinessRule>? rules = null;

        Assert.Throws<ArgumentNullException>(() => { _ = rules!.ValidateAll(); });
    }
}
