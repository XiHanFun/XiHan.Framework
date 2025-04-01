#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanClaimsIdentityExtensions
// Guid:0a2e451a-c771-4bc9-8553-f81b886f14e0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/25 5:07:19
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Security.Principal;
using XiHan.Framework.Security.Claims;
using XiHan.Framework.Utils.System;
using XiHan.Framework.Utils.Text;

namespace XiHan.Framework.Security.Extensions;

/// <summary>
/// 曦寒声明标识扩展
/// </summary>
public static class XiHanClaimsIdentityExtensions
{
    /// <summary>
    /// 查找用户标识
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    public static Guid? FindUserId([NotNull] this ClaimsPrincipal principal)
    {
        _ = CheckHelper.NotNull(principal, nameof(principal));

        var userIdOrNull = principal.Claims?.FirstOrDefault(c => c.Type == XiHanClaimTypes.UserId);
        return userIdOrNull is null || userIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : Guid.TryParse(userIdOrNull.Value, out var guid) ? guid : null;
    }

    /// <summary>
    /// 查找用户标识
    /// </summary>
    /// <param name="identity"></param>
    /// <returns></returns>
    public static Guid? FindUserId([NotNull] this IIdentity identity)
    {
        _ = CheckHelper.NotNull(identity, nameof(identity));

        var claimsIdentity = identity as ClaimsIdentity;

        var userIdOrNull = claimsIdentity?.Claims?.FirstOrDefault(c => c.Type == XiHanClaimTypes.UserId);
        return userIdOrNull is null || userIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : Guid.TryParse(userIdOrNull.Value, out var guid) ? guid : null;
    }

    /// <summary>
    /// 查找租户标识
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    public static Guid? FindTenantId([NotNull] this ClaimsPrincipal principal)
    {
        _ = CheckHelper.NotNull(principal, nameof(principal));

        var tenantIdOrNull = principal.Claims?.FirstOrDefault(c => c.Type == XiHanClaimTypes.TenantId);
        return tenantIdOrNull is null || tenantIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : Guid.TryParse(tenantIdOrNull.Value, out var guid) ? guid : null;
    }

    /// <summary>
    /// 查找租户标识
    /// </summary>
    /// <param name="identity"></param>
    /// <returns></returns>
    public static Guid? FindTenantId([NotNull] this IIdentity identity)
    {
        _ = CheckHelper.NotNull(identity, nameof(identity));

        var claimsIdentity = identity as ClaimsIdentity;

        var tenantIdOrNull = claimsIdentity?.Claims?.FirstOrDefault(c => c.Type == XiHanClaimTypes.TenantId);
        return tenantIdOrNull is null || tenantIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : Guid.TryParse(tenantIdOrNull.Value, out var guid) ? guid : null;
    }

    /// <summary>
    /// 查找客户端标识
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    public static string? FindClientId([NotNull] this ClaimsPrincipal principal)
    {
        _ = CheckHelper.NotNull(principal, nameof(principal));

        var clientIdOrNull = principal.Claims?.FirstOrDefault(c => c.Type == XiHanClaimTypes.ClientId);
        return clientIdOrNull is null || clientIdOrNull.Value.IsNullOrWhiteSpace() ? null : clientIdOrNull.Value;
    }

    /// <summary>
    /// 查找客户端标识
    /// </summary>
    /// <param name="identity"></param>
    /// <returns></returns>
    public static string? FindClientId([NotNull] this IIdentity identity)
    {
        _ = CheckHelper.NotNull(identity, nameof(identity));

        var claimsIdentity = identity as ClaimsIdentity;

        var clientIdOrNull = claimsIdentity?.Claims?.FirstOrDefault(c => c.Type == XiHanClaimTypes.ClientId);
        return clientIdOrNull is null || clientIdOrNull.Value.IsNullOrWhiteSpace() ? null : clientIdOrNull.Value;
    }

    /// <summary>
    /// 查找版本标识
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    public static Guid? FindEditionId([NotNull] this ClaimsPrincipal principal)
    {
        _ = CheckHelper.NotNull(principal, nameof(principal));

        var editionIdOrNull = principal.Claims?.FirstOrDefault(c => c.Type == XiHanClaimTypes.EditionId);
        return editionIdOrNull is null || editionIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : Guid.TryParse(editionIdOrNull.Value, out var guid) ? guid : null;
    }

    /// <summary>
    /// 查找版本标识
    /// </summary>
    /// <param name="identity"></param>
    /// <returns></returns>
    public static Guid? FindEditionId([NotNull] this IIdentity identity)
    {
        _ = CheckHelper.NotNull(identity, nameof(identity));

        var claimsIdentity = identity as ClaimsIdentity;

        var editionIdOrNull = claimsIdentity?.Claims?.FirstOrDefault(c => c.Type == XiHanClaimTypes.EditionId);
        return editionIdOrNull is null || editionIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : Guid.TryParse(editionIdOrNull.Value, out var guid) ? guid : null;
    }

    /// <summary>
    /// 查找模仿者租户标识
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    public static Guid? FindImpersonatorTenantId([NotNull] this ClaimsPrincipal principal)
    {
        _ = CheckHelper.NotNull(principal, nameof(principal));

        var impersonatorTenantIdOrNull = principal.Claims?.FirstOrDefault(c => c.Type == XiHanClaimTypes.ImpersonatorTenantId);
        return impersonatorTenantIdOrNull is null || impersonatorTenantIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : Guid.TryParse(impersonatorTenantIdOrNull.Value, out var guid) ? guid : null;
    }

    /// <summary>
    /// 查找模仿者租户标识
    /// </summary>
    /// <param name="identity"></param>
    /// <returns></returns>
    public static Guid? FindImpersonatorTenantId([NotNull] this IIdentity identity)
    {
        _ = CheckHelper.NotNull(identity, nameof(identity));

        var claimsIdentity = identity as ClaimsIdentity;

        var impersonatorTenantIdOrNull = claimsIdentity?.Claims?.FirstOrDefault(c => c.Type == XiHanClaimTypes.ImpersonatorTenantId);
        return impersonatorTenantIdOrNull is null || impersonatorTenantIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : Guid.TryParse(impersonatorTenantIdOrNull.Value, out var guid) ? guid : null;
    }

    /// <summary>
    /// 查找模仿者用户标识
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    public static Guid? FindImpersonatorUserId([NotNull] this ClaimsPrincipal principal)
    {
        _ = CheckHelper.NotNull(principal, nameof(principal));

        var impersonatorUserIdOrNull = principal.Claims?.FirstOrDefault(c => c.Type == XiHanClaimTypes.ImpersonatorUserId);
        return impersonatorUserIdOrNull is null || impersonatorUserIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : Guid.TryParse(impersonatorUserIdOrNull.Value, out var guid) ? guid : null;
    }

    /// <summary>
    /// 查找模仿者用户标识
    /// </summary>
    /// <param name="identity"></param>
    /// <returns></returns>
    public static Guid? FindImpersonatorUserId([NotNull] this IIdentity identity)
    {
        _ = CheckHelper.NotNull(identity, nameof(identity));

        var claimsIdentity = identity as ClaimsIdentity;

        var impersonatorUserIdOrNull = claimsIdentity?.Claims?.FirstOrDefault(c => c.Type == XiHanClaimTypes.ImpersonatorUserId);
        return impersonatorUserIdOrNull is null || impersonatorUserIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : Guid.TryParse(impersonatorUserIdOrNull.Value, out var guid) ? guid : null;
    }

    /// <summary>
    /// 添加声明
    /// </summary>
    /// <param name="claimsIdentity"></param>
    /// <param name="claim"></param>
    /// <returns></returns>
    public static ClaimsIdentity AddIfNotContains(this ClaimsIdentity claimsIdentity, Claim claim)
    {
        _ = CheckHelper.NotNull(claimsIdentity, nameof(claimsIdentity));

        if (!claimsIdentity.Claims.Any(x => string.Equals(x.Type, claim.Type, StringComparison.OrdinalIgnoreCase)))
        {
            claimsIdentity.AddClaim(claim);
        }

        return claimsIdentity;
    }

    /// <summary>
    /// 移除所有声明
    /// </summary>
    /// <param name="claimsIdentity"></param>
    /// <param name="claimType"></param>
    /// <returns></returns>
    public static ClaimsIdentity RemoveAll(this ClaimsIdentity claimsIdentity, string claimType)
    {
        _ = CheckHelper.NotNull(claimsIdentity, nameof(claimsIdentity));

        foreach (var x in claimsIdentity.FindAll(claimType).ToList())
        {
            claimsIdentity.RemoveClaim(x);
        }

        return claimsIdentity;
    }

    /// <summary>
    /// 添加或替换声明
    /// </summary>
    /// <param name="claimsIdentity"></param>
    /// <param name="claim"></param>
    /// <returns></returns>
    public static ClaimsIdentity AddOrReplace(this ClaimsIdentity claimsIdentity, Claim claim)
    {
        _ = CheckHelper.NotNull(claimsIdentity, nameof(claimsIdentity));

        foreach (var x in claimsIdentity.FindAll(claim.Type).ToList())
        {
            claimsIdentity.RemoveClaim(x);
        }

        claimsIdentity.AddClaim(claim);

        return claimsIdentity;
    }

    /// <summary>
    /// 添加声明
    /// </summary>
    /// <param name="principal"></param>
    /// <param name="identity"></param>
    /// <returns></returns>
    public static ClaimsPrincipal AddIdentityIfNotContains([NotNull] this ClaimsPrincipal principal, ClaimsIdentity identity)
    {
        _ = CheckHelper.NotNull(principal, nameof(principal));

        if (!principal.Identities.Any(x => string.Equals(x.AuthenticationType, identity.AuthenticationType, StringComparison.OrdinalIgnoreCase)))
        {
            principal.AddIdentity(identity);
        }

        return principal;
    }

    /// <summary>
    /// 查找会话标识
    /// </summary>
    /// <param name="identity"></param>
    /// <returns></returns>
    public static string? FindSessionId([NotNull] this IIdentity identity)
    {
        _ = CheckHelper.NotNull(identity, nameof(identity));

        var claimsIdentity = identity as ClaimsIdentity;

        var sessionIdOrNull = claimsIdentity?.Claims?.FirstOrDefault(c => c.Type == XiHanClaimTypes.SessionId);
        return sessionIdOrNull is null || sessionIdOrNull.Value.IsNullOrWhiteSpace() ? null : sessionIdOrNull.Value;
    }

    /// <summary>
    /// 查找会话标识
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    public static string? FindSessionId([NotNull] this ClaimsPrincipal principal)
    {
        _ = CheckHelper.NotNull(principal, nameof(principal));

        var sessionIdOrNull = principal.Claims?.FirstOrDefault(c => c.Type == XiHanClaimTypes.SessionId);
        return sessionIdOrNull is null || sessionIdOrNull.Value.IsNullOrWhiteSpace() ? null : sessionIdOrNull.Value;
    }
}
