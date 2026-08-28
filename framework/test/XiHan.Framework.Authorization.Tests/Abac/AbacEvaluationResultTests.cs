// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Abac;

namespace XiHan.Framework.Authorization.Tests.Abac;

/// <summary>
/// ABAC 评估结果测试
/// </summary>
/// <remarks>
/// 默认值必须是拒绝：这个类型会被自定义评估器用对象初始化器构造，一旦默认放行就会出现静默越权。
/// </remarks>
public class AbacEvaluationResultTests
{
    /// <summary>
    /// 新建实例默认是拒绝且无说明
    /// </summary>
    [Fact]
    public void New_ByDefault_IsDenied()
    {
        var result = new AbacEvaluationResult();

        Assert.False(result.IsAllowed);
        Assert.Null(result.Reason);
    }

    /// <summary>
    /// 放行工厂不带原因时说明为 null
    /// </summary>
    [Fact]
    public void Allow_WithoutReason_HasNullReason()
    {
        var result = AbacEvaluationResult.Allow();

        Assert.True(result.IsAllowed);
        Assert.Null(result.Reason);
    }

    /// <summary>
    /// 放行工厂保留传入的原因
    /// </summary>
    [Fact]
    public void Allow_WithReason_KeepsReason()
    {
        var result = AbacEvaluationResult.Allow("命中 allow 策略");

        Assert.True(result.IsAllowed);
        Assert.Equal("命中 allow 策略", result.Reason);
    }

    /// <summary>
    /// 拒绝工厂不带原因时说明为 null
    /// </summary>
    [Fact]
    public void Deny_WithoutReason_HasNullReason()
    {
        var result = AbacEvaluationResult.Deny();

        Assert.False(result.IsAllowed);
        Assert.Null(result.Reason);
    }

    /// <summary>
    /// 拒绝工厂保留传入的原因
    /// </summary>
    [Fact]
    public void Deny_WithReason_KeepsReason()
    {
        var result = AbacEvaluationResult.Deny("租户不匹配");

        Assert.False(result.IsAllowed);
        Assert.Equal("租户不匹配", result.Reason);
    }

    /// <summary>
    /// 工厂每次返回新实例
    /// </summary>
    [Fact]
    public void Allow_CalledTwice_ReturnsDistinctInstances()
    {
        Assert.NotSame(AbacEvaluationResult.Allow(), AbacEvaluationResult.Allow());
    }
}
