// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Schema;

/// <summary>
/// 一张表的结构
/// </summary>
/// <param name="Table">表；分表的列取最新的物理表</param>
/// <param name="Columns">列，按在表里的顺序</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record TableSchema(TableInfo Table, IReadOnlyList<ColumnInfo> Columns);
