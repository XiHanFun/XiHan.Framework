// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;
using XiHan.Framework.Security.Users;

namespace XiHan.Framework.Authorization.Tests.Infrastructure;

/// <summary>
/// 当前登录主体替身
/// </summary>
/// <remarks>
/// 策略评估器的声明要求只从 <see cref="ICurrentUser"/> 读取，未认证时应当拿不到任何声明，
/// 这里用可注入声明列表的手写替身把这条链路打开，避免依赖真实的 HTTP 上下文。
/// </remarks>
public sealed class FakeCurrentUser : ICurrentUser
{
    private readonly Claim[] _claims;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="claims">主体持有的声明</param>
    public FakeCurrentUser(params Claim[] claims)
    {
        _claims = claims;
    }

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
    /// 查找单个声明
    /// </summary>
    /// <param name="claimType">声明类型</param>
    /// <returns>声明，不存在返回 null</returns>
    public Claim? FindClaim(string claimType)
    {
        return _claims.FirstOrDefault(claim => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 查找同类型的全部声明
    /// </summary>
    /// <param name="claimType">声明类型</param>
    /// <returns>声明数组</returns>
    public Claim[] FindClaims(string claimType)
    {
        return [.. _claims.Where(claim => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// 获取全部声明
    /// </summary>
    /// <returns>声明数组</returns>
    public Claim[] GetAllClaims()
    {
        return [.. _claims];
    }

    /// <summary>
    /// 是否在角色中
    /// </summary>
    /// <param name="roleName">角色名称</param>
    /// <returns>是否在角色中</returns>
    public bool IsInRole(string roleName)
    {
        return Roles.Contains(roleName, StringComparer.Ordinal);
    }
}
