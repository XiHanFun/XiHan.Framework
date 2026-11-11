// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Permissions;

/// <summary>
/// 授予管理的单条便捷方法
/// </summary>
/// <remarks>
/// 单条是批量的特例，转调批量方法取第一项（批量优先第 1 条）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class PermissionGrantAdministrationExtensions
{
    /// <summary>
    /// 授予一条
    /// </summary>
    public static async ValueTask<Result> GrantAsync(this IPermissionGrantAdministration grants, PermissionGrant grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(grant);
        var results = await grants.GrantAsync([grant], cancellationToken).ConfigureAwait(false);
        return results[0];
    }

    /// <summary>
    /// 撤销一条
    /// </summary>
    /// <remarks>
    /// 不存在返回 <see cref="SecurityErrors.GrantNotFound"/>
    /// </remarks>
    public static async ValueTask<Result> RevokeAsync(this IPermissionGrantAdministration grants, PermissionGrant grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(grant);
        var results = await grants.RevokeAsync([grant], cancellationToken).ConfigureAwait(false);
        return results[0];
    }
}
