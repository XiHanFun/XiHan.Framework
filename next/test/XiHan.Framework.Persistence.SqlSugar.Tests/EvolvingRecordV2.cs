// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 演进后的表结构。
/// </summary>
/// <remarks>
/// 同一张表加了非可空的枚举、整数、长整数、小数、布尔、Guid 列，以及可空的字符串与时刻列。
/// </remarks>
[SugarTable("evolving_record")]
public sealed class EvolvingRecordV2
{
    /// <summary>
    /// 主键。
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 名字。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 枚举（按整数存）。
    /// </summary>
    public EvolvingKind Kind { get; set; }

    /// <summary>
    /// 整数。
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// 长整数。
    /// </summary>
    public long Total { get; set; }

    /// <summary>
    /// 小数。
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// 布尔。
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Guid。
    /// </summary>
    public Guid Ref { get; set; }

    /// <summary>
    /// 可空字符串：非可空字符串加到已有行的表上不受支持，新字符串列应声明可空。
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// 可空时刻：日期时间类型没有可移植的缺省值，新时刻列应声明可空。
    /// </summary>
    public DateTimeOffset? At { get; set; }
}
