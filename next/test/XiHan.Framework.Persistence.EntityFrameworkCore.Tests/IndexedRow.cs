// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// 声明了唯一索引与普通索引的实体
/// </summary>
/// <remarks>
/// <see cref="IndexedDbContext"/> 把它映射到表 <c>indexed_row</c>：索引名按此刻的表名推出
/// </remarks>
[EntityIndex(nameof(TenantId), nameof(Code), IsUnique = true)]
[EntityIndex(nameof(Category), nameof(At))]
public sealed class IndexedRow : IEntity<Guid>
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <summary>
    /// 租户
    /// </summary>
    [StringLength(64)]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// 编码：与租户共同唯一
    /// </summary>
    [StringLength(64)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 分类
    /// </summary>
    [StringLength(64)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 时刻
    /// </summary>
    public DateTimeOffset At { get; set; }
}
