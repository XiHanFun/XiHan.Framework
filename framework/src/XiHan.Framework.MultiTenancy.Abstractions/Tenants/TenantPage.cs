// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tenants;

/// <summary>
/// 租户列表的一页
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record TenantPage
{
    /// <summary>
    /// 本页的租户，按 ID 升序
    /// </summary>
    public required IReadOnlyList<TenantInfo> Items { get; init; }

    /// <summary>
    /// 下一页的续页令牌
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 表示已经读完；不为 <see langword="null"/> 时下一页也可能为空
    /// </remarks>
    public string? ContinuationToken { get; init; }
}
