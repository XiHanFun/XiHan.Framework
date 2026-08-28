// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;
using XiHan.Framework.Authorization.Abac;

namespace XiHan.Framework.Authorization.Tests.Infrastructure;

/// <summary>
/// ABAC 属性收集器替身
/// </summary>
/// <remarks>
/// 返回固定属性快照，并记录调用参数，用于验证混合授权处理器是否把权限编码、策略编码与资源如实透传下去。
/// </remarks>
public sealed class FakeAbacAttributeCollector : IAbacAttributeCollector
{
    /// <summary>
    /// 调用次数
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// 最近一次收到的用户主体
    /// </summary>
    public ClaimsPrincipal? LastPrincipal { get; private set; }

    /// <summary>
    /// 最近一次收到的资源对象
    /// </summary>
    public object? LastResource { get; private set; }

    /// <summary>
    /// 最近一次收到的权限编码
    /// </summary>
    public string? LastPermissionCode { get; private set; }

    /// <summary>
    /// 最近一次收到的策略编码
    /// </summary>
    public string? LastPolicyCode { get; private set; }

    /// <summary>
    /// 固定返回的属性快照
    /// </summary>
    public AbacAttributeSet Result { get; set; } = new();

    /// <summary>
    /// 收集 ABAC 属性
    /// </summary>
    /// <param name="principal">用户主体</param>
    /// <param name="resource">资源对象</param>
    /// <param name="permissionCode">权限编码</param>
    /// <param name="policyCode">策略编码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>属性快照</returns>
    public Task<AbacAttributeSet> CollectAsync(
        ClaimsPrincipal principal,
        object? resource,
        string permissionCode,
        string policyCode,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastPrincipal = principal;
        LastResource = resource;
        LastPermissionCode = permissionCode;
        LastPolicyCode = policyCode;
        return Task.FromResult(Result);
    }
}
