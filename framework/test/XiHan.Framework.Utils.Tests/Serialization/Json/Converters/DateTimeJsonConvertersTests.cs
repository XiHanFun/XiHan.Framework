// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Serialization.Json.Converters;

namespace XiHan.Framework.Utils.Tests.Serialization.Json.Converters;

/// <summary>
/// 日期时间类 JSON 转换器测试
/// </summary>
/// <remarks>
/// 涉及时区换算的用例一律用 DateTimeOffset 或显式 IANA 时区来构造，
/// 避免依赖运行机器的本地时区；IANA 时区数据不可用时整组跳过而不是误报失败。
/// </remarks>
public class DateTimeJsonConvertersTests
{
    /// <summary>
    /// 探测运行环境能否解析 IANA 时区标识，探测一次后缓存
    /// </summary>
    private static readonly Lazy<bool> IanaTimeZoneAvailable = new(() =>
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    });

    /// <summary>
    /// 用给定转换器构造一个干净的序列化选项
    /// </summary>
    /// <remarks>
    /// 编码器必须与框架标准路径保持一致：JsonSerializeOptions.Encoder 默认、
    /// JsonConverterFactory.CreateOptions 都显式使用 UnsafeRelaxedJsonEscaping。
    /// 若这里不设 Encoder 就会退回 JavaScriptEncoder.Default，把 DateTimeOffset 偏移量里的 '+'
    /// 转义成 U+002B 的六字符形式，断言的就不再是转换器的输出而是测试自己挑的编码器行为。
    /// </remarks>
    /// <param name="converters">要挂载的转换器</param>
    private static JsonSerializerOptions CreateOptions(params JsonConverter[] converters)
    {
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        foreach (var converter in converters)
        {
            options.Converters.Add(converter);
        }

        return options;
    }

    /// <summary>
    /// DateOnly 默认按 yyyy-MM-dd 写出并可读回
    /// </summary>
    [Fact]
    public void DateOnlyConverter_WithDefaultFormat_RoundTrips()
    {
        var options = CreateOptions(new DateOnlyJsonConverter());

        Assert.Equal("\"2024-05-06\"", JsonSerializer.Serialize(new DateOnly(2024, 5, 6), options));
        Assert.Equal(new DateOnly(2024, 5, 6), JsonSerializer.Deserialize<DateOnly>("\"2024-05-06\"", options));
    }

    /// <summary>
    /// DateOnly 使用构造时指定的自定义格式写出
    /// </summary>
    [Fact]
    public void DateOnlyConverter_WithCustomFormat_UsesIt()
    {
        var options = CreateOptions(new DateOnlyJsonConverter("yyyyMMdd"));

        Assert.Equal("\"20240506\"", JsonSerializer.Serialize(new DateOnly(2024, 5, 6), options));
    }

    /// <summary>
    /// DateOnly 遇到非字符串标记回落为默认值
    /// </summary>
    [Fact]
    public void DateOnlyConverter_WhenTokenNotString_FallsBackToDefault()
    {
        var options = CreateOptions(new DateOnlyJsonConverter());

        Assert.Equal(default(DateOnly), JsonSerializer.Deserialize<DateOnly>("123", options));
        Assert.Equal(default(DateOnly), JsonSerializer.Deserialize<DateOnly>("\"不是日期\"", options));
    }

    /// <summary>
    /// 可空 DateOnly 的 null 与有值往返
    /// </summary>
    [Fact]
    public void DateOnlyNullableConverter_RoundTripsNullAndValue()
    {
        var options = CreateOptions(new DateOnlyNullableConverter("yyyyMMdd"));

        Assert.Equal("null", JsonSerializer.Serialize<DateOnly?>(null, options));
        Assert.Equal("\"20240506\"", JsonSerializer.Serialize<DateOnly?>(new DateOnly(2024, 5, 6), options));
        Assert.Null(JsonSerializer.Deserialize<DateOnly?>("null", options));
        Assert.Null(JsonSerializer.Deserialize<DateOnly?>("123", options));
        Assert.Equal(new DateOnly(2024, 5, 6), JsonSerializer.Deserialize<DateOnly?>("\"2024-05-06\"", options));
    }

    /// <summary>
    /// TimeOnly 默认按 HH:mm:ss 写出并可读回
    /// </summary>
    [Fact]
    public void TimeOnlyConverter_WithDefaultFormat_RoundTrips()
    {
        var options = CreateOptions(new TimeOnlyJsonConverter());

        Assert.Equal("\"07:08:09\"", JsonSerializer.Serialize(new TimeOnly(7, 8, 9), options));
        Assert.Equal(new TimeOnly(7, 8, 9), JsonSerializer.Deserialize<TimeOnly>("\"07:08:09\"", options));
    }

    /// <summary>
    /// TimeOnly 使用构造时指定的自定义格式写出
    /// </summary>
    [Fact]
    public void TimeOnlyConverter_WithCustomFormat_UsesIt()
    {
        var options = CreateOptions(new TimeOnlyJsonConverter("HHmmss"));

        Assert.Equal("\"070809\"", JsonSerializer.Serialize(new TimeOnly(7, 8, 9), options));
    }

    /// <summary>
    /// TimeOnly 遇到非字符串标记回落为默认值
    /// </summary>
    [Fact]
    public void TimeOnlyConverter_WhenTokenNotString_FallsBackToDefault()
    {
        var options = CreateOptions(new TimeOnlyJsonConverter());

        Assert.Equal(default(TimeOnly), JsonSerializer.Deserialize<TimeOnly>("123", options));
    }

    /// <summary>
    /// 可空 TimeOnly 的 null 与有值往返
    /// </summary>
    [Fact]
    public void TimeOnlyNullableConverter_RoundTripsNullAndValue()
    {
        var options = CreateOptions(new TimeOnlyNullableConverter());

        Assert.Equal("null", JsonSerializer.Serialize<TimeOnly?>(null, options));
        Assert.Equal("\"07:08:09\"", JsonSerializer.Serialize<TimeOnly?>(new TimeOnly(7, 8, 9), options));
        Assert.Null(JsonSerializer.Deserialize<TimeOnly?>("null", options));
        Assert.Equal(new TimeOnly(7, 8, 9), JsonSerializer.Deserialize<TimeOnly?>("\"07:08:09\"", options));
    }

    /// <summary>
    /// DateTime 默认按 yyyy-MM-dd HH:mm:ss 写出并可读回
    /// </summary>
    [Fact]
    public void DateTimeConverter_WithDefaultFormat_RoundTrips()
    {
        var options = CreateOptions(new DateTimeJsonConverter());
        var value = new DateTime(2024, 5, 6, 7, 8, 9);

        var json = JsonSerializer.Serialize(value, options);

        Assert.Equal("\"2024-05-06 07:08:09\"", json);
        Assert.Equal(value, JsonSerializer.Deserialize<DateTime>(json, options));
    }

    /// <summary>
    /// DateTime 遇到非字符串标记回落为默认值
    /// </summary>
    [Fact]
    public void DateTimeConverter_WhenTokenNotString_FallsBackToDefault()
    {
        var options = CreateOptions(new DateTimeJsonConverter());

        Assert.Equal(default(DateTime), JsonSerializer.Deserialize<DateTime>("123", options));
        Assert.Equal(default(DateTime), JsonSerializer.Deserialize<DateTime>("\"不是时间\"", options));
    }

    /// <summary>
    /// 时区解析器返回空值时不做换算，按原值输出
    /// </summary>
    [Fact]
    public void DateTimeConverter_WhenResolverReturnsNull_KeepsRawValue()
    {
        var options = CreateOptions(new DateTimeJsonConverter("yyyy-MM-dd HH:mm:ss", false, () => null));

        Assert.Equal("\"2024-05-06 00:00:00\"", JsonSerializer.Serialize(new DateTime(2024, 5, 6), options));
    }

    /// <summary>
    /// 时区标识非法时回退为原值而不是抛异常
    /// </summary>
    [Fact]
    public void DateTimeConverter_WhenTimeZoneInvalid_FallsBackToRawValue()
    {
        var options = CreateOptions(new DateTimeJsonConverter("yyyy-MM-dd HH:mm:ss", false, () => "不存在的时区"));

        Assert.Equal("\"2024-05-06 00:00:00\"", JsonSerializer.Serialize(new DateTime(2024, 5, 6), options));
    }

    /// <summary>
    /// 提供有效 IANA 时区时，存储的 UTC 时间被换算为用户本地时间
    /// </summary>
    [Fact]
    public void DateTimeConverter_WithTimeZoneResolver_ConvertsStoredUtcToUserTime()
    {
        Assert.SkipUnless(IanaTimeZoneAvailable.Value, "当前运行环境无法解析 IANA 时区标识（Asia/Shanghai），跳过该组验证。");

        var options = CreateOptions(new DateTimeJsonConverter("yyyy-MM-dd HH:mm:ss", false, () => "Asia/Shanghai"));

        // 存储约定为 UTC，Asia/Shanghai 固定 +08:00，无夏令时
        Assert.Equal("\"2024-05-06 08:00:00\"", JsonSerializer.Serialize(new DateTime(2024, 5, 6), options));
    }

    /// <summary>
    /// 可空 DateTime 的 null 与有值往返，且同样支持时区换算
    /// </summary>
    [Fact]
    public void DateTimeNullableConverter_RoundTripsNullAndValue()
    {
        var options = CreateOptions(new DateTimeNullableConverter());

        Assert.Equal("null", JsonSerializer.Serialize<DateTime?>(null, options));
        Assert.Equal("\"2024-05-06 07:08:09\"", JsonSerializer.Serialize<DateTime?>(new DateTime(2024, 5, 6, 7, 8, 9), options));
        Assert.Null(JsonSerializer.Deserialize<DateTime?>("null", options));
        Assert.Equal(new DateTime(2024, 5, 6, 7, 8, 9), JsonSerializer.Deserialize<DateTime?>("\"2024-05-06 07:08:09\"", options));
    }

    /// <summary>
    /// DateTimeOffset 默认按 ISO 8601 带偏移写出并可无损读回
    /// </summary>
    [Fact]
    public void DateTimeOffsetConverter_WithDefaultFormat_RoundTripsWithOffset()
    {
        var options = CreateOptions(new DateTimeOffsetJsonConverter());
        var value = new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.FromHours(8));

        var json = JsonSerializer.Serialize(value, options);

        Assert.Equal("\"2024-05-06T07:08:09+08:00\"", json);

        var restored = JsonSerializer.Deserialize<DateTimeOffset>(json, options);
        Assert.Equal(value, restored);
        Assert.Equal(TimeSpan.FromHours(8), restored.Offset);
    }

    /// <summary>
    /// isUtc 为真时写入与读取都归一到 UTC 偏移
    /// </summary>
    [Fact]
    public void DateTimeOffsetConverter_WhenIsUtc_NormalizesToUtc()
    {
        var options = CreateOptions(new DateTimeOffsetJsonConverter("yyyy-MM-ddTHH:mm:sszzz", true));

        var json = JsonSerializer.Serialize(new DateTimeOffset(2024, 5, 6, 8, 0, 0, TimeSpan.FromHours(8)), options);
        Assert.Equal("\"2024-05-06T00:00:00+00:00\"", json);

        var restored = JsonSerializer.Deserialize<DateTimeOffset>("\"2024-05-06T08:00:00+08:00\"", options);
        Assert.Equal(TimeSpan.Zero, restored.Offset);
        Assert.Equal(new DateTimeOffset(2024, 5, 6, 0, 0, 0, TimeSpan.Zero), restored);
    }

    /// <summary>
    /// DateTimeOffset 遇到非字符串标记回落为默认值
    /// </summary>
    [Fact]
    public void DateTimeOffsetConverter_WhenTokenNotString_FallsBackToDefault()
    {
        var options = CreateOptions(new DateTimeOffsetJsonConverter());

        Assert.Equal(default(DateTimeOffset), JsonSerializer.Deserialize<DateTimeOffset>("123", options));
    }

    /// <summary>
    /// 提供有效 IANA 时区时，输出换算后的无偏移墙钟字符串
    /// </summary>
    [Fact]
    public void DateTimeOffsetConverter_WithTimeZoneResolver_WritesWallClockWithoutOffset()
    {
        Assert.SkipUnless(IanaTimeZoneAvailable.Value, "当前运行环境无法解析 IANA 时区标识（Asia/Shanghai），跳过该组验证。");

        var options = CreateOptions(new DateTimeOffsetJsonConverter("yyyy-MM-ddTHH:mm:sszzz", false, () => "Asia/Shanghai"));

        var json = JsonSerializer.Serialize(new DateTimeOffset(2024, 5, 6, 0, 0, 0, TimeSpan.Zero), options);

        Assert.Equal("\"2024-05-06 08:00:00\"", json);
    }

    /// <summary>
    /// 时区标识非法时 DateTimeOffset 回退到默认的 ISO 带偏移输出
    /// </summary>
    [Fact]
    public void DateTimeOffsetConverter_WhenTimeZoneInvalid_FallsBackToIsoFormat()
    {
        var options = CreateOptions(new DateTimeOffsetJsonConverter("yyyy-MM-ddTHH:mm:sszzz", false, () => "不存在的时区"));

        var json = JsonSerializer.Serialize(new DateTimeOffset(2024, 5, 6, 0, 0, 0, TimeSpan.Zero), options);

        Assert.Equal("\"2024-05-06T00:00:00+00:00\"", json);
    }

    /// <summary>
    /// 可空 DateTimeOffset 的 null 与有值往返
    /// </summary>
    [Fact]
    public void DateTimeOffsetNullableConverter_RoundTripsNullAndValue()
    {
        var options = CreateOptions(new DateTimeOffsetNullableConverter());
        var value = new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.FromHours(8));

        Assert.Equal("null", JsonSerializer.Serialize<DateTimeOffset?>(null, options));
        Assert.Equal("\"2024-05-06T07:08:09+08:00\"", JsonSerializer.Serialize<DateTimeOffset?>(value, options));
        Assert.Null(JsonSerializer.Deserialize<DateTimeOffset?>("null", options));
        Assert.Equal(value, JsonSerializer.Deserialize<DateTimeOffset?>("\"2024-05-06T07:08:09+08:00\"", options));
    }
}
