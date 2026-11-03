// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Schema;

/// <summary>
/// 一列
/// </summary>
/// <remarks>
/// 值按数据库报告的为准，几处归一（MySQL、SQL Server、Oracle 上实测，ADR-0084）：主键列报告为不可空（SQLite 对 <c>INTEGER PRIMARY KEY</c> 报告可空）；<c>decimal</c> / <c>numeric</c> / <c>number</c> / <c>money</c> 族给精度与小数位、不给长度，只有字符与二进制类型给长度，其余类型两者都没有；Oracle 读数据字典，类型名是数据库类型名；数据库没有的项（长度为 0、空注释、空缺省值）为 <see langword="null"/>
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ColumnInfo
{
    /// <summary>
    /// 列名（库里的写法）
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 数据库报告的类型名，不含长度与精度
    /// </summary>
    /// <remarks>
    /// 如 <c>varchar</c>、<c>int8</c>、<c>numeric</c>、<c>TEXT</c>、<c>VARCHAR2</c>、<c>NUMBER</c>、<c>TIMESTAMP</c>；Oracle 也是数据库类型名，不是 CLR 类型名
    /// </remarks>
    public required string DataType { get; init; }

    /// <summary>
    /// 在表里的序号（从 0 起）
    /// </summary>
    public int Ordinal { get; init; }

    /// <summary>
    /// 是否是主键（的一部分）
    /// </summary>
    public bool IsPrimaryKey { get; init; }

    /// <summary>
    /// 是否自增
    /// </summary>
    /// <remarks>
    /// identity（SQL Server、PostgreSQL、Oracle 12c 起的 <c>GENERATED … AS IDENTITY</c>）、PostgreSQL 的 serial、MySQL 的 <c>AUTO_INCREMENT</c>、SQLite 的 <c>AUTOINCREMENT</c>（只认这个关键字，单凭 <c>INTEGER PRIMARY KEY</c> 不算）
    /// </remarks>
    public bool IsIdentity { get; init; }

    /// <summary>
    /// 是否可空
    /// </summary>
    /// <remarks>
    /// 主键列一律为 <see langword="false"/>（SQLite 对 <c>INTEGER PRIMARY KEY</c> 报告可空，读取时按主键改正）
    /// </remarks>
    public bool IsNullable { get; init; }

    /// <summary>
    /// 字符与二进制类型的声明长度
    /// </summary>
    /// <remarks>
    /// 只给 <c>char</c>、<c>nchar</c>、<c>varchar</c>、<c>nvarchar</c>、<c>varchar2</c>、<c>nvarchar2</c>、<c>character</c>、<c>character varying</c>、<c>bpchar</c>、<c>binary</c>、<c>varbinary</c>、<c>raw</c>；Oracle 的字符类型按字符数（<c>NVARCHAR2(30)</c> 是 30）。没有声明长度、没有上限（<c>nvarchar(max)</c>、<c>text</c>、<c>CLOB</c>）与其余类型都为 <see langword="null"/>，在每种数据库上都一样：整数的显示宽度（MySQL <c>tinyint(1)</c>）、日期的小数秒位数（MySQL <c>datetime(6)</c>）、<c>bit</c>、浮点，以及 SQL Server 给 <c>int</c>、<c>bigint</c>、<c>float</c>、<c>datetime2</c> 报告的位数，都不当长度
    /// </remarks>
    public int? Length { get; init; }

    /// <summary>
    /// 定点数的精度（总位数）
    /// </summary>
    /// <remarks>
    /// 只给定点数族（<c>decimal</c>、<c>dec</c>、<c>numeric</c>、<c>number</c>、<c>money</c>、<c>smallmoney</c>）；不带精度的 Oracle <c>NUMBER</c> 与其余类型为 <see langword="null"/>
    /// </remarks>
    public int? Precision { get; init; }

    /// <summary>
    /// 定点数的小数位
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Precision"/> 同时给出（没有小数位为 0）；<see cref="Precision"/> 为 <see langword="null"/> 时也为 <see langword="null"/>
    /// </remarks>
    public int? Scale { get; init; }

    /// <summary>
    /// 列注释
    /// </summary>
    /// <remarks>
    /// 数据库没有或不支持（SQLite）时为 <see langword="null"/>
    /// </remarks>
    public string? Comment { get; init; }

    /// <summary>
    /// 缺省值（数据库报告的原文）
    /// </summary>
    /// <remarks>
    /// 写法随数据库，不做规整：如 PostgreSQL <c>nextval('…'::regclass)</c>、<c>'x'::character varying</c>，MySQL <c>x</c>，SQL Server <c>('x')</c>，Oracle 与 SQLite <c>'x'</c>；没有为 <see langword="null"/>
    /// </remarks>
    public string? DefaultValue { get; init; }
}
