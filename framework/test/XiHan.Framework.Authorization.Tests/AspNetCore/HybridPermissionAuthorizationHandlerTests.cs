// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using System.Security.Claims;
using XiHan.Framework.Authorization.AspNetCore;
using XiHan.Framework.Authorization.Tests.Infrastructure;

namespace XiHan.Framework.Authorization.Tests.AspNetCore;

/// <summary>
/// 混合权限授权处理器测试
/// </summary>
/// <remarks>
/// 处理器是 RBAC 与 ABAC 的合流点，判定必须是“与”而不是“或”，且任何一步不通过都只是不调用 Succeed（保持失败），
/// 不能抛异常。这里逐条验证短路顺序：拿不到用户 → 不检查；RBAC 不通过 → 不进 ABAC；没配 ABAC → 直接通过。
/// </remarks>
public class HybridPermissionAuthorizationHandlerTests
{
    /// <summary>
    /// 主体里没有任何用户标识声明时直接失败，且不触碰权限检查器
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithoutUserIdClaim_FailsWithoutCheckingPermission()
    {
        var checker = new FakePermissionChecker("read");
        var collector = new FakeAbacAttributeCollector();
        var evaluator = new FakeAbacEvaluator(true);
        var handler = new HybridPermissionAuthorizationHandler(checker, collector, evaluator);
        var context = CreateContext(new HybridPermissionRequirement("read", string.Empty), new ClaimsPrincipal());

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.Empty(checker.CheckedPermissions);
        Assert.Equal(0, collector.CallCount);
    }

    /// <summary>
    /// 用户标识可以来自四种声明类型中的任意一种
    /// </summary>
    /// <param name="claimType">承载用户标识的声明类型</param>
    [Theory]
    [InlineData(ClaimTypes.NameIdentifier)]
    [InlineData("sub")]
    [InlineData("userid")]
    [InlineData("user_id")]
    public async Task HandleAsync_ResolvesUserIdFromAlternativeClaimTypes(string claimType)
    {
        var checker = new FakePermissionChecker("read");
        var handler = new HybridPermissionAuthorizationHandler(checker, new FakeAbacAttributeCollector(), new FakeAbacEvaluator(true));
        var context = CreateContext(
            new HybridPermissionRequirement("read", string.Empty),
            CreatePrincipal(new Claim(claimType, "u1")));

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    /// <summary>
    /// 权限检查不通过时失败，并且不进入 ABAC 评估
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenPermissionDenied_ShortCircuitsBeforeAbac()
    {
        var checker = new FakePermissionChecker();
        var collector = new FakeAbacAttributeCollector();
        var evaluator = new FakeAbacEvaluator(true);
        var handler = new HybridPermissionAuthorizationHandler(checker, collector, evaluator);
        var context = CreateContext(new HybridPermissionRequirement("read", "allow"), CreateUserPrincipal());

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.Equal("read", Assert.Single(checker.CheckedPermissions));
        Assert.Equal(0, collector.CallCount);
        Assert.Equal(0, evaluator.CallCount);
    }

    /// <summary>
    /// 只配权限、未配 ABAC 时权限通过即成功，不做属性收集
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithoutAbacPolicy_SucceedsOnPermissionOnly()
    {
        var collector = new FakeAbacAttributeCollector();
        var evaluator = new FakeAbacEvaluator(false);
        var handler = new HybridPermissionAuthorizationHandler(new FakePermissionChecker("read"), collector, evaluator);
        var context = CreateContext(new HybridPermissionRequirement("read", string.Empty), CreateUserPrincipal());

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        Assert.Equal(0, collector.CallCount);
        Assert.Equal(0, evaluator.CallCount);
    }

    /// <summary>
    /// 只配 ABAC、未配权限编码时跳过权限检查，直接走属性收集与评估
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithoutPermissionCode_SkipsPermissionCheck()
    {
        var checker = new FakePermissionChecker();
        var collector = new FakeAbacAttributeCollector();
        var evaluator = new FakeAbacEvaluator(true);
        var handler = new HybridPermissionAuthorizationHandler(checker, collector, evaluator);
        var context = CreateContext(new HybridPermissionRequirement(string.Empty, "self_only"), CreateUserPrincipal());

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        Assert.Empty(checker.CheckedPermissions);
        Assert.Equal(1, evaluator.CallCount);
    }

    /// <summary>
    /// 权限通过且 ABAC 放行时才判定成功
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenPermissionAndAbacPass_Succeeds()
    {
        var handler = new HybridPermissionAuthorizationHandler(
            new FakePermissionChecker("read"),
            new FakeAbacAttributeCollector(),
            new FakeAbacEvaluator(true));
        var context = CreateContext(new HybridPermissionRequirement("read", "self_only"), CreateUserPrincipal());

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    /// <summary>
    /// 权限通过但 ABAC 拒绝时整体失败（两者是“与”关系）
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenAbacDenies_Fails()
    {
        var handler = new HybridPermissionAuthorizationHandler(
            new FakePermissionChecker("read"),
            new FakeAbacAttributeCollector(),
            new FakeAbacEvaluator(false));
        var context = CreateContext(new HybridPermissionRequirement("read", "self_only"), CreateUserPrincipal());

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    /// <summary>
    /// 属性收集器拿到的主体、资源、权限编码与策略编码与要求一致
    /// </summary>
    [Fact]
    public async Task HandleAsync_ForwardsRequirementAndResourceToCollector()
    {
        var collector = new FakeAbacAttributeCollector();
        var handler = new HybridPermissionAuthorizationHandler(
            new FakePermissionChecker("read"),
            collector,
            new FakeAbacEvaluator(true));
        var principal = CreateUserPrincipal();
        var resource = new object();
        var context = CreateContext(new HybridPermissionRequirement("read", "self_only"), principal, resource);

        await handler.HandleAsync(context);

        Assert.Equal(1, collector.CallCount);
        Assert.Same(principal, collector.LastPrincipal);
        Assert.Same(resource, collector.LastResource);
        Assert.Equal("read", collector.LastPermissionCode);
        Assert.Equal("self_only", collector.LastPolicyCode);
    }

    /// <summary>
    /// 评估上下文的三组属性直接引用收集器的产出，中间不做拷贝或过滤
    /// </summary>
    [Fact]
    public async Task HandleAsync_BuildsEvaluationContextFromCollectedAttributes()
    {
        var collector = new FakeAbacAttributeCollector();
        collector.Result.SubjectAttributes["user_id"] = "u1";
        collector.Result.ResourceAttributes["owner_user_id"] = "u1";
        collector.Result.EnvironmentAttributes["request_method"] = "GET";
        var evaluator = new FakeAbacEvaluator(true);
        var handler = new HybridPermissionAuthorizationHandler(new FakePermissionChecker("read"), collector, evaluator);
        var resource = new object();
        var context = CreateContext(new HybridPermissionRequirement("read", "self_only"), CreateUserPrincipal(), resource);

        await handler.HandleAsync(context);

        var evaluationContext = evaluator.LastContext;
        Assert.NotNull(evaluationContext);
        Assert.Equal("u1", evaluationContext!.UserId);
        Assert.Equal("read", evaluationContext.PermissionCode);
        Assert.Equal("self_only", evaluationContext.PolicyCode);
        Assert.Same(resource, evaluationContext.Resource);
        Assert.Same(collector.Result.SubjectAttributes, evaluationContext.SubjectAttributes);
        Assert.Same(collector.Result.ResourceAttributes, evaluationContext.ResourceAttributes);
        Assert.Same(collector.Result.EnvironmentAttributes, evaluationContext.EnvironmentAttributes);
    }

    /// <summary>
    /// 上下文里混入其它类型的要求时不受影响，只处理自己的要求
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithForeignRequirement_OnlyHandlesOwnRequirement()
    {
        var requirement = new HybridPermissionRequirement("read", string.Empty);
        var foreign = new DenyAnonymousAuthorizationRequirement();
        var handler = new HybridPermissionAuthorizationHandler(
            new FakePermissionChecker("read"),
            new FakeAbacAttributeCollector(),
            new FakeAbacEvaluator(true));
        var context = new AuthorizationHandlerContext(
            [requirement, foreign],
            CreateUserPrincipal(),
            null);

        await handler.HandleAsync(context);

        // 自己的要求已被满足而移出待办，但外来的要求没人处理，因此整体仍未全部满足
        Assert.DoesNotContain((IAuthorizationRequirement)requirement, context.PendingRequirements);
        Assert.Contains((IAuthorizationRequirement)foreign, context.PendingRequirements);
        Assert.False(context.HasSucceeded);
    }

    private static AuthorizationHandlerContext CreateContext(
        HybridPermissionRequirement requirement,
        ClaimsPrincipal user,
        object? resource = null)
    {
        return new AuthorizationHandlerContext([requirement], user, resource);
    }

    private static ClaimsPrincipal CreateUserPrincipal()
    {
        return CreatePrincipal(new Claim(ClaimTypes.NameIdentifier, "u1"));
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
