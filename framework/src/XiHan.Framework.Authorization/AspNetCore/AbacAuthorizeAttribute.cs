#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AbacAuthorizeAttribute
// Guid:8347fbcf-af33-4902-8466-0b44adcae8ff
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/10 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Authorization;

namespace XiHan.Framework.Authorization.AspNetCore;

/// <summary>
/// ABAC 授权特性（仅策略，不校验 RBAC 权限码）
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class AbacAuthorizeAttribute : AuthorizeAttribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="abacPolicyCode">ABAC 策略编码</param>
    public AbacAuthorizeAttribute(string abacPolicyCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(abacPolicyCode);
        AbacPolicyCode = abacPolicyCode.Trim();
        Policy = HybridAuthorizationPolicyName.Build(null, AbacPolicyCode);
    }

    /// <summary>
    /// ABAC 策略编码
    /// </summary>
    public string AbacPolicyCode { get; }
}
