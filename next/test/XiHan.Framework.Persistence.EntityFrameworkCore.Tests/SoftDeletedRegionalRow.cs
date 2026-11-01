// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 键里有可空列的软删除实体。
/// </summary>
/// <remarks>
/// 部分唯一索引（编码、区域）在 SQL Server 上的条件要另加区域 <c>IS NOT NULL</c>。
/// </remarks>
[EntityIndex(nameof(Code), nameof(Region), IsUnique = true)]
public sealed class SoftDeletedRegionalRow : IEntity<Guid>, ISoftDeletable
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <summary>
    /// 编码。
    /// </summary>
    [StringLength(64)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 区域。
    /// </summary>
    /// <remarks>
    /// 没有区域的行不参与唯一性。
    /// </remarks>
    [StringLength(32)]
    public string? Region { get; set; }

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAt { get; set; }
}
