#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DateTimeOffsetJsonConverter
// Guid:479f8b31-7ad0-4700-a335-b6c8b48a2979
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/08/27 12:28:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Serialization.Json.Converters;

/// <summary>
/// DateTimeOffsetJsonConverter
/// <para>可选用户时区换算：构造时传入 timeZoneResolver（返回 IANA 时区，如 Asia/Shanghai），
/// 序列化时把值换算为该时区并输出“无偏移墙钟”字符串（yyyy-MM-dd HH:mm:ss，与 DateTime 一致，
/// 便于前端按本地直接显示）；解析器为空或返回空时回退默认 ISO-8601 带偏移行为。
/// 时区来源由调用方注入（如 Web 层按请求头解析），本转换器不依赖 HTTP。</para>
/// </summary>
public class DateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
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
            try
            {
                // IANA 标识；net10 + ICU 在 Linux/Windows 均可解析
                var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                // 换算到用户时区后，输出无偏移墙钟（前端 dayjs 直接按本地显示，避免再被浏览器时区二次偏移）
                return TimeZoneInfo.ConvertTime(value, timeZoneInfo).ToString(UserTimeZoneFormat);
            }
            catch
            {
                // 时区标识非法等异常下回退默认行为
            }
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
