// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 安全上下文。携带当前用户、租户、权限集等与安全相关的信息。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public class SecurityContext
{
    /// <summary>
    /// 当前用户标识。
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// 当前用户名。
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 当前租户标识。
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// 当前用户拥有的权限码集合。
    /// </summary>
    public IReadOnlySet<string> Permissions { get; set; } = new HashSet<string>();

    /// <summary>
    /// 当前用户拥有的角色集合。
    /// </summary>
    public IReadOnlySet<string> Roles { get; set; } = new HashSet<string>();

    /// <summary>
    /// 是否已认证。
    /// </summary>
    public bool IsAuthenticated => UserId is not null;
}
