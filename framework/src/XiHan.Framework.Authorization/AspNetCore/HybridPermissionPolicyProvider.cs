// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

    /// <summary>
    /// 按策略名动态构建混合权限策略，无法解析为混合策略名时交由默认提供器处理
    /// </summary>
    /// <param name="policyName">策略名称</param>
    /// <returns>解析出的授权策略，不存在返回 null</returns>
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

    /// <summary>
    /// 取得默认授权策略，直接委托给默认提供器
    /// </summary>
    /// <returns>默认授权策略</returns>
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _fallbackProvider.GetDefaultPolicyAsync();
    }

    /// <summary>
    /// 取得兜底授权策略，直接委托给默认提供器
    /// </summary>
    /// <returns>兜底授权策略，未配置返回 null</returns>
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _fallbackProvider.GetFallbackPolicyAsync();
    }
}
