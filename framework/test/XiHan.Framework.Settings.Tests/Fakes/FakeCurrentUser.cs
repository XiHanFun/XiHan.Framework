// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;
using XiHan.Framework.Security.Users;

namespace XiHan.Framework.Settings.Tests.Fakes;

/// <summary>
/// 当前用户替身
/// </summary>
/// <remarks>
/// 设置系统只依赖 <see cref="ICurrentUser.UserId"/> 与 <see cref="ICurrentUser.TenantId"/>，
/// 其余成员给出无副作用的空实现即可。
/// </remarks>
public sealed class FakeCurrentUser : ICurrentUser
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="userId">用户标识，null 表示匿名上下文</param>
    /// <param name="tenantId">租户标识，null 表示无租户上下文</param>
    public FakeCurrentUser(long? userId = null, long? tenantId = null)
    {
        UserId = userId;
        TenantId = tenantId;
    }

    /// <summary>
    /// 是否认证
    /// </summary>
    public bool IsAuthenticated => UserId is not null;

    /// <summary>
    /// 用户标识
    /// </summary>
    public long? UserId { get; }

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
    public long? TenantId { get; }

    /// <summary>
    /// 角色
    /// </summary>
    public string[] Roles { get; set; } = [];

    /// <summary>
    /// 获取声明
    /// </summary>
    /// <param name="claimType">声明类型</param>
    /// <returns>始终为 null</returns>
    public Claim? FindClaim(string claimType)
    {
        return null;
    }

    /// <summary>
    /// 获取声明集合
    /// </summary>
    /// <param name="claimType">声明类型</param>
    /// <returns>始终为空数组</returns>
    public Claim[] FindClaims(string claimType)
    {
        return [];
    }

    /// <summary>
    /// 获取所有声明
    /// </summary>
    /// <returns>始终为空数组</returns>
    public Claim[] GetAllClaims()
    {
        return [];
    }

    /// <summary>
    /// 是否在角色中
    /// </summary>
    /// <param name="roleName">角色名称</param>
    /// <returns>是否命中</returns>
    public bool IsInRole(string roleName)
    {
        return Roles.Contains(roleName);
    }
}
