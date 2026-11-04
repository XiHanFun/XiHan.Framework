// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using SqlSugar;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 声明了唯一索引与普通索引的实体
/// </summary>
/// <remarks>
/// 表名由 <c>[SugarTable]</c> 改成 <c>indexed_record</c>：索引名按映射出的表名推出，不按类型名
/// </remarks>
[SugarTable("indexed_record")]
[EntityIndex(nameof(TenantId), nameof(Code), IsUnique = true)]
[EntityIndex(nameof(Category), nameof(At))]
public sealed class IndexedRecord : IEntity<Guid>
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
