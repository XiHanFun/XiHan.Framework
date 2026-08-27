// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Policies;
using XiHan.Framework.Authorization.Roles;
using XiHan.Framework.Authorization.Tests.Infrastructure;

namespace XiHan.Framework.Authorization.Tests;

/// <summary>
/// 默认授权服务测试
/// </summary>
/// <remarks>
/// 授权服务本身不做判定，只做“判定结果 → 授权结果”的翻译与写操作的异常兜底，
/// 因此这里把检查器、存储、策略评估器全部换成替身，专测翻译规则和 try/catch 分支。
/// </remarks>
public class DefaultAuthorizationServiceTests
{
    /// <summary>
    /// 有权限时返回成功
    /// </summary>
    [Fact]
    public async Task AuthorizeAsync_WhenGranted_Succeeds()
    {
        var checker = new FakePermissionChecker("Sys.User.Create");
        var service = CreateService(checker, out _, out _, out _);

        var result = await service.AuthorizeAsync("u1", "Sys.User.Create", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Null(result.FailureReason);
    }

    /// <summary>
    /// 无权限时返回权限不足结果，并把权限名写进失败要求
    /// </summary>
    [Fact]
    public async Task AuthorizeAsync_WhenNotGranted_ReturnsPermissionDenied()
    {
        var checker = new FakePermissionChecker();
        var service = CreateService(checker, out _, out _, out _);

        var result = await service.AuthorizeAsync("u1", "Sys.User.Create", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("缺少权限: Sys.User.Create", result.FailureReason);
        Assert.Equal("Sys.User.Create", Assert.Single(result.FailedRequirements));
    }

    /// <summary>
    /// 策略通过时返回成功，且资源对象原样透传给策略评估器
    /// </summary>
    [Fact]
    public async Task AuthorizePolicyAsync_WhenPolicyPasses_SucceedsAndForwardsResource()
    {
        var service = CreateService(new FakePermissionChecker(), out _, out _, out var policyEvaluator);
        var resource = new object();

        var result = await service.AuthorizePolicyAsync("u1", "p1", resource, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("p1", policyEvaluator.LastPolicyName);
        Assert.Same(resource, policyEvaluator.LastResource);
    }

    /// <summary>
    /// 策略失败时原因与失败要求逐条转写
    /// </summary>
    [Fact]
    public async Task AuthorizePolicyAsync_WhenPolicyFails_CopiesReasonAndRequirements()
    {
        var service = CreateService(new FakePermissionChecker(), out _, out _, out var policyEvaluator);
        policyEvaluator.Result = PolicyEvaluationResult.Failure("策略 'p1' 评估失败", ["缺少权限: a"]);

        var result = await service.AuthorizePolicyAsync("u1", "p1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("策略 'p1' 评估失败", result.FailureReason);
        Assert.Equal("缺少权限: a", Assert.Single(result.FailedRequirements));
    }

    /// <summary>
    /// 策略失败但未给出原因时使用兜底文案，避免把 null 透出到调用方
    /// </summary>
    [Fact]
    public async Task AuthorizePolicyAsync_WhenReasonMissing_UsesFallbackReason()
    {
        var service = CreateService(new FakePermissionChecker(), out _, out _, out var policyEvaluator);
        policyEvaluator.Result = new PolicyEvaluationResult { Succeeded = false };

        var result = await service.AuthorizePolicyAsync("u1", "p1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("策略评估失败", result.FailureReason);
    }

    /// <summary>
    /// 在角色中返回成功，不在角色中返回角色不足结果
    /// </summary>
    [Fact]
    public async Task AuthorizeRoleAsync_ReflectsRoleMembership()
    {
        var token = TestContext.Current.CancellationToken;
        var service = CreateService(new FakePermissionChecker(), out _, out var roleStore, out _);
        await roleStore.Inner.CreateRoleAsync(new RoleDefinition("r1", "admin", "管理员"), token);
        await roleStore.Inner.AddUserToRoleAsync("u1", "admin", token);

        Assert.True((await service.AuthorizeRoleAsync("u1", "admin", token)).Succeeded);

        var denied = await service.AuthorizeRoleAsync("u2", "admin", token);
        Assert.False(denied.Succeeded);
        Assert.Equal("不在角色中: admin", denied.FailureReason);
    }

    /// <summary>
    /// 任意权限满足其一即成功，一个都没有则把全部候选写进失败要求
    /// </summary>
    [Fact]
    public async Task AuthorizeAnyAsync_ReflectsAnyGranted()
    {
        var token = TestContext.Current.CancellationToken;
        var service = CreateService(new FakePermissionChecker("b"), out _, out _, out _);

        Assert.True((await service.AuthorizeAnyAsync("u1", ["a", "b"], token)).Succeeded);

        var denied = await service.AuthorizeAnyAsync("u1", ["a", "c"], token);
        Assert.False(denied.Succeeded);
        Assert.Equal("缺少所需权限（任意一个）", denied.FailureReason);
        Assert.Equal(2, denied.FailedRequirements.Count);
    }

    /// <summary>
    /// 全部权限缺一不可
    /// </summary>
    [Fact]
    public async Task AuthorizeAllAsync_ReflectsAllGranted()
    {
        var token = TestContext.Current.CancellationToken;
        var service = CreateService(new FakePermissionChecker("a", "b"), out _, out _, out _);

        Assert.True((await service.AuthorizeAllAsync("u1", ["a", "b"], token)).Succeeded);

        var denied = await service.AuthorizeAllAsync("u1", ["a", "c"], token);
        Assert.False(denied.Succeeded);
        Assert.Equal("缺少所需权限（全部）", denied.FailureReason);
    }

    /// <summary>
    /// 用户权限直接取自权限检查器
    /// </summary>
    [Fact]
    public async Task GetUserPermissionsAsync_ReturnsCheckerResult()
    {
        var service = CreateService(new FakePermissionChecker("a", "b"), out _, out _, out _);

        var permissions = await service.GetUserPermissionsAsync("u1", TestContext.Current.CancellationToken);

        Assert.Equal(2, permissions.Count);
        Assert.Contains("a", permissions);
        Assert.Contains("b", permissions);
    }

    /// <summary>
    /// 用户角色只返回角色名称而不是完整定义
    /// </summary>
    [Fact]
    public async Task GetUserRolesAsync_ProjectsRoleNames()
    {
        var token = TestContext.Current.CancellationToken;
        var service = CreateService(new FakePermissionChecker(), out _, out var roleStore, out _);
        await roleStore.Inner.CreateRoleAsync(new RoleDefinition("r1", "admin", "管理员"), token);
        await roleStore.Inner.AddUserToRoleAsync("u1", "admin", token);

        var roles = await service.GetUserRolesAsync("u1", token);

        Assert.Equal("admin", Assert.Single(roles));
    }

    /// <summary>
    /// 授予未定义的权限直接失败，不落存储
    /// </summary>
    [Fact]
    public async Task GrantPermissionAsync_WhenPermissionUndefined_Fails()
    {
        var token = TestContext.Current.CancellationToken;
        var checker = new FakePermissionChecker();
        var service = CreateService(checker, out var permissionStore, out _, out _);

        var result = await service.GrantPermissionAsync("u1", "Sys.User.Create", token);

        Assert.False(result.Succeeded);
        Assert.Equal("权限不存在: Sys.User.Create", result.FailureReason);
        Assert.Empty(await permissionStore.Inner.GetUserPermissionNamesAsync("u1"));
    }

    /// <summary>
    /// 授予已定义的权限会真正写入存储
    /// </summary>
    [Fact]
    public async Task GrantPermissionAsync_WhenPermissionDefined_WritesToStore()
    {
        var token = TestContext.Current.CancellationToken;
        var checker = new FakePermissionChecker();
        checker.ExistingPermissions.Add("Sys.User.Create");
        var service = CreateService(checker, out var permissionStore, out _, out _);

        var result = await service.GrantPermissionAsync("u1", "Sys.User.Create", token);

        Assert.True(result.Succeeded);
        Assert.Contains("Sys.User.Create", await permissionStore.Inner.GetUserPermissionNamesAsync("u1"));
    }

    /// <summary>
    /// 存储写入异常被吞成失败结果而不是向上抛
    /// </summary>
    [Fact]
    public async Task GrantPermissionAsync_WhenStoreThrows_ReturnsFailure()
    {
        var token = TestContext.Current.CancellationToken;
        var checker = new FakePermissionChecker();
        checker.ExistingPermissions.Add("Sys.User.Create");
        var service = CreateService(checker, out var permissionStore, out _, out _);
        permissionStore.ThrowOnWrite = true;

        var result = await service.GrantPermissionAsync("u1", "Sys.User.Create", token);

        Assert.False(result.Succeeded);
        Assert.StartsWith("授予权限失败: ", result.FailureReason);
    }

    /// <summary>
    /// 撤销权限不校验权限是否存在，直接落存储
    /// </summary>
    [Fact]
    public async Task RevokePermissionAsync_RemovesFromStore()
    {
        var token = TestContext.Current.CancellationToken;
        var service = CreateService(new FakePermissionChecker(), out var permissionStore, out _, out _);
        await permissionStore.Inner.GrantPermissionToUserAsync("u1", "Sys.User.Create", token);

        var result = await service.RevokePermissionAsync("u1", "Sys.User.Create", token);

        Assert.True(result.Succeeded);
        Assert.Empty(await permissionStore.Inner.GetUserPermissionNamesAsync("u1"));
    }

    /// <summary>
    /// 撤销权限时的存储异常被吞成失败结果
    /// </summary>
    [Fact]
    public async Task RevokePermissionAsync_WhenStoreThrows_ReturnsFailure()
    {
        var service = CreateService(new FakePermissionChecker(), out var permissionStore, out _, out _);
        permissionStore.ThrowOnWrite = true;

        var result = await service.RevokePermissionAsync("u1", "Sys.User.Create", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.StartsWith("撤销权限失败: ", result.FailureReason);
    }

    /// <summary>
    /// 角色不存在时先于写入失败，文案区分于写入异常
    /// </summary>
    [Fact]
    public async Task AddUserToRoleAsync_WhenRoleMissing_Fails()
    {
        var service = CreateService(new FakePermissionChecker(), out _, out _, out _);

        var result = await service.AddUserToRoleAsync("u1", "admin", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("角色不存在: admin", result.FailureReason);
    }

    /// <summary>
    /// 角色存在时写入用户角色关系
    /// </summary>
    [Fact]
    public async Task AddUserToRoleAsync_WhenRoleExists_WritesMembership()
    {
        var token = TestContext.Current.CancellationToken;
        var service = CreateService(new FakePermissionChecker(), out _, out var roleStore, out _);
        await roleStore.Inner.CreateRoleAsync(new RoleDefinition("r1", "admin", "管理员"), token);

        var result = await service.AddUserToRoleAsync("u1", "admin", token);

        Assert.True(result.Succeeded);
        Assert.True(await roleStore.Inner.IsInRoleAsync("u1", "admin", token));
    }

    /// <summary>
    /// 角色存在但写入抛异常时走异常分支，文案与角色不存在区分
    /// </summary>
    [Fact]
    public async Task AddUserToRoleAsync_WhenStoreThrows_ReturnsFailure()
    {
        var token = TestContext.Current.CancellationToken;
        var service = CreateService(new FakePermissionChecker(), out _, out var roleStore, out _);
        await roleStore.Inner.CreateRoleAsync(new RoleDefinition("r1", "admin", "管理员"), token);
        roleStore.ThrowOnMembershipWrite = true;

        var result = await service.AddUserToRoleAsync("u1", "admin", token);

        Assert.False(result.Succeeded);
        Assert.StartsWith("添加用户到角色失败: ", result.FailureReason);
    }

    /// <summary>
    /// 移除用户角色成功后关系消失
    /// </summary>
    [Fact]
    public async Task RemoveUserFromRoleAsync_RemovesMembership()
    {
        var token = TestContext.Current.CancellationToken;
        var service = CreateService(new FakePermissionChecker(), out _, out var roleStore, out _);
        await roleStore.Inner.CreateRoleAsync(new RoleDefinition("r1", "admin", "管理员"), token);
        await roleStore.Inner.AddUserToRoleAsync("u1", "admin", token);

        var result = await service.RemoveUserFromRoleAsync("u1", "admin", token);

        Assert.True(result.Succeeded);
        Assert.False(await roleStore.Inner.IsInRoleAsync("u1", "admin", token));
    }

    /// <summary>
    /// 移除用户角色时的存储异常被吞成失败结果
    /// </summary>
    [Fact]
    public async Task RemoveUserFromRoleAsync_WhenStoreThrows_ReturnsFailure()
    {
        var service = CreateService(new FakePermissionChecker(), out _, out var roleStore, out _);
        roleStore.ThrowOnMembershipWrite = true;

        var result = await service.RemoveUserFromRoleAsync("u1", "admin", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.StartsWith("从角色中移除用户失败: ", result.FailureReason);
    }

    private static DefaultAuthorizationService CreateService(
        FakePermissionChecker checker,
        out FaultInjectingPermissionStore permissionStore,
        out FaultInjectingRoleStore roleStore,
        out FakePolicyEvaluator policyEvaluator)
    {
        permissionStore = new FaultInjectingPermissionStore();
        roleStore = new FaultInjectingRoleStore();
        policyEvaluator = new FakePolicyEvaluator();
        return new DefaultAuthorizationService(checker, permissionStore, roleStore, policyEvaluator);
    }
}
