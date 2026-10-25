// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Authorization;

/// <summary>
/// 一次调用的授权要求：策略名与可选的资源。
/// </summary>
/// <param name="Policy">策略名；<c>permission:&lt;code&gt;</c> 映射为权限策略。</param>
/// <param name="Resource">资源名；无具体资源时为 <see langword="null"/>。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record AuthorizationRequirement(string Policy, ResourceName? Resource = null)
{
    /// <summary>
    /// 策略名。
    /// </summary>
    public string Policy { get; } = !string.IsNullOrWhiteSpace(Policy) ? Policy : throw new ArgumentException("The policy name must not be empty.", nameof(Policy));

    /// <summary>
    /// 只要求已认证。
    /// </summary>
    public static AuthorizationRequirement Authenticated { get; } = new(AuthenticatedPolicy.PolicyName);

    /// <summary>
    /// 要求权限。
    /// </summary>
    public static AuthorizationRequirement Permission(string code, ResourceName? resource = null) => new(PermissionPolicy.NameFor(code), resource);
}
