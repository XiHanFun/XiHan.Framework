// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Data;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// <see cref="DateTimeOffsetConverter"/>。
/// </summary>
/// <remarks>
/// 各驱动交出的日期时间形态都还原成同一个 UTC 时刻，参数按 UTC 写入。
/// </remarks>
public sealed class DateTimeOffsetConverterTests
{
    /// <summary>
    /// 一个 UTC 时刻。
    /// </summary>
    private static readonly DateTimeOffset _moment = new(2026, 10, 27, 19, 34, 39, TimeSpan.Zero);

    [Fact]
    public void QueryConverter_RestoresTheSameUtcInstant_FromEveryDriverShape()
    {
        var converter = new DateTimeOffsetConverter();

        // Npgsql 的 legacy 时间戳模式把 timestamptz 换算成本地时间、Kind=Local 交出：按 Kind 换回 UTC，而不是把本地墙钟当 UTC（那会多出本地时区偏移，回写后逐次累加）
        var local = _moment.UtcDateTime.ToLocalTime();
        Assert.Equal(DateTimeKind.Local, local.Kind);
        Assert.Equal(_moment, Read(converter, local));

        // Kind=Utc 与 Unspecified（SQL Server、MySQL 等直接交出存的值）按写入时的 UTC 解释
        Assert.Equal(_moment, Read(converter, DateTime.SpecifyKind(_moment.UtcDateTime, DateTimeKind.Utc)));
        Assert.Equal(_moment, Read(converter, DateTime.SpecifyKind(_moment.UtcDateTime, DateTimeKind.Unspecified)));

        // 驱动直接给 DateTimeOffset（任意偏移）与 SQLite 的文本
        Assert.Equal(_moment, Read(converter, _moment.ToOffset(TimeSpan.FromHours(8))));
        Assert.Equal(_moment, Read(converter, "2026-10-27 19:34:39"));
        Assert.Equal(_moment, Read(converter, "2026-10-27T19:34:39Z"));
        Assert.Equal(TimeSpan.Zero, Read(converter, local).Offset);

        // 空列
        Assert.Equal(default, Read(converter, DBNull.Value));
    }

    [Fact]
    public void ParameterConverter_WritesTheUtcInstant_WithUtcKind()
    {
        var converter = new DateTimeOffsetConverter();
        var parameter = converter.ParameterConverter<DateTimeOffset>(_moment.ToOffset(TimeSpan.FromHours(8)), 3);

        var written = Assert.IsType<DateTime>(parameter.Value);
        Assert.Equal(_moment.UtcDateTime, written);
        Assert.Equal(DateTimeKind.Utc, written.Kind);
        Assert.Equal("@xihan_dto_3", parameter.ParameterName);
        Assert.Equal(DBNull.Value, converter.ParameterConverter<DateTimeOffset>(null!, 0).Value);
    }

    /// <summary>
    /// 把一个值放进单列单行的表，经 <see cref="DataTableReader"/> 以 <see cref="IDataRecord"/> 的形态交给转换器。
    /// </summary>
    private static DateTimeOffset Read(DateTimeOffsetConverter converter, object value)
    {
        using var table = new DataTable();
        // object 列不做 DateTime 模式换算，Kind 原样保留
        table.Columns.Add("value", typeof(object));
        table.Rows.Add(value);
        using var reader = new DataTableReader(table);
        Assert.True(reader.Read());
        return converter.QueryConverter<DateTimeOffset>(reader, 0);
    }
}
