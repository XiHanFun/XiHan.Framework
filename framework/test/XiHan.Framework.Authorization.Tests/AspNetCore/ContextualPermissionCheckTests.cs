// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using XiHan.Framework.Authorization.AspNetCore;
using XiHan.Framework.Authorization.Permissions;
using XiHan.Framework.Authorization.Tests.Infrastructure;

namespace XiHan.Framework.Authorization.Tests.AspNetCore;

/// <summary>
/// 带请求上下文的权限判定
/// </summary>
/// <remarks>
/// 处理器把主体与授权资源交给 <see cref="IPermissionChecker.CheckAsync"/>，检查器据此按请求求值附加条件；
/// 不通过时记下带原因码的失败原因，403 据此写出说明。缺省实现转调 IsGrantedAsync，现有检查器不用改。
/// </remarks>
public class ContextualPermissionCheckTests
{
    /// <summary>
    /// 缺省 CheckAsync 转调 IsGrantedAsync：授予即通过，没授予给出「没有授予」
    /// </summary>
    [Fact]
    public async Task DefaultCheckAsync_ForwardsToIsGranted()
    {
        IPermissionChecker checker = new FakePermissionChecker("read");

        var granted = await checker.CheckAsync(new PermissionCheckContext("u1", "read"));
        var denied = await checker.CheckAsync(new PermissionCheckContext("u1", "write"));

        Assert.True(granted.IsGranted);
        Assert.False(denied.IsGranted);
        Assert.Equal(PermissionFailureCodes.NotGranted, denied.FailureCode);
    }

    /// <summary>
    /// 上下文从授权资源里取出当前请求：资源本身是 HttpContext，或带 HttpContext 属性的过滤器上下文
    /// </summary>
    [Fact]
    public void Context_ResolvesHttpContextFromResource()
    {
        var httpContext = new DefaultHttpContext();

        Assert.Same(httpContext, new PermissionCheckContext("u1", "read", resource: httpContext).HttpContext);
        Assert.Same(httpContext, new PermissionCheckContext("u1", "read", resource: new FilterLike(httpContext)).HttpContext);
        Assert.Null(new PermissionCheckContext("u1", "read", resource: "not-a-request").HttpContext);
        Assert.Throws<ArgumentException>(() => new PermissionCheckContext(" ", "read"));
    }

    /// <summary>
    /// 处理器把主体、权限码与资源原样交给检查器
    /// </summary>
    [Fact]
    public async Task Handler_PassesPrincipalAndResource()
    {
        var checker = new RecordingChecker(PermissionCheckResult.Granted);
        var handler = new HybridPermissionAuthorizationHandler(checker, new FakeAbacAttributeCollector(), new FakeAbacEvaluator(true));
        var principal = CreateUserPrincipal();
        var httpContext = new DefaultHttpContext();
        var context = new AuthorizationHandlerContext([new HybridPermissionRequirement("read", string.Empty)], principal, httpContext);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        var received = Assert.Single(checker.Contexts);
        Assert.Equal("u1", received.UserId);
        Assert.Equal("read", received.PermissionCode);
        Assert.Same(principal, received.Principal);
        Assert.Same(httpContext, received.HttpContext);
    }

    /// <summary>
    /// 条件不满足：处理器失败并记下检查器给的原因码与说明
    /// </summary>
    [Fact]
    public async Task Handler_ConditionNotMet_FailsWithReason()
    {
        var handler = new HybridPermissionAuthorizationHandler(
            new RecordingChecker(PermissionCheckResult.ConditionNotMet), new FakeAbacAttributeCollector(), new FakeAbacEvaluator(true));
        var context = new AuthorizationHandlerContext([new HybridPermissionRequirement("read", string.Empty)], CreateUserPrincipal(), null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
        var reason = Assert.IsType<PermissionAuthorizationFailureReason>(Assert.Single(context.FailureReasons));
        Assert.Equal(PermissionFailureCodes.ConditionNotMet, reason.Code);
        Assert.Equal("当前条件下不允许此操作", reason.Message);
    }

    /// <summary>
    /// 检查器没给原因码时按「没有授予」记
    /// </summary>
    [Fact]
    public async Task Handler_MissingFailureCode_DefaultsToNotGranted()
    {
        var handler = new HybridPermissionAuthorizationHandler(
            new RecordingChecker(new PermissionCheckResult(false)), new FakeAbacAttributeCollector(), new FakeAbacEvaluator(true));
        var context = new AuthorizationHandlerContext([new HybridPermissionRequirement("read", string.Empty)], CreateUserPrincipal(), null);

        await handler.HandleAsync(context);

        var reason = Assert.IsType<PermissionAuthorizationFailureReason>(Assert.Single(context.FailureReasons));
        Assert.Equal(PermissionFailureCodes.NotGranted, reason.Code);
        Assert.Equal("权限不足", reason.Message);
    }

    /// <summary>
    /// 权限通过而 ABAC 策略不通过：记 ABAC 拒绝
    /// </summary>
    [Fact]
    public async Task Handler_AbacDenied_FailsWithReason()
    {
        var handler = new HybridPermissionAuthorizationHandler(
            new RecordingChecker(PermissionCheckResult.Granted), new FakeAbacAttributeCollector(), new FakeAbacEvaluator(false));
        var context = new AuthorizationHandlerContext([new HybridPermissionRequirement("read", "self_only")], CreateUserPrincipal(), null);

        await handler.HandleAsync(context);

        var reason = Assert.IsType<PermissionAuthorizationFailureReason>(Assert.Single(context.FailureReasons));
        Assert.Equal(PermissionFailureCodes.AbacDenied, reason.Code);
    }

    private static ClaimsPrincipal CreateUserPrincipal()
    {
        return new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "u1")], "TestAuth"));
    }

    /// <summary>
    /// 带 HttpContext 属性的资源（模拟 MVC 过滤器上下文）
    /// </summary>
    private sealed class FilterLike(HttpContext httpContext)
    {
        public HttpContext HttpContext { get; } = httpContext;
    }

    /// <summary>
    /// 记录收到的判定输入并返回固定结果的检查器
    /// </summary>
    private sealed class RecordingChecker(PermissionCheckResult result) : IPermissionChecker
    {
        public List<PermissionCheckContext> Contexts { get; } = [];

        public Task<PermissionCheckResult> CheckAsync(PermissionCheckContext context, CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);
            return Task.FromResult(result);
        }

        public Task<bool> IsGrantedAsync(string userId, string permissionName, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("处理器应调用 CheckAsync");
        }

        public Task<bool> IsAnyGrantedAsync(string userId, List<string> permissionNames, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> IsAllGrantedAsync(string userId, List<string> permissionNames, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<string>> GetGrantedPermissionsAsync(string userId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> PermissionExistsAsync(string permissionName, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
