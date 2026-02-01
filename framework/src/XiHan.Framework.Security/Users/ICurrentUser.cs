#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICurrentUser
// Guid:ac4fcaeb-d22c-4c3b-bd3e-d27eca85f87e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/25 05:18:26
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Claims;

namespace XiHan.Framework.Security.Users;

/// <summary>
/// 当前用户
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// 是否认证
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// 用户标识
    /// </summary>
    Guid? Id { get; }

    /// <summary>
    /// 用户名
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// 名称
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// 姓
    /// </summary>
    string? SurName { get; }

    /// <summary>
    /// 手机号
    /// </summary>
    string? PhoneNumber { get; }

    /// <summary>
    /// 手机号是否验证
    /// </summary>
    bool PhoneNumberVerified { get; }

    /// <summary>
    /// 邮箱
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// 邮箱是否验证
    /// </summary>
    bool EmailVerified { get; }

    /// <summary>
    /// 租户标识
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>
    /// 角色
    /// </summary>
    string[] Roles { get; }

    /// <summary>
    /// 获取声明
    /// </summary>
    /// <param name="claimType"></param>
    /// <returns></returns>
    Claim? FindClaim(string claimType);

    /// <summary>
    /// 获取声明
    /// </summary>
    /// <param name="claimType"></param>
    /// <returns></returns>
    Claim[] FindClaims(string claimType);

    /// <summary>
    /// 获取所有权限
    /// </summary>
    /// <returns></returns>
    Claim[] GetAllClaims();

    /// <summary>
    /// 是否在角色中
    /// </summary>
    /// <param name="roleName"></param>
    /// <returns></returns>
    bool IsInRole(string roleName);
}
