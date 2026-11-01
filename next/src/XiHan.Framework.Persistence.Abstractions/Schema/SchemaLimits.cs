// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Schema;

/// <summary>
/// 库结构读取的容量上限。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class SchemaLimits
{
    /// <summary>
    /// <see cref="ISchemaReader.GetTablesAsync"/> 一次最多读几张表的结构：100。
    /// </summary>
    /// <remarks>
    /// 读列是每张表一次查询，上限约束的是一次调用的往返次数与耗时；超过返回 <see cref="PersistenceErrors.TakeExceeded"/>，分批读。列表本身按 <see cref="TableListQuery.Take"/> 分页（最多 1000）。
    /// </remarks>
    public const int MaxTablesPerRead = 100;
}
