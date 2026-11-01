// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Schema;

/// <summary>
/// 一列。
/// </summary>
/// <remarks>
/// 值按数据库报告的为准，几处归一：主键列报告为不可空（SQLite 对 <c>INTEGER PRIMARY KEY</c> 报告可空）；<c>decimal</c> / <c>numeric</c> / <c>number</c> / <c>money</c> 族给精度与小数位、不给长度，其余类型给长度；数据库没有的项（长度为 0、空注释、空缺省值）为 <see langword="null"/>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ColumnInfo
{
    /// <summary>
    /// 列名（库里的写法）。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 数据库报告的类型名（如 <c>varchar</c>、<c>int8</c>、<c>numeric</c>、<c>TEXT</c>），不含长度与精度。
    /// </summary>
    public required string DataType { get; init; }

    /// <summary>
    /// 在表里的序号（从 0 起）。
    /// </summary>
    public int Ordinal { get; init; }

    /// <summary>
    /// 是否是主键（的一部分）。
    /// </summary>
    public bool IsPrimaryKey { get; init; }

    /// <summary>
    /// 是否自增（identity、serial、<c>AUTOINCREMENT</c>）。
    /// </summary>
    public bool IsIdentity { get; init; }

    /// <summary>
    /// 是否可空（主键列总是 <see langword="false"/>）。
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// 长度（定点数族以外的类型）。
    /// </summary>
    /// <remarks>
    /// 字符与二进制类型是声明的长度；没有声明、不限长或不适用时为 <see langword="null"/>（SQLite、PostgreSQL 上整数与日期等类型为 <see langword="null"/>，其他数据库可能报告字节数或显示宽度）。
    /// </remarks>
    public int? Length { get; init; }

    /// <summary>
    /// 定点数的精度（总位数）。
    /// </summary>
    /// <remarks>
    /// 不适用时为 <see langword="null"/>。
    /// </remarks>
    public int? Precision { get; init; }

    /// <summary>
    /// 定点数的小数位。
    /// </summary>
    /// <remarks>
    /// 不适用时为 <see langword="null"/>。
    /// </remarks>
    public int? Scale { get; init; }

    /// <summary>
    /// 列注释。
    /// </summary>
    /// <remarks>
    /// 数据库没有或不支持（SQLite）时为 <see langword="null"/>。
    /// </remarks>
    public string? Comment { get; init; }

    /// <summary>
    /// 缺省值（数据库报告的原文）。
    /// </summary>
    /// <remarks>
    /// 如 <c>'x'</c>、<c>nextval('…'::regclass)</c>、<c>'x'::character varying</c>；没有为 <see langword="null"/>。
    /// </remarks>
    public string? DefaultValue { get; init; }
}
