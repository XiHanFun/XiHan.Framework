// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Permissions;

/// <summary>
/// 授予列表的一页
/// </summary>
/// <remarks>
/// 偏移分页的结果页（RFC-0001「分页」）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record PermissionGrantPage
{
    /// <summary>
    /// 本页的授予
    /// </summary>
    public required IReadOnlyList<PermissionGrant> Items { get; init; }

    /// <summary>
    /// 满足过滤的总数
    /// </summary>
    /// <remarks>
    /// 不受分页影响
    /// </remarks>
    public required long Total { get; init; }
}
