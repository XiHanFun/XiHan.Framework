// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Security.Principal;
using XiHan.Framework.Security.Claims;
using XiHan.Framework.Utils.Diagnostics;
using XiHan.Framework.Utils.Extensions;

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
    public static long? FindUserId(this ClaimsPrincipal principal)
    {
        Guard.NotNull(principal, nameof(principal));

        var userIdOrNull = principal.Claims.FirstOrDefault(c => c.Type == XiHanClaimTypes.UserId);
        return userIdOrNull is null || userIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : long.TryParse(userIdOrNull.Value, out var id) ? id : null;
    }

    /// <summary>
    /// 查找用户标识
    /// </summary>
    /// <param name="identity"></param>
    /// <returns></returns>
    public static long? FindUserId(this IIdentity identity)
    {
        Guard.NotNull(identity, nameof(identity));

        var claimsIdentity = identity as ClaimsIdentity;

        var userIdOrNull = claimsIdentity?.Claims.FirstOrDefault(c => c.Type == XiHanClaimTypes.UserId);
        return userIdOrNull is null || userIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : long.TryParse(userIdOrNull.Value, out var id) ? id : null;
    }

    /// <summary>
    /// 查找租户标识
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    public static long? FindTenantId(this ClaimsPrincipal principal)
    {
        Guard.NotNull(principal, nameof(principal));

        var tenantIdOrNull = principal.Claims.FirstOrDefault(c => c.Type == XiHanClaimTypes.TenantId);
        return tenantIdOrNull is null || tenantIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : long.TryParse(tenantIdOrNull.Value, out var id) ? id : null;
    }

    /// <summary>
    /// 查找租户标识
    /// </summary>
    /// <param name="identity"></param>
    /// <returns></returns>
    public static long? FindTenantId(this IIdentity identity)
    {
        Guard.NotNull(identity, nameof(identity));

        var claimsIdentity = identity as ClaimsIdentity;

        var tenantIdOrNull = claimsIdentity?.Claims.FirstOrDefault(c => c.Type == XiHanClaimTypes.TenantId);
        return tenantIdOrNull is null || tenantIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : long.TryParse(tenantIdOrNull.Value, out var id) ? id : null;
    }

    /// <summary>
    /// 查找客户端标识
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    public static string? FindClientId(this ClaimsPrincipal principal)
    {
        Guard.NotNull(principal, nameof(principal));

        var clientIdOrNull = principal.Claims.FirstOrDefault(c => c.Type == XiHanClaimTypes.ClientId);
        return clientIdOrNull is null || clientIdOrNull.Value.IsNullOrWhiteSpace() ? null : clientIdOrNull.Value;
    }

    /// <summary>
    /// 查找客户端标识
    /// </summary>
    /// <param name="identity"></param>
    /// <returns></returns>
    public static string? FindClientId(this IIdentity identity)
    {
        Guard.NotNull(identity, nameof(identity));

        var claimsIdentity = identity as ClaimsIdentity;

        var clientIdOrNull = claimsIdentity?.Claims.FirstOrDefault(c => c.Type == XiHanClaimTypes.ClientId);
        return clientIdOrNull is null || clientIdOrNull.Value.IsNullOrWhiteSpace() ? null : clientIdOrNull.Value;
    }

    /// <summary>
    /// 查找版本标识
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    public static Guid? FindEditionId(this ClaimsPrincipal principal)
    {
        Guard.NotNull(principal, nameof(principal));

        var editionIdOrNull = principal.Claims.FirstOrDefault(c => c.Type == XiHanClaimTypes.EditionId);
        return editionIdOrNull is null || editionIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : Guid.TryParse(editionIdOrNull.Value, out var guid) ? guid : null;
    }

    /// <summary>
    /// 查找版本标识
    /// </summary>
    /// <param name="identity"></param>
    /// <returns></returns>
    public static Guid? FindEditionId(this IIdentity identity)
    {
        Guard.NotNull(identity, nameof(identity));

        var claimsIdentity = identity as ClaimsIdentity;

        var editionIdOrNull = claimsIdentity?.Claims.FirstOrDefault(c => c.Type == XiHanClaimTypes.EditionId);
        return editionIdOrNull is null || editionIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : Guid.TryParse(editionIdOrNull.Value, out var guid) ? guid : null;
    }

    /// <summary>
    /// 查找模仿者租户标识
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    public static long? FindImpersonatorTenantId(this ClaimsPrincipal principal)
    {
        Guard.NotNull(principal, nameof(principal));

        var impersonatorTenantIdOrNull = principal.Claims.FirstOrDefault(c => c.Type == XiHanClaimTypes.ImpersonatorTenantId);
        return impersonatorTenantIdOrNull is null || impersonatorTenantIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : long.TryParse(impersonatorTenantIdOrNull.Value, out var id) ? id : null;
    }

    /// <summary>
    /// 查找模仿者租户标识
    /// </summary>
    /// <param name="identity"></param>
    /// <returns></returns>
    public static long? FindImpersonatorTenantId(this IIdentity identity)
    {
        Guard.NotNull(identity, nameof(identity));

        var claimsIdentity = identity as ClaimsIdentity;

        var impersonatorTenantIdOrNull = claimsIdentity?.Claims.FirstOrDefault(c => c.Type == XiHanClaimTypes.ImpersonatorTenantId);
        return impersonatorTenantIdOrNull is null || impersonatorTenantIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : long.TryParse(impersonatorTenantIdOrNull.Value, out var id) ? id : null;
    }

    /// <summary>
    /// 查找模仿者用户标识
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    public static long? FindImpersonatorUserId(this ClaimsPrincipal principal)
    {
        Guard.NotNull(principal, nameof(principal));

        var impersonatorUserIdOrNull = principal.Claims.FirstOrDefault(c => c.Type == XiHanClaimTypes.ImpersonatorUserId);
        return impersonatorUserIdOrNull is null || impersonatorUserIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : long.TryParse(impersonatorUserIdOrNull.Value, out var id) ? id : null;
    }

    /// <summary>
    /// 查找模仿者用户标识
    /// </summary>
    /// <param name="identity"></param>
    /// <returns></returns>
    public static long? FindImpersonatorUserId(this IIdentity identity)
    {
        Guard.NotNull(identity, nameof(identity));

        var claimsIdentity = identity as ClaimsIdentity;

        var impersonatorUserIdOrNull = claimsIdentity?.Claims.FirstOrDefault(c => c.Type == XiHanClaimTypes.ImpersonatorUserId);
        return impersonatorUserIdOrNull is null || impersonatorUserIdOrNull.Value.IsNullOrWhiteSpace()
            ? null
            : long.TryParse(impersonatorUserIdOrNull.Value, out var id) ? id : null;
    }

    /// <summary>
    /// 是否处于模仿态
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    public static bool IsImpersonating(this ClaimsPrincipal principal)
    {
        return principal.FindImpersonatorUserId().HasValue;
    }

    /// <summary>
    /// 构建模仿者声明集合
    /// </summary>
    /// <remarks>
    /// 用户标识不大于 0、或可选项为空白时，对应声明不产出。
    /// </remarks>
    /// <param name="impersonatorUserId">模仿者用户标识</param>
    /// <param name="impersonatorUserName">模仿者用户名</param>
    /// <param name="impersonatorTenantId">模仿者租户标识</param>
    /// <param name="impersonatorTenantName">模仿者租户名称</param>
    /// <returns>模仿者声明集合</returns>
    public static IReadOnlyList<Claim> BuildImpersonatorClaims(
        long impersonatorUserId,
        string? impersonatorUserName = null,
        long? impersonatorTenantId = null,
        string? impersonatorTenantName = null)
    {
        if (impersonatorUserId <= 0)
        {
            return [];
        }

        var claims = new List<Claim>
        {
            new(XiHanClaimTypes.ImpersonatorUserId, impersonatorUserId.ToString())
        };

        if (!impersonatorUserName.IsNullOrWhiteSpace())
        {
            claims.Add(new Claim(XiHanClaimTypes.ImpersonatorUserName, impersonatorUserName));
        }

        if (impersonatorTenantId.HasValue)
        {
            claims.Add(new Claim(XiHanClaimTypes.ImpersonatorTenantId, impersonatorTenantId.Value.ToString()));
        }

        if (!impersonatorTenantName.IsNullOrWhiteSpace())
        {
            claims.Add(new Claim(XiHanClaimTypes.ImpersonatorTenantName, impersonatorTenantName));
        }

        return claims;
    }

    /// <summary>
    /// 添加声明
    /// </summary>
    /// <param name="claimsIdentity"></param>
    /// <param name="claim"></param>
    /// <returns></returns>
    public static ClaimsIdentity AddIfNotContains(this ClaimsIdentity claimsIdentity, Claim claim)
    {
        Guard.NotNull(claimsIdentity, nameof(claimsIdentity));

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
        Guard.NotNull(claimsIdentity, nameof(claimsIdentity));

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
        Guard.NotNull(claimsIdentity, nameof(claimsIdentity));

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
    public static ClaimsPrincipal AddIdentityIfNotContains(this ClaimsPrincipal principal, ClaimsIdentity identity)
    {
        Guard.NotNull(principal, nameof(principal));

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
    public static string? FindSessionId(this IIdentity identity)
    {
        Guard.NotNull(identity, nameof(identity));

        var claimsIdentity = identity as ClaimsIdentity;

        var sessionIdOrNull = claimsIdentity?.Claims.FirstOrDefault(c => c.Type == XiHanClaimTypes.SessionId);
        return sessionIdOrNull is null || sessionIdOrNull.Value.IsNullOrWhiteSpace() ? null : sessionIdOrNull.Value;
    }

    /// <summary>
    /// 查找会话标识
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    public static string? FindSessionId(this ClaimsPrincipal principal)
    {
        Guard.NotNull(principal, nameof(principal));

        var sessionIdOrNull = principal.Claims.FirstOrDefault(c => c.Type == XiHanClaimTypes.SessionId);
        return sessionIdOrNull is null || sessionIdOrNull.Value.IsNullOrWhiteSpace() ? null : sessionIdOrNull.Value;
    }
}
