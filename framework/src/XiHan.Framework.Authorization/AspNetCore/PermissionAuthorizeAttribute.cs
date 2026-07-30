// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
