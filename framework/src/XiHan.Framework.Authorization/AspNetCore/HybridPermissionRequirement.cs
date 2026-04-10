#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HybridPermissionRequirement
// Guid:cc9d5f40-46ce-4706-a9a3-1610e3fe2e17
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/10 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
