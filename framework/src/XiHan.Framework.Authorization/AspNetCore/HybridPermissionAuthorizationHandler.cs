// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using XiHan.Framework.Authorization.Abac;
using XiHan.Framework.Authorization.Permissions;

namespace XiHan.Framework.Authorization.AspNetCore;

/// <summary>
/// 混合权限授权处理器
/// </summary>
public class HybridPermissionAuthorizationHandler : AuthorizationHandler<HybridPermissionRequirement>
{
    private readonly IPermissionChecker _permissionChecker;
    private readonly IAbacAttributeCollector _abacAttributeCollector;
    private readonly IAbacEvaluator _abacEvaluator;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="permissionChecker">权限检查器</param>
    /// <param name="abacAttributeCollector">ABAC 属性收集器</param>
    /// <param name="abacEvaluator">ABAC 评估器</param>
    public HybridPermissionAuthorizationHandler(
        IPermissionChecker permissionChecker,
        IAbacAttributeCollector abacAttributeCollector,
        IAbacEvaluator abacEvaluator)
    {
        _permissionChecker = permissionChecker;
        _abacAttributeCollector = abacAttributeCollector;
        _abacEvaluator = abacEvaluator;
    }

    /// <summary>
    /// 先按权限编码做实时权限检查，再按 ABAC 策略编码收集属性并评估，两者均通过才判定授权成功
    /// </summary>
    /// <remarks>
    /// 权限检查走带请求上下文的 <see cref="IPermissionChecker.CheckAsync"/>，检查器可按请求求值附加条件；
    /// 不通过时记下 <see cref="PermissionAuthorizationFailureReason"/>，写 403 时据此带出原因。
    /// </remarks>
    /// <param name="context">授权处理上下文</param>
    /// <param name="requirement">混合权限要求</param>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, HybridPermissionRequirement requirement)
    {
        var userId = ResolveUserId(context.User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(requirement.PermissionCode))
        {
            // 一律以实时检查器（授权快照）为准，含超管（其快照含通配 *）；不再信任登录时冻结在 token 里的权限声明，
            // 确保授权/撤销、用户禁用、会话注销均即时生效（实时检查器自身负责通配与会话/用户有效性判定）。
            var check = await _permissionChecker.CheckAsync(
                new PermissionCheckContext(userId, requirement.PermissionCode, context.User, context.Resource));
            if (!check.IsGranted)
            {
                context.Fail(new PermissionAuthorizationFailureReason(
                    this,
                    check.FailureCode ?? PermissionFailureCodes.NotGranted,
                    check.FailureMessage ?? PermissionCheckResult.NotGranted.FailureMessage!));
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(requirement.AbacPolicyCode))
        {
            context.Succeed(requirement);
            return;
        }

        var attributes = await _abacAttributeCollector.CollectAsync(
            context.User,
            context.Resource,
            requirement.PermissionCode,
            requirement.AbacPolicyCode);

        var evaluation = await _abacEvaluator.EvaluateAsync(new AbacEvaluationContext
        {
            UserId = userId,
            PermissionCode = requirement.PermissionCode,
            PolicyCode = requirement.AbacPolicyCode,
            Resource = context.Resource,
            SubjectAttributes = attributes.SubjectAttributes,
            ResourceAttributes = attributes.ResourceAttributes,
            EnvironmentAttributes = attributes.EnvironmentAttributes,
            EvaluationTime = DateTimeOffset.UtcNow
        });

        if (evaluation.IsAllowed)
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail(new PermissionAuthorizationFailureReason(this, PermissionFailureCodes.AbacDenied, "当前条件下不允许此操作"));
    }

    private static string ResolveUserId(ClaimsPrincipal principal)
    {
        return principal.Claims
            .FirstOrDefault(claim =>
                string.Equals(claim.Type, ClaimTypes.NameIdentifier, StringComparison.OrdinalIgnoreCase)
                || string.Equals(claim.Type, "sub", StringComparison.OrdinalIgnoreCase)
                || string.Equals(claim.Type, "userid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(claim.Type, "user_id", StringComparison.OrdinalIgnoreCase))
            ?.Value ?? string.Empty;
    }
}
