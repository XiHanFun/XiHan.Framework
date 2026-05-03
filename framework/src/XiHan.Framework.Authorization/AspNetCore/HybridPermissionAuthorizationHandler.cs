#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HybridPermissionAuthorizationHandler
// Guid:892f8aa9-882e-4c7d-aef6-e4d8fd50e968
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/10 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using XiHan.Framework.Authorization.Abac;
using XiHan.Framework.Authorization.Permissions;
using XiHan.Framework.Security.Claims;

namespace XiHan.Framework.Authorization.AspNetCore;

/// <summary>
/// 混合权限授权处理器
/// </summary>
public class HybridPermissionAuthorizationHandler : AuthorizationHandler<HybridPermissionRequirement>
{
    private const string WildcardPermission = "*";

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

    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, HybridPermissionRequirement requirement)
    {
        var userId = ResolveUserId(context.User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(requirement.PermissionCode))
        {
            var isGranted = HasPermissionClaim(context.User, requirement.PermissionCode)
                || await _permissionChecker.IsGrantedAsync(userId, requirement.PermissionCode);
            if (!isGranted)
            {
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
        }
    }

    private static bool HasPermissionClaim(ClaimsPrincipal principal, string permissionCode)
    {
        var normalizedPermissionCode = permissionCode.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPermissionCode))
        {
            return false;
        }

        return principal.Claims
            .Where(IsPermissionClaim)
            .SelectMany(claim => SplitPermissionClaimValue(claim.Value))
            .Any(permission =>
                string.Equals(permission, WildcardPermission, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, normalizedPermissionCode, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPermissionClaim(Claim claim)
    {
        return string.Equals(claim.Type, XiHanClaimTypes.Permission, StringComparison.OrdinalIgnoreCase)
            || string.Equals(claim.Type, "permissions", StringComparison.OrdinalIgnoreCase)
            || string.Equals(claim.Type, "scope", StringComparison.OrdinalIgnoreCase)
            || string.Equals(claim.Type, "scp", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SplitPermissionClaimValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var permission in value.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return permission;
        }
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
