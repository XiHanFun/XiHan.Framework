// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Schema;

/// <summary>
/// 一张表（不含列）
/// </summary>
/// <remarks>
/// 按时间分表的实体的各个物理表折叠成一张逻辑表：<see cref="Name"/> 是逻辑名，<see cref="PhysicalTables"/> 列出物理表（新到旧），<see cref="Comment"/> 取最新的物理表
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record TableInfo
{
    /// <summary>
    /// 表名（分表是逻辑名）
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 表注释
    /// </summary>
    /// <remarks>
    /// 数据库没有或不支持（SQLite）时为 <see langword="null"/>
    /// </remarks>
    public string? Comment { get; init; }

    /// <summary>
    /// 物理表：普通表是它自己，分表新到旧
    /// </summary>
    public required IReadOnlyList<string> PhysicalTables { get; init; }

    /// <summary>
    /// 是否是折叠出的分表
    /// </summary>
    public bool IsSplit { get; init; }
}
