// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Localization;

namespace XiHan.Framework.Utils.Serialization.Json.Converters;

/// <summary>
/// DateTimeOffsetJsonConverter
/// <para>可选用户时区换算：构造时传入 timeZoneResolver（返回 IANA 时区，如 Asia/Shanghai；旧别名与 Windows ID 同样接受），
/// 序列化时把值换算为该时区并输出“无偏移墙钟”字符串（yyyy-MM-dd HH:mm:ss，与 DateTime 一致，
/// 便于前端按本地直接显示）；解析器为空或返回空时回退默认 ISO-8601 带偏移行为。
/// 返回的时区在当前系统找不到、也找不到等价时区时不换算，按 UTC 输出带 +00:00 的 ISO 8601（不随构造时的格式），
/// 与 <see cref="DateTimeJsonConverter"/> 一致。
/// 时区来源由调用方注入（如 Web 层按请求头解析），本转换器不依赖 HTTP。</para>
/// </summary>
public class DateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    /// <summary>
    /// 指定的用户时区解析不了时的输出格式（ISO 8601 带偏移，日期时间类转换器共用）
    /// </summary>
    internal const string UnresolvedTimeZoneFormat = "yyyy-MM-ddTHH:mm:sszzz";

    /// <summary>
    /// 用户时区换算后的输出格式（无偏移墙钟，与 DateTimeJsonConverter 一致）
    /// </summary>
    private const string UserTimeZoneFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly string _dateFormatString;
    private readonly bool _isUtc;
    private readonly Func<string?>? _timeZoneResolver;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DateTimeOffsetJsonConverter()
        : this("yyyy-MM-ddTHH:mm:sszzz", false)
    {
    }

    /// <summary>
    /// 构造函数（按用户时区换算，格式与 UTC 用默认）
    /// </summary>
    /// <param name="timeZoneResolver">当前用户时区（IANA）解析委托；返回 null/空表示不换算</param>
    public DateTimeOffsetJsonConverter(Func<string?> timeZoneResolver)
        : this("yyyy-MM-ddTHH:mm:sszzz", false, timeZoneResolver)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dateFormatString">无时区换算时的输出格式</param>
    /// <param name="isUtc">无时区换算时是否按 UTC 输出</param>
    /// <param name="timeZoneResolver">当前用户时区（IANA）解析委托；返回 null/空表示不换算</param>
    public DateTimeOffsetJsonConverter(string dateFormatString, bool isUtc, Func<string?>? timeZoneResolver = null)
    {
        _dateFormatString = dateFormatString;
        _isUtc = isUtc;
        _timeZoneResolver = timeZoneResolver;
    }

    /// <summary>
    /// 读
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="typeToConvert"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String when DateTimeOffset.TryParse(reader.GetString(), out var value) => _isUtc ? value.ToUniversalTime() : value,
            _ => default
        };
    }

    /// <summary>
    /// 写
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(FormatDateTimeOffset(value, _dateFormatString, _isUtc, _timeZoneResolver));
    }

    /// <summary>
    /// 按用户时区（优先，输出无偏移墙钟）或默认（ISO 带偏移）格式化。
    /// </summary>
    /// <param name="value">时间</param>
    /// <param name="dateFormatString">无换算时输出格式</param>
    /// <param name="isUtc">无换算时是否按 UTC 输出</param>
    /// <param name="timeZoneResolver">当前用户时区（IANA）解析委托</param>
    /// <returns>格式化字符串</returns>
    internal static string FormatDateTimeOffset(DateTimeOffset value, string dateFormatString, bool isUtc, Func<string?>? timeZoneResolver)
    {
        var timeZoneId = timeZoneResolver?.Invoke();
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            if (TimeZoneHelper.TryGetTimeZone(timeZoneId, out var timeZoneInfo))
            {
                // 换算到用户时区后，输出无偏移墙钟（前端 dayjs 直接按本地显示，避免再被浏览器时区二次偏移）
                return TimeZoneInfo.ConvertTime(value, timeZoneInfo).ToString(UserTimeZoneFormat);
            }

            // 指定的时区解析不了：固定输出 UTC 带偏移，不用构造时的格式，免得它不带偏移让客户端错当本地时间
            return value.ToUniversalTime().ToString(UnresolvedTimeZoneFormat, CultureInfo.InvariantCulture);
        }

        return (isUtc ? value.ToUniversalTime() : value).ToString(dateFormatString);
    }
}

/// <summary>
/// DateTimeOffsetNullableConverter
/// <para>语义同 <see cref="DateTimeOffsetJsonConverter"/>，支持可选的用户时区换算。</para>
/// </summary>
public class DateTimeOffsetNullableConverter : JsonConverter<DateTimeOffset?>
{
    private readonly string _dateFormatString;
    private readonly bool _isUtc;
    private readonly Func<string?>? _timeZoneResolver;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DateTimeOffsetNullableConverter()
        : this("yyyy-MM-ddTHH:mm:sszzz", false)
    {
    }

    /// <summary>
    /// 构造函数（按用户时区换算，格式与 UTC 用默认）
    /// </summary>
    /// <param name="timeZoneResolver">当前用户时区（IANA）解析委托；返回 null/空表示不换算</param>
    public DateTimeOffsetNullableConverter(Func<string?> timeZoneResolver)
        : this("yyyy-MM-ddTHH:mm:sszzz", false, timeZoneResolver)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dateFormatString">无时区换算时的输出格式</param>
    /// <param name="isUtc">无时区换算时是否按 UTC 输出</param>
    /// <param name="timeZoneResolver">当前用户时区（IANA）解析委托；返回 null/空表示不换算</param>
    public DateTimeOffsetNullableConverter(string dateFormatString, bool isUtc, Func<string?>? timeZoneResolver = null)
    {
        _dateFormatString = dateFormatString;
        _isUtc = isUtc;
        _timeZoneResolver = timeZoneResolver;
    }

    /// <summary>
    /// 读
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="typeToConvert"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String when DateTimeOffset.TryParse(reader.GetString(), out var value) => _isUtc ? value.ToUniversalTime() : value,
            JsonTokenType.Null => null,
            _ => null
        };
    }

    /// <summary>
    /// 写
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(DateTimeOffsetJsonConverter.FormatDateTimeOffset(value.Value, _dateFormatString, _isUtc, _timeZoneResolver));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
