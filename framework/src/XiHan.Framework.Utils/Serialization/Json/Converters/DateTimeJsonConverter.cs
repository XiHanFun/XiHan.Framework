#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DateTimeJsonConverter
// Guid:c71e2006-dcf4-4291-a572-f489662fef84
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/08/27 12:28:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Serialization.Json.Converters;

/// <summary>
/// DateTimeJsonConverter
/// <para>可选用户时区换算：构造时传入 timeZoneResolver（返回 IANA 时区，如 Asia/Shanghai），
/// 序列化时把存储的 UTC 时间换算为该时区后输出；解析器为空或返回空时回退 isUtc 行为。
/// 时区来源由调用方注入（如 Web 层按请求头解析），本转换器不依赖 HTTP，保持 Utils 纯净。</para>
/// </summary>
public class DateTimeJsonConverter : JsonConverter<DateTime>
{
    private readonly string _dateFormatString;
    private readonly bool _isUtc;
    private readonly Func<string?>? _timeZoneResolver;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DateTimeJsonConverter()
        : this("yyyy-MM-dd HH:mm:ss", false)
    {
    }

    /// <summary>
    /// 构造函数（按用户时区换算，格式与 UTC 用默认）
    /// </summary>
    /// <param name="timeZoneResolver">当前用户时区（IANA）解析委托；返回 null/空表示不换算</param>
    public DateTimeJsonConverter(Func<string?> timeZoneResolver)
        : this("yyyy-MM-dd HH:mm:ss", false, timeZoneResolver)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dateFormatString">输出格式</param>
    /// <param name="isUtc">是否按 UTC 输出（无时区解析器时生效）</param>
    /// <param name="timeZoneResolver">当前用户时区（IANA）解析委托；返回 null/空表示不换算</param>
    public DateTimeJsonConverter(string dateFormatString, bool isUtc, Func<string?>? timeZoneResolver = null)
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
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String when DateTime.TryParse(reader.GetString(), out var value) => _isUtc ? value.ToUniversalTime() : value,
            _ => default
        };
    }

    /// <summary>
    /// 写
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(FormatDateTime(value, _dateFormatString, _isUtc, _timeZoneResolver));
    }

    /// <summary>
    /// 按用户时区（优先）或 isUtc 规则格式化时间。
    /// </summary>
    /// <param name="value">时间</param>
    /// <param name="dateFormatString">输出格式</param>
    /// <param name="isUtc">无时区解析器时是否按 UTC 输出</param>
    /// <param name="timeZoneResolver">当前用户时区（IANA）解析委托</param>
    /// <returns>格式化字符串</returns>
    internal static string FormatDateTime(DateTime value, string dateFormatString, bool isUtc, Func<string?>? timeZoneResolver)
    {
        var timeZoneId = timeZoneResolver?.Invoke();
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            return ConvertToUserTime(value, timeZoneId).ToString(dateFormatString);
        }

        return (isUtc ? value.ToUniversalTime() : value).ToString(dateFormatString);
    }

    /// <summary>
    /// 将存储的 UTC 时间按 IANA 时区换算为用户本地时间；时区非法时回退原值。
    /// </summary>
    private static DateTime ConvertToUserTime(DateTime value, string timeZoneId)
    {
        try
        {
            // IANA 标识；net10 + ICU 在 Linux/Windows 均可解析
            var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            // 存储约定为 UTC；显式标记 Kind 以满足 ConvertTimeFromUtc 的入参要求
            var utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, timeZoneInfo);
        }
        catch
        {
            return value;
        }
    }
}

/// <summary>
/// DateTimeNullableConverter
/// <para>语义同 <see cref="DateTimeJsonConverter"/>，支持可选的用户时区换算。</para>
/// </summary>
public class DateTimeNullableConverter : JsonConverter<DateTime?>
{
    private readonly string _dateFormatString;
    private readonly bool _isUtc;
    private readonly Func<string?>? _timeZoneResolver;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DateTimeNullableConverter()
        : this("yyyy-MM-dd HH:mm:ss", false)
    {
    }

    /// <summary>
    /// 构造函数（按用户时区换算，格式与 UTC 用默认）
    /// </summary>
    /// <param name="timeZoneResolver">当前用户时区（IANA）解析委托；返回 null/空表示不换算</param>
    public DateTimeNullableConverter(Func<string?> timeZoneResolver)
        : this("yyyy-MM-dd HH:mm:ss", false, timeZoneResolver)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dateFormatString">输出格式</param>
    /// <param name="isUtc">是否按 UTC 输出（无时区解析器时生效）</param>
    /// <param name="timeZoneResolver">当前用户时区（IANA）解析委托；返回 null/空表示不换算</param>
    public DateTimeNullableConverter(string dateFormatString, bool isUtc, Func<string?>? timeZoneResolver = null)
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
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String when DateTime.TryParse(reader.GetString(), out var value) => _isUtc ? value.ToUniversalTime() : value,
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
    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(DateTimeJsonConverter.FormatDateTime(value.Value, _dateFormatString, _isUtc, _timeZoneResolver));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
