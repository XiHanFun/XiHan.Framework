// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 声明了中立索引的分表实体：约定不支持，映射时报错。
/// </summary>
[SugarTable("split_indexed_{year}{month}{day}")]
[SplitTable(SplitType.Year)]
[EntityIndex(nameof(RecordedAt))]
public sealed class SplitIndexedRecord : IEntity<Guid>
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <summary>
    /// 分表字段。
    /// </summary>
    [SplitField]
    public DateTime RecordedAt { get; set; }
}
