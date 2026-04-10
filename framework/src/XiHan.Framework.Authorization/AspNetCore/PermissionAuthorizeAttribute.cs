#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PermissionAuthorizeAttribute
// Guid:ae2f2205-ee98-47c0-a915-1fcf628359af
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/10 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Authorization;

namespace XiHan.Framework.Authorization.AspNetCore;

/// <summary>
/// 权限授权特性（支持叠加 ABAC 策略）
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class PermissionAuthorizeAttribute : AuthorizeAttribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="permissionCode">权限编码</param>
    public PermissionAuthorizeAttribute(string permissionCode)
        : this(permissionCode, null)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="permissionCode">权限编码</param>
    /// <param name="abacPolicyCode">ABAC 策略编码</param>
    public PermissionAuthorizeAttribute(string permissionCode, string? abacPolicyCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);
        PermissionCode = permissionCode.Trim();
        AbacPolicyCode = abacPolicyCode?.Trim() ?? string.Empty;
        Policy = HybridAuthorizationPolicyName.Build(PermissionCode, AbacPolicyCode);
    }

    /// <summary>
    /// 权限编码
    /// </summary>
    public string PermissionCode { get; }

    /// <summary>
    /// ABAC 策略编码
    /// </summary>
    public string AbacPolicyCode { get; }
}
