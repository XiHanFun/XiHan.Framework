#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HybridPermissionPolicyProvider
// Guid:8e380a58-745f-43d6-bf6d-7f5ec18408df
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/10 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace XiHan.Framework.Authorization.AspNetCore;

/// <summary>
/// 混合权限策略提供器
/// </summary>
public class HybridPermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">授权配置</param>
    public HybridPermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    /// <inheritdoc />
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!HybridAuthorizationPolicyName.TryParse(policyName, out var permissionCode, out var abacPolicyCode))
        {
            return _fallbackProvider.GetPolicyAsync(policyName);
        }

        var policy = new AuthorizationPolicyBuilder()
            .AddRequirements(new HybridPermissionRequirement(permissionCode, abacPolicyCode))
            .Build();
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }

    /// <inheritdoc />
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _fallbackProvider.GetDefaultPolicyAsync();
    }

    /// <inheritdoc />
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _fallbackProvider.GetFallbackPolicyAsync();
    }
}
