#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CurrentUserExtensions
// Guid:0771b2d2-aa0a-4073-b792-3efe1e0a1791
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/25 5:23:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Security.Claims;
using XiHan.Framework.Security.Users;
using XiHan.Framework.Utils.System;
using XiHan.Framework.Utils.Text;

namespace XiHan.Framework.Security.Extensions;

/// <summary>
/// 当前用户扩展
/// </summary>
public static class CurrentUserExtensions
{
    /// <summary>
    /// 查找声明
    /// </summary>
    /// <param name="currentUser"></param>
    /// <param name="claimType"></param>
    /// <returns></returns>
    public static string? FindClaimValue(this ICurrentUser currentUser, string claimType)
    {
        return currentUser.FindClaim(claimType)?.Value;
    }

    /// <summary>
    /// 查找声明
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="currentUser"></param>
    /// <param name="claimType"></param>
    /// <returns></returns>
    public static T FindClaimValue<T>(this ICurrentUser currentUser, string claimType)
        where T : struct
    {
        var value = currentUser.FindClaimValue(claimType);
        return value is null ? default : value.To<T>();
    }

    /// <summary>
    /// 获取用户标识
    /// </summary>
    /// <param name="currentUser"></param>
    /// <returns></returns>
    public static Guid GetId(this ICurrentUser currentUser)
    {
        Debug.Assert(currentUser.Id is not null, "当前用户Id不为空");

        return currentUser!.Id!.Value;
    }

    /// <summary>
    /// 获取模仿者租户标识
    /// </summary>
    /// <param name="currentUser"></param>
    /// <returns></returns>
    public static Guid? FindImpersonatorTenantId([NotNull] this ICurrentUser currentUser)
    {
        var impersonatorTenantId = currentUser.FindClaimValue(XiHanClaimTypes.ImpersonatorTenantId);
        return impersonatorTenantId.IsNullOrWhiteSpace() ? null : Guid.TryParse(impersonatorTenantId, out var guid) ? guid : null;
    }

    /// <summary>
    /// 获取模仿者用户标识
    /// </summary>
    /// <param name="currentUser"></param>
    /// <returns></returns>
    public static Guid? FindImpersonatorUserId([NotNull] this ICurrentUser currentUser)
    {
        var impersonatorUserId = currentUser.FindClaimValue(XiHanClaimTypes.ImpersonatorUserId);
        return impersonatorUserId.IsNullOrWhiteSpace() ? null : Guid.TryParse(impersonatorUserId, out var guid) ? guid : null;
    }

    /// <summary>
    /// 获取模仿者租户名称
    /// </summary>
    /// <param name="currentUser"></param>
    /// <returns></returns>
    public static string? FindImpersonatorTenantName([NotNull] this ICurrentUser currentUser)
    {
        return currentUser.FindClaimValue(XiHanClaimTypes.ImpersonatorTenantName);
    }

    /// <summary>
    /// 获取模仿者用户名
    /// </summary>
    /// <param name="currentUser"></param>
    /// <returns></returns>
    public static string? FindImpersonatorUserName([NotNull] this ICurrentUser currentUser)
    {
        return currentUser.FindClaimValue(XiHanClaimTypes.ImpersonatorUserName);
    }

    /// <summary>
    /// 获取会话标识
    /// </summary>
    /// <param name="currentUser"></param>
    /// <returns></returns>
    public static string GetSessionId([NotNull] this ICurrentUser currentUser)
    {
        var sessionId = currentUser.FindSessionId();
        Debug.Assert(sessionId is not null, "sessionId 不为空");
        return sessionId!;
    }

    /// <summary>
    /// 查找会话标识
    /// </summary>
    /// <param name="currentUser"></param>
    /// <returns></returns>
    public static string? FindSessionId([NotNull] this ICurrentUser currentUser)
    {
        return currentUser.FindClaimValue(XiHanClaimTypes.SessionId);
    }
}
