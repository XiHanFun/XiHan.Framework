// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Permissions;

/// <summary>
/// 一条权限授予。<see cref="TenantId"/> 为 <see langword="null"/> 时对所有租户生效，否则只对该租户生效。
/// </summary>
/// <param name="Subject">主体。</param>
/// <param name="Permission">权限码，或 <see cref="All"/>。</param>
/// <param name="TenantId">租户；<see langword="null"/> 为全局。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record PermissionGrant(GrantSubject Subject, string Permission, string? TenantId = null)
{
    /// <summary>
    /// 通配：全部权限。
    /// </summary>
    public const string All = "*";

    /// <summary>
    /// 权限码。
    /// </summary>
    public string Permission { get; } = !string.IsNullOrWhiteSpace(Permission) ? Permission : throw new ArgumentException("The permission code must not be empty.", nameof(Permission));
}
