// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;
using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 按月分表的实体。
/// </summary>
/// <remarks>
/// 表名带 SqlSugar 的 <c>{year}{month}{day}</c> 占位符，<c>[SplitField]</c> 是 UTC 的 <see cref="DateTime"/> 列；带创建时间与并发戳协议。
/// </remarks>
[SugarTable("split_records_{year}{month}{day}")]
[SplitTable(SplitType.Month)]
public sealed class SplitTableRecord : IEntity<Guid>, IHasCreationTime, IHasConcurrencyStamp
{
    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <summary>
    /// 记录时刻（UTC）。
    /// </summary>
    /// <remarks>
    /// SqlSugar 据此决定落在哪张分表。
    /// </remarks>
    [SplitField]
    public DateTime RecordedAt { get; set; }

    /// <summary>
    /// 内容。
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; }

    /// <inheritdoc />
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
