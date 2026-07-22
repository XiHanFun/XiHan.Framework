// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;

namespace XiHan.Framework.Authorization.AspNetCore;

/// <summary>
/// 混合权限要求
/// </summary>
public sealed class HybridPermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="permissionCode">权限编码</param>
    /// <param name="abacPolicyCode">ABAC 策略编码</param>
    public HybridPermissionRequirement(string permissionCode, string abacPolicyCode)
    {
        PermissionCode = permissionCode?.Trim() ?? string.Empty;
        AbacPolicyCode = abacPolicyCode?.Trim() ?? string.Empty;
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
