// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;
using XiHan.Framework.Authorization.Permissions;
using XiHan.Framework.Authorization.Policies;
using XiHan.Framework.Authorization.Roles;
using XiHan.Framework.Authorization.Tests.Infrastructure;

namespace XiHan.Framework.Authorization.Tests.Policies;

/// <summary>
/// 默认策略评估器测试
/// </summary>
/// <remarks>
/// 四类要求的组合语义各不相同，是最容易写反的地方：角色是“任意其一”，权限是“全部满足”，
/// 声明只认当前登录主体（未认证即全部判失败，fail-closed），自定义要求抛异常也算失败而不是向上冒泡。
/// 另外多策略评估里，EvaluateAll 不短路而 EvaluateAny 命中即停，这里用带计数的存储把差异测出来。
/// </remarks>
public class DefaultPolicyEvaluatorTests
{
    /// <summary>
    /// 用户标识为空时直接失败，不去查策略
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WithEmptyUserId_FailsWithoutQueryingStore()
    {
        var fixture = new EvaluatorFixture();

        var result = await fixture.Evaluator.EvaluateAsync(string.Empty, "p1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("用户ID不能为空", result.FailureReason);
        Assert.Empty(fixture.PolicyStore.QueriedPolicyNames);
    }

    /// <summary>
    /// 策略名称为空时直接失败
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WithEmptyPolicyName_Fails()
    {
        var fixture = new EvaluatorFixture();

        var result = await fixture.Evaluator.EvaluateAsync("u1", string.Empty, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("策略名称不能为空", result.FailureReason);
    }

    /// <summary>
    /// 策略不存在时失败，说明里回显策略名
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenPolicyMissing_Fails()
    {
        var fixture = new EvaluatorFixture();

        var result = await fixture.Evaluator.EvaluateAsync("u1", "p1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("策略 'p1' 不存在", result.FailureReason);
    }

    /// <summary>
    /// 策略被禁用时失败，且不再评估任何要求
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenPolicyDisabled_Fails()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new EvaluatorFixture();
        var requirement = new DelegateAuthorizationRequirement("never", _ => true);
        await fixture.AddPolicyAsync(new PolicyDefinition("p1", "策略一")
        {
            IsEnabled = false,
            CustomRequirements = [requirement]
        });

        var result = await fixture.Evaluator.EvaluateAsync("u1", "p1", cancellationToken: token);

        Assert.False(result.Succeeded);
        Assert.Equal("策略 'p1' 已禁用", result.FailureReason);
        Assert.Equal(0, requirement.EvaluateCount);
    }

    /// <summary>
    /// 没有任何要求的启用策略直接通过
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WithNoRequirements_Succeeds()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new EvaluatorFixture();
        await fixture.AddPolicyAsync(new PolicyDefinition("p1", "策略一"));

        var result = await fixture.Evaluator.EvaluateAsync("u1", "p1", cancellationToken: token);

        Assert.True(result.Succeeded);
        Assert.Empty(result.FailedRequirements);
    }

    /// <summary>
    /// 角色要求是“任意其一”，命中一个即算满足
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_RoleRequirement_IsAnyOf()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new EvaluatorFixture();
        await fixture.AddRoleAsync("r1", "ops");
        await fixture.AssignRoleAsync("u1", "ops");
        await fixture.AddPolicyAsync(new PolicyDefinition("p1", "策略一")
        {
            RequiredRoles = ["admin", "ops"]
        });

        var result = await fixture.Evaluator.EvaluateAsync("u1", "p1", cancellationToken: token);

        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// 一个角色都不在时失败，失败要求里列出全部候选角色
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_RoleRequirement_WhenNoneMatched_Fails()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new EvaluatorFixture();
        await fixture.AddPolicyAsync(new PolicyDefinition("p1", "策略一")
        {
            RequiredRoles = ["admin", "ops"]
        });

        var result = await fixture.Evaluator.EvaluateAsync("u1", "p1", cancellationToken: token);

        Assert.False(result.Succeeded);
        Assert.Equal("策略 'p1' 评估失败", result.FailureReason);
        Assert.Equal("需要以下角色之一: admin, ops", Assert.Single(result.FailedRequirements));
    }

    /// <summary>
    /// 权限要求是“全部满足”，缺哪个就报哪个
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_PermissionRequirement_IsAllOf()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new EvaluatorFixture();
        await fixture.DefineAndGrantAsync("u1", "read");
        await fixture.AddPolicyAsync(new PolicyDefinition("p1", "策略一")
        {
            RequiredPermissions = ["read", "write"]
        });

        var result = await fixture.Evaluator.EvaluateAsync("u1", "p1", cancellationToken: token);

        Assert.False(result.Succeeded);
        Assert.Equal("缺少权限: write", Assert.Single(result.FailedRequirements));
    }

    /// <summary>
    /// 权限齐全时通过
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_PermissionRequirement_WhenAllGranted_Succeeds()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new EvaluatorFixture();
        await fixture.DefineAndGrantAsync("u1", "read");
        await fixture.DefineAndGrantAsync("u1", "write");
        await fixture.AddPolicyAsync(new PolicyDefinition("p1", "策略一")
        {
            RequiredPermissions = ["read", "write"]
        });

        Assert.True((await fixture.Evaluator.EvaluateAsync("u1", "p1", cancellationToken: token)).Succeeded);
    }

    /// <summary>
    /// 声明要求匹配当前主体的声明，类型比较忽略大小写
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_ClaimRequirement_MatchesCurrentUserClaims()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new EvaluatorFixture(new Claim("scope", "full"));
        await fixture.AddPolicyAsync(new PolicyDefinition("p1", "策略一")
        {
            RequiredClaims = { ["Scope"] = "full" }
        });

        Assert.True((await fixture.Evaluator.EvaluateAsync("u1", "p1", cancellationToken: token)).Succeeded);
    }

    /// <summary>
    /// 声明值比较区分大小写，不能靠大小写绕过
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_ClaimRequirement_ValueIsCaseSensitive()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new EvaluatorFixture(new Claim("scope", "full"));
        await fixture.AddPolicyAsync(new PolicyDefinition("p1", "策略一")
        {
            RequiredClaims = { ["scope"] = "Full" }
        });

        var result = await fixture.Evaluator.EvaluateAsync("u1", "p1", cancellationToken: token);

        Assert.False(result.Succeeded);
        Assert.Equal("缺少声明: scope = Full", Assert.Single(result.FailedRequirements));
    }

    /// <summary>
    /// 声明要求的值为空串时只校验声明类型是否存在
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_ClaimRequirement_WithEmptyValue_ChecksTypeOnly()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new EvaluatorFixture(new Claim("scope", "anything"));
        await fixture.AddPolicyAsync(new PolicyDefinition("p1", "策略一")
        {
            RequiredClaims = { ["scope"] = string.Empty }
        });

        Assert.True((await fixture.Evaluator.EvaluateAsync("u1", "p1", cancellationToken: token)).Succeeded);
    }

    /// <summary>
    /// 当前主体没有任何声明时，所有声明要求判失败（fail-closed）
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_ClaimRequirement_WithoutAnyClaim_Fails()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new EvaluatorFixture();
        await fixture.AddPolicyAsync(new PolicyDefinition("p1", "策略一")
        {
            RequiredClaims = { ["scope"] = "full" }
        });

        Assert.False((await fixture.Evaluator.EvaluateAsync("u1", "p1", cancellationToken: token)).Succeeded);
    }

    /// <summary>
    /// 自定义要求返回假时失败，说明里带上要求名
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_CustomRequirement_WhenRejected_Fails()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new EvaluatorFixture();
        await fixture.AddPolicyAsync(new PolicyDefinition("p1", "策略一")
        {
            CustomRequirements = [new DelegateAuthorizationRequirement("business-hours", _ => false)]
        });

        var result = await fixture.Evaluator.EvaluateAsync("u1", "p1", cancellationToken: token);

        Assert.False(result.Succeeded);
        Assert.Equal("自定义要求未通过: business-hours", Assert.Single(result.FailedRequirements));
    }

    /// <summary>
    /// 自定义要求抛异常时转成失败要求，不向上冒泡
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_CustomRequirement_WhenThrows_IsCapturedAsFailure()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new EvaluatorFixture();
        await fixture.AddPolicyAsync(new PolicyDefinition("p1", "策略一")
        {
            CustomRequirements =
            [
                new DelegateAuthorizationRequirement("boom", _ => throw new InvalidOperationException("外部服务不可用"))
            ]
        });

        var result = await fixture.Evaluator.EvaluateAsync("u1", "p1", cancellationToken: token);

        Assert.False(result.Succeeded);
        Assert.Equal("自定义要求评估异常: boom - 外部服务不可用", Assert.Single(result.FailedRequirements));
    }

    /// <summary>
    /// 自定义要求拿到的上下文里带齐用户、策略、资源、角色、权限与声明
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_CustomRequirement_ReceivesPopulatedContext()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new EvaluatorFixture(new Claim("scope", "full"));
        await fixture.AddRoleAsync("r1", "ops");
        await fixture.AssignRoleAsync("u1", "ops");
        await fixture.DefineAndGrantAsync("u1", "read");
        var requirement = new DelegateAuthorizationRequirement("capture", _ => true);
        await fixture.AddPolicyAsync(new PolicyDefinition("p1", "策略一")
        {
            CustomRequirements = [requirement]
        });
        var resource = new object();

        await fixture.Evaluator.EvaluateAsync("u1", "p1", resource, token);

        var context = requirement.CapturedContext;
        Assert.NotNull(context);
        Assert.Equal("u1", context!.UserId);
        Assert.Equal("p1", context.PolicyName);
        Assert.Same(resource, context.Resource);
        Assert.Equal("ops", Assert.Single(context.UserRoles));
        Assert.Equal("read", Assert.Single(context.UserPermissions));
        Assert.Equal("full", context.UserClaims["scope"]);
    }

    /// <summary>
    /// 多类要求同时不满足时，失败要求逐条累加
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_AccumulatesAllFailedRequirements()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new EvaluatorFixture();
        await fixture.AddPolicyAsync(new PolicyDefinition("p1", "策略一")
        {
            RequiredRoles = ["admin"],
            RequiredPermissions = ["read", "write"],
            RequiredClaims = { ["scope"] = "full" },
            CustomRequirements = [new DelegateAuthorizationRequirement("custom", _ => false)]
        });

        var result = await fixture.Evaluator.EvaluateAsync("u1", "p1", cancellationToken: token);

        Assert.False(result.Succeeded);
        Assert.Equal(5, result.FailedRequirements.Count);
    }

    /// <summary>
    /// 多策略全通过模式下策略名列表为空时失败
    /// </summary>
    [Fact]
    public async Task EvaluateAllAsync_WithEmptyList_Fails()
    {
        var fixture = new EvaluatorFixture();

        var result = await fixture.Evaluator.EvaluateAllAsync("u1", [], cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("策略名称列表不能为空", result.FailureReason);
    }

    /// <summary>
    /// 多策略全通过模式不短路：即使前面已经失败，后面的策略仍会被评估
    /// </summary>
    [Fact]
    public async Task EvaluateAllAsync_DoesNotShortCircuitOnFailure()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new EvaluatorFixture();
        await fixture.AddPolicyAsync(new PolicyDefinition("p1", "策略一") { RequiredRoles = ["admin"] });
        await fixture.AddPolicyAsync(new PolicyDefinition("p2", "策略二") { RequiredRoles = ["ops"] });

        var result = await fixture.Evaluator.EvaluateAllAsync("u1", ["p1", "p2"], cancellationToken: token);

        Assert.False(result.Succeeded);
        Assert.Equal("部分策略评估失败", result.FailureReason);
        Assert.Equal(2, result.FailedRequirements.Count);
        Assert.Contains("p2", fixture.PolicyStore.QueriedPolicyNames);
    }

    /// <summary>
    /// 多策略全通过模式下全部满足才成功
    /// </summary>
    [Fact]
    public async Task EvaluateAllAsync_WhenAllPass_Succeeds()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new EvaluatorFixture();
        await fixture.AddPolicyAsync(new PolicyDefinition("p1", "策略一"));
        await fixture.AddPolicyAsync(new PolicyDefinition("p2", "策略二"));

        Assert.True((await fixture.Evaluator.EvaluateAllAsync("u1", ["p1", "p2"], cancellationToken: token)).Succeeded);
    }

    /// <summary>
    /// 多策略任意通过模式下策略名列表为空时失败
    /// </summary>
    [Fact]
    public async Task EvaluateAnyAsync_WithEmptyList_Fails()
    {
        var fixture = new EvaluatorFixture();

        var result = await fixture.Evaluator.EvaluateAnyAsync("u1", [], cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("策略名称列表不能为空", result.FailureReason);
    }

    /// <summary>
    /// 多策略任意通过模式命中即停，后续策略不再查询
    /// </summary>
    [Fact]
    public async Task EvaluateAnyAsync_ShortCircuitsOnFirstSuccess()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new EvaluatorFixture();
        await fixture.AddPolicyAsync(new PolicyDefinition("p1", "策略一"));
        await fixture.AddPolicyAsync(new PolicyDefinition("p2", "策略二"));
        fixture.PolicyStore.QueriedPolicyNames.Clear();

        var result = await fixture.Evaluator.EvaluateAnyAsync("u1", ["p1", "p2"], cancellationToken: token);

        Assert.True(result.Succeeded);
        Assert.Equal("p1", Assert.Single(fixture.PolicyStore.QueriedPolicyNames));
    }

    /// <summary>
    /// 多策略任意通过模式全部失败时汇总所有失败要求
    /// </summary>
    [Fact]
    public async Task EvaluateAnyAsync_WhenAllFail_AggregatesFailures()
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = new EvaluatorFixture();
        await fixture.AddPolicyAsync(new PolicyDefinition("p1", "策略一") { RequiredRoles = ["admin"] });
        await fixture.AddPolicyAsync(new PolicyDefinition("p2", "策略二") { RequiredRoles = ["ops"] });

        var result = await fixture.Evaluator.EvaluateAnyAsync("u1", ["p1", "p2"], cancellationToken: token);

        Assert.False(result.Succeeded);
        Assert.Equal("所有策略评估都失败", result.FailureReason);
        Assert.Equal(2, result.FailedRequirements.Count);
    }

    /// <summary>
    /// 策略评估所需的一整套内存依赖
    /// </summary>
    private sealed class EvaluatorFixture
    {
        public EvaluatorFixture(params Claim[] claims)
        {
            PolicyStore = new CountingPolicyStore();
            PermissionStore = new DefaultPermissionStore();
            RoleStore = new DefaultRoleStore();
            Evaluator = new DefaultPolicyEvaluator(
                PolicyStore,
                new DefaultPermissionChecker(PermissionStore, RoleStore),
                RoleStore,
                new FakeCurrentUser(claims));
        }

        public CountingPolicyStore PolicyStore { get; }

        public DefaultPermissionStore PermissionStore { get; }

        public DefaultRoleStore RoleStore { get; }

        public DefaultPolicyEvaluator Evaluator { get; }

        public Task AddPolicyAsync(PolicyDefinition policy)
        {
            return PolicyStore.CreatePolicyAsync(policy);
        }

        public Task AddRoleAsync(string roleId, string roleName)
        {
            return RoleStore.CreateRoleAsync(new RoleDefinition(roleId, roleName, roleName));
        }

        public Task AssignRoleAsync(string userId, string roleName)
        {
            return RoleStore.AddUserToRoleAsync(userId, roleName);
        }

        public async Task DefineAndGrantAsync(string userId, string permissionName)
        {
            await PermissionStore.AddOrUpdatePermissionAsync(new PermissionDefinition(permissionName, permissionName));
            await PermissionStore.GrantPermissionToUserAsync(userId, permissionName);
        }
    }
}
