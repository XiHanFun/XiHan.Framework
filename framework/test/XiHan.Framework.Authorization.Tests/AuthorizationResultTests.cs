// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Tests;

/// <summary>
/// 授权结果测试
/// </summary>
/// <remarks>
/// 授权结果是整个授权门面对外的唯一返回契约，四个工厂方法各自的默认值语义必须锁死：
/// 失败原因文案会被上层直接透出，失败要求列表在任何分支下都不能为 null。
/// </remarks>
public class AuthorizationResultTests
{
    /// <summary>
    /// 新建实例默认是未授权且失败要求列表非空引用
    /// </summary>
    [Fact]
    public void New_ByDefault_IsNotSucceededWithEmptyRequirements()
    {
        var result = new AuthorizationResult();

        Assert.False(result.Succeeded);
        Assert.Null(result.FailureReason);
        Assert.NotNull(result.FailedRequirements);
        Assert.Empty(result.FailedRequirements);
        Assert.Null(result.AdditionalData);
    }

    /// <summary>
    /// 成功结果不带失败原因与失败要求
    /// </summary>
    [Fact]
    public void Success_ReturnsSucceededWithoutFailureInfo()
    {
        var result = AuthorizationResult.Success();

        Assert.True(result.Succeeded);
        Assert.Null(result.FailureReason);
        Assert.Empty(result.FailedRequirements);
    }

    /// <summary>
    /// 失败结果未传要求列表时退化为空列表而不是 null
    /// </summary>
    [Fact]
    public void Failure_WithoutRequirements_UsesEmptyList()
    {
        var result = AuthorizationResult.Failure("越权");

        Assert.False(result.Succeeded);
        Assert.Equal("越权", result.FailureReason);
        Assert.NotNull(result.FailedRequirements);
        Assert.Empty(result.FailedRequirements);
    }

    /// <summary>
    /// 失败结果保留传入的失败要求列表
    /// </summary>
    [Fact]
    public void Failure_WithRequirements_KeepsThem()
    {
        var result = AuthorizationResult.Failure("越权", ["a", "b"]);

        Assert.False(result.Succeeded);
        Assert.Equal(new[] { "a", "b" }, result.FailedRequirements);
    }

    /// <summary>
    /// 权限不足结果把权限名同时写进原因和失败要求
    /// </summary>
    [Fact]
    public void PermissionDenied_PutsPermissionNameInReasonAndRequirements()
    {
        var result = AuthorizationResult.PermissionDenied("Sys.User.Create");

        Assert.False(result.Succeeded);
        Assert.Equal("缺少权限: Sys.User.Create", result.FailureReason);
        Assert.Equal("Sys.User.Create", Assert.Single(result.FailedRequirements));
    }

    /// <summary>
    /// 角色不足结果把角色名同时写进原因和失败要求
    /// </summary>
    [Fact]
    public void RoleDenied_PutsRoleNameInReasonAndRequirements()
    {
        var result = AuthorizationResult.RoleDenied("admin");

        Assert.False(result.Succeeded);
        Assert.Equal("不在角色中: admin", result.FailureReason);
        Assert.Equal("admin", Assert.Single(result.FailedRequirements));
    }

    /// <summary>
    /// 两次调用工厂方法返回互相独立的实例，失败要求列表不共享
    /// </summary>
    [Fact]
    public void Failure_CalledTwice_ReturnsIndependentInstances()
    {
        var first = AuthorizationResult.Failure("原因一");
        var second = AuthorizationResult.Failure("原因二");

        first.FailedRequirements.Add("x");

        Assert.NotSame(first, second);
        Assert.Empty(second.FailedRequirements);
    }
}
