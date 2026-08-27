// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;
using XiHan.Framework.Security.Users;

namespace XiHan.Framework.Auditing.Tests.Fakes;

/// <summary>
/// 当前用户替身
/// </summary>
/// <remarks>
/// 只为 <see cref="DefaultEntityAuditContextProvider"/> 提供可控的身份字段，声明相关成员返回空集合。
/// </remarks>
public sealed class FakeCurrentUser : ICurrentUser
{
    /// <summary>
    /// 是否认证
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// 用户标识
    /// </summary>
    public long? UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 姓
    /// </summary>
    public string? SurName { get; set; }

    /// <summary>
    /// 手机号
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// 手机号是否验证
    /// </summary>
    public bool PhoneNumberVerified { get; set; }

    /// <summary>
    /// 邮箱
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 邮箱是否验证
    /// </summary>
    public bool EmailVerified { get; set; }

    /// <summary>
    /// 租户标识
    /// </summary>
    public long? TenantId { get; set; }

    /// <summary>
    /// 角色
    /// </summary>
    public string[] Roles { get; set; } = [];

    /// <summary>
    /// 获取声明
    /// </summary>
    /// <param name="claimType">声明类型</param>
    /// <returns>始终为空</returns>
    public Claim? FindClaim(string claimType)
    {
        return null;
    }

    /// <summary>
    /// 获取声明集合
    /// </summary>
    /// <param name="claimType">声明类型</param>
    /// <returns>始终为空集合</returns>
    public Claim[] FindClaims(string claimType)
    {
        return [];
    }

    /// <summary>
    /// 获取全部声明
    /// </summary>
    /// <returns>始终为空集合</returns>
    public Claim[] GetAllClaims()
    {
        return [];
    }

    /// <summary>
    /// 是否在角色中
    /// </summary>
    /// <param name="roleName">角色名</param>
    /// <returns>是否命中</returns>
    public bool IsInRole(string roleName)
    {
        return Roles.Contains(roleName);
    }
}
