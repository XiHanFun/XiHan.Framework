// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using SqlSugar;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 与 <see cref="SameIndexNameRecordB"/> 显式给了同一个索引名的实体
/// </summary>
/// <remarks>
/// PostgreSQL、SQLite 与 Oracle 的索引名按架构唯一，两张表一起建表时 <c>InitTables</c> 在建表之前报错（ADR-0249）
/// </remarks>
[SugarTable("same_index_name_record_a")]
[EntityIndex(nameof(Code), Name = "ix_same_index_name_code")]
public sealed class SameIndexNameRecordA : IEntity<Guid>
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <summary>
    /// 编码
    /// </summary>
    [StringLength(64)]
    public string Code { get; set; } = string.Empty;
}
