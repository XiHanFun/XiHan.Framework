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
/// </summary>
public class DateTimeJsonConverter : JsonConverter<DateTime>
{
    private readonly string _dateFormatString;
    private readonly bool _isUtc;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DateTimeJsonConverter()
    {
        _dateFormatString = "yyyy-MM-dd HH:mm:ss";
        _isUtc = false;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dateFormatString"></param>
    /// <param name="isUtc"></param>
    public DateTimeJsonConverter(string dateFormatString, bool isUtc)
    {
        _dateFormatString = dateFormatString;
        _isUtc = isUtc;
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
        writer.WriteStringValue(_isUtc ? value.ToUniversalTime().ToString(_dateFormatString) : value.ToString(_dateFormatString));
    }
}

/// <summary>
/// DateTimeNullableConverter
/// </summary>
public class DateTimeNullableConverter : JsonConverter<DateTime?>
{
    private readonly string _dateFormatString;
    private readonly bool _isUtc;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DateTimeNullableConverter()
    {
        _dateFormatString = "yyyy-MM-dd HH:mm:ss";
        _isUtc = false;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dateFormatString"></param>
    /// <param name="isUtc"></param>
    public DateTimeNullableConverter(string dateFormatString, bool isUtc)
    {
        _dateFormatString = dateFormatString;
        _isUtc = isUtc;
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
        switch (value.HasValue)
        {
            case true:
                writer.WriteStringValue(_isUtc ? value.Value.ToUniversalTime().ToString(_dateFormatString) : value.Value.ToString(_dateFormatString));
                break;

            case false:
                writer.WriteNullValue();
                break;
        }
    }
}
