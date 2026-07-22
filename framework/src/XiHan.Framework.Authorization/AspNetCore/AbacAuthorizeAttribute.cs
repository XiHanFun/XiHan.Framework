// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
