// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Exceptions;
using XiHan.Framework.Domain.Extensions;
using XiHan.Framework.Domain.Rules;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Extensions;

/// <summary>
/// 实体扩展方法测试
/// </summary>
/// <remarks>
/// CheckRules 是「遇到第一条违反就抛」的快速失败语义，ValidateRules 则收集全部违反项，
/// 两者不能混淆——前者用于写入前的守卫，后者用于给用户一次性展示所有问题。
/// </remarks>
public class EntityExtensionsTests
{
    /// <summary>
    /// 规则未被违反时检查静默通过
    /// </summary>
    [Fact]
    public void CheckRule_WhenRuleIsSatisfied_DoesNotThrow()
    {
        var entity = new SampleEntity(1);
        var rule = new SampleBusinessRule("余额不足", false);

        entity.CheckRule(rule);

        Assert.Equal(1, rule.CheckedCount);
    }

    /// <summary>
    /// 规则被违反时抛出业务规则异常并携带规则实例
    /// </summary>
    [Fact]
    public void CheckRule_WhenRuleIsBroken_ThrowsWithRule()
    {
        var entity = new SampleEntity(1);
        var rule = new SampleBusinessRule("余额不足", true);

        var exception = Assert.Throws<BusinessRuleValidationException>(() => entity.CheckRule(rule));

        Assert.Equal("余额不足", exception.Message);
        Assert.Same(rule, exception.BrokenRule);
    }

    /// <summary>
    /// 批量检查在第一条被违反的规则处快速失败
    /// </summary>
    [Fact]
    public void CheckRules_WithParams_FailsFastOnFirstBrokenRule()
    {
        var entity = new SampleEntity(1);
        var satisfied = new SampleBusinessRule("ok", false);
        var broken = new SampleBusinessRule("坏了", true);
        var never = new SampleBusinessRule("不该被检查", true);

        var exception = Assert.Throws<BusinessRuleValidationException>(() => entity.CheckRules(satisfied, broken, never));

        Assert.Equal("坏了", exception.Message);
        Assert.Equal(1, satisfied.CheckedCount);
        Assert.Equal(1, broken.CheckedCount);
        Assert.Equal(0, never.CheckedCount);
    }

    /// <summary>
    /// 批量检查全部通过时静默返回
    /// </summary>
    [Fact]
    public void CheckRules_WhenAllSatisfied_DoesNotThrow()
    {
        var entity = new SampleEntity(1);
        var first = new SampleBusinessRule("a", false);
        var second = new SampleBusinessRule("b", false);

        entity.CheckRules(first, second);

        Assert.Equal(1, first.CheckedCount);
        Assert.Equal(1, second.CheckedCount);
    }

    /// <summary>
    /// 集合重载与可变参数重载语义一致
    /// </summary>
    [Fact]
    public void CheckRules_WithEnumerable_BehavesLikeParamsOverload()
    {
        var entity = new SampleEntity(1);
        var rules = new List<IBusinessRule>
        {
            new SampleBusinessRule("a", false),
            new SampleBusinessRule("坏了", true)
        };

        var exception = Assert.Throws<BusinessRuleValidationException>(() => entity.CheckRules(rules));

        Assert.Equal("坏了", exception.Message);
    }

    /// <summary>
    /// 空规则集合不触发任何异常
    /// </summary>
    [Fact]
    public void CheckRules_WithEmptyCollection_DoesNotThrow()
    {
        var entity = new SampleEntity(1);
        var rules = new List<IBusinessRule>();

        entity.CheckRules(rules);
    }

    /// <summary>
    /// 尝试检查返回布尔结果而不抛异常
    /// </summary>
    [Fact]
    public void TryCheckRule_ReturnsResultWithoutThrowing()
    {
        var entity = new SampleEntity(1);

        Assert.True(entity.TryCheckRule(new SampleBusinessRule("ok", false)));
        Assert.False(entity.TryCheckRule(new SampleBusinessRule("坏了", true)));
    }

    /// <summary>
    /// 验证规则收集全部被违反的规则而不快速失败
    /// </summary>
    [Fact]
    public void ValidateRules_CollectsEveryBrokenRule()
    {
        var entity = new SampleEntity(1);
        var satisfied = new SampleBusinessRule("ok", false);
        var firstBroken = new SampleBusinessRule("坏了一", true);
        var secondBroken = new SampleBusinessRule("坏了二", true);

        var broken = entity.ValidateRules(satisfied, firstBroken, secondBroken).ToList();

        Assert.Equal(2, broken.Count);
        Assert.Same(firstBroken, broken[0]);
        Assert.Same(secondBroken, broken[1]);
    }

    /// <summary>
    /// 全部通过时验证结果为空
    /// </summary>
    [Fact]
    public void ValidateRules_WhenAllSatisfied_ReturnsEmpty()
    {
        var entity = new SampleEntity(1);

        var broken = entity.ValidateRules(new SampleBusinessRule("a", false), new SampleBusinessRule("b", false));

        Assert.Empty(broken);
    }

    /// <summary>
    /// 集合重载的验证结果与可变参数重载一致
    /// </summary>
    [Fact]
    public void ValidateRules_WithEnumerable_CollectsBrokenRules()
    {
        var entity = new SampleEntity(1);
        var brokenRule = new SampleBusinessRule("坏了", true);
        var rules = new List<IBusinessRule>
        {
            new SampleBusinessRule("ok", false),
            brokenRule
        };

        var broken = entity.ValidateRules(rules).ToList();

        Assert.Same(brokenRule, Assert.Single(broken));
    }
}
