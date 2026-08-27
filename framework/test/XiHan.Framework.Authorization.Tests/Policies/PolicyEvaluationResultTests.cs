// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Policies;

namespace XiHan.Framework.Authorization.Tests.Policies;

/// <summary>
/// 策略评估结果测试
/// </summary>
/// <remarks>
/// 授权服务会直接把这里的失败要求列表搬进授权结果，因此任何分支下它都不能是 null。
/// </remarks>
public class PolicyEvaluationResultTests
{
    /// <summary>
    /// 新建实例默认失败且失败要求为空列表
    /// </summary>
    [Fact]
    public void New_ByDefault_IsNotSucceeded()
    {
        var result = new PolicyEvaluationResult();

        Assert.False(result.Succeeded);
        Assert.Null(result.FailureReason);
        Assert.NotNull(result.FailedRequirements);
        Assert.Empty(result.FailedRequirements);
        Assert.Null(result.AdditionalData);
    }

    /// <summary>
    /// 成功结果不带失败信息
    /// </summary>
    [Fact]
    public void Success_HasNoFailureInfo()
    {
        var result = PolicyEvaluationResult.Success();

        Assert.True(result.Succeeded);
        Assert.Null(result.FailureReason);
        Assert.Empty(result.FailedRequirements);
    }

    /// <summary>
    /// 失败结果未传要求列表时退化为空列表
    /// </summary>
    [Fact]
    public void Failure_WithoutRequirements_UsesEmptyList()
    {
        var result = PolicyEvaluationResult.Failure("策略不存在");

        Assert.False(result.Succeeded);
        Assert.Equal("策略不存在", result.FailureReason);
        Assert.Empty(result.FailedRequirements);
    }

    /// <summary>
    /// 失败结果保留传入的要求列表
    /// </summary>
    [Fact]
    public void Failure_WithRequirements_KeepsThem()
    {
        var result = PolicyEvaluationResult.Failure("评估失败", ["缺少权限: read"]);

        Assert.Equal("缺少权限: read", Assert.Single(result.FailedRequirements));
    }

    /// <summary>
    /// 两次调用工厂返回互相独立的实例
    /// </summary>
    [Fact]
    public void Failure_CalledTwice_ReturnsIndependentInstances()
    {
        var first = PolicyEvaluationResult.Failure("一");
        var second = PolicyEvaluationResult.Failure("二");

        first.FailedRequirements.Add("x");

        Assert.Empty(second.FailedRequirements);
    }
}
