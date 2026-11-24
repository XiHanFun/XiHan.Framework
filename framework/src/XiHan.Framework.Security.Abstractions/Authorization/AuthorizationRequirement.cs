// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Authorization;

/// <summary>
/// 一次调用的授权要求：策略名与可选的资源
/// </summary>
/// <remarks>
/// 策略名是必填的初始化成员，资源是可选的初始化成员，没有位置构造与解构：以后加字段只加可选的初始化成员，不破坏已有的构造处（ADR-0142、ADR-0288）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record AuthorizationRequirement
{
    /// <summary>
    /// 策略名
    /// </summary>
    /// <remarks>
    /// <c>permission:&lt;code&gt;</c> 映射为权限策略
    /// </remarks>
    /// <exception cref="ArgumentException">为空或空白</exception>
    public required string Policy
    {
        get;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The policy name must not be empty.", nameof(value));
    }

    /// <summary>
    /// 资源名
    /// </summary>
    /// <remarks>
    /// 无具体资源时为 <see langword="null"/>
    /// </remarks>
    public ResourceName? Resource { get; init; }

    /// <summary>
    /// 只要求已认证
    /// </summary>
    public static AuthorizationRequirement Authenticated { get; } = new() { Policy = AuthenticatedPolicy.PolicyName };

    /// <summary>
    /// 要求权限
    /// </summary>
    /// <remarks>
    /// <c>resource</c> 取 <c>null</c>
    /// </remarks>
    public static AuthorizationRequirement Permission(string code)
        => Permission(code, null);

    /// <summary>
    /// 要求权限
    /// </summary>
    public static AuthorizationRequirement Permission(string code, ResourceName? resource) => new() { Policy = PermissionPolicy.NameFor(code), Resource = resource };
}
