// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Conformance.Fixtures;

/// <summary>
/// 套件实体：多租户。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class ConformanceNote : IEntity<Guid>, IMultiTenant
{
    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <inheritdoc />
    public string TenantId { get; set; } = string.Empty;

    /// <summary>内容。</summary>
    public string Text { get; set; } = string.Empty;
}
