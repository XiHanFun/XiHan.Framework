#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DateTimeOffsetJsonConverter
// Guid:fded905f-17ef-4373-afbc-f2716e06f072
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
/// </summary>
public class DateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    private readonly string _dateFormatString;
    private readonly bool _isUtc;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DateTimeOffsetJsonConverter()
    {
        _dateFormatString = "yyyy-MM-dd HH:mm:ss";
        _isUtc = false;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dateFormatString"></param>
    /// <param name="isUtc"></param>
    public DateTimeOffsetJsonConverter(string dateFormatString, bool isUtc)
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
        writer.WriteStringValue(_isUtc ? value.ToUniversalTime().ToString(_dateFormatString) : value.ToString(_dateFormatString));
    }
}

/// <summary>
/// DateTimeOffsetNullableConverter
/// </summary>
public class DateTimeOffsetNullableConverter : JsonConverter<DateTimeOffset?>
{
    private readonly string _dateFormatString;
    private readonly bool _isUtc;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DateTimeOffsetNullableConverter()
    {
        _dateFormatString = "yyyy-MM-dd HH:mm:ss";
        _isUtc = false;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dateFormatString"></param>
    /// <param name="isUtc"></param>
    public DateTimeOffsetNullableConverter(string dateFormatString, bool isUtc)
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
