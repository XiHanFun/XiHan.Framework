// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;
using XiHan.Framework.Security.Users;

namespace XiHan.Framework.MultiTenancy.Tests.Fakes;

/// <summary>
/// 当前用户的手写替身
/// </summary>
/// <remarks>
/// <see cref="CurrentUserTenantResolveContributor"/> 只读取 <see cref="IsAuthenticated"/> 与 <see cref="TenantId"/>，
/// 其余成员按契约给出无副作用的空实现，保证替身不会掩盖被测逻辑的真实分支。
/// </remarks>
internal sealed class FakeCurrentUser : ICurrentUser
{
    /// <summary>
    /// 是否已认证
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// 用户唯一标识
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
    /// 手机号是否已验证
    /// </summary>
    public bool PhoneNumberVerified { get; set; }

    /// <summary>
    /// 邮箱
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 邮箱是否已验证
    /// </summary>
    public bool EmailVerified { get; set; }

    /// <summary>
    /// 租户唯一标识
    /// </summary>
    public long? TenantId { get; set; }

    /// <summary>
    /// 角色集合
    /// </summary>
    public string[] Roles { get; set; } = [];

    /// <summary>
    /// 查找声明
    /// </summary>
    /// <param name="claimType">声明类型</param>
    /// <returns>声明</returns>
    public Claim? FindClaim(string claimType)
    {
        return null;
    }

    /// <summary>
    /// 查找声明集合
    /// </summary>
    /// <param name="claimType">声明类型</param>
    /// <returns>声明集合</returns>
    public Claim[] FindClaims(string claimType)
    {
        return [];
    }

    /// <summary>
    /// 获取全部声明
    /// </summary>
    /// <returns>声明集合</returns>
    public Claim[] GetAllClaims()
    {
        return [];
    }

    /// <summary>
    /// 判断是否属于指定角色
    /// </summary>
    /// <param name="roleName">角色名称</param>
    /// <returns>是否属于该角色</returns>
    public bool IsInRole(string roleName)
    {
        return Array.IndexOf(Roles, roleName) >= 0;
    }
}
