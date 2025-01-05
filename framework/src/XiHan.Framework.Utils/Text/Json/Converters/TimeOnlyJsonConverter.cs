#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TimeOnlyJsonConverter
// Guid:fded905f-17ef-4373-afbc-f2716e06f072
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-12-05 下午 05:33:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Text.Json.Converters;

/// <summary>
/// TimeOnlyJsonConverter
/// </summary>
public class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    private readonly string _dateFormatString;

    /// <summary>
    /// 构造函数
    /// </summary>
    public TimeOnlyJsonConverter()
    {
        _dateFormatString = "HH:mm:ss";
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dateFormatString"></param>
    public TimeOnlyJsonConverter(string dateFormatString)
    {
        _dateFormatString = dateFormatString;
    }

    /// <summary>
    /// 读
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="typeToConvert"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            if (TimeOnly.TryParse(reader.GetString(), out var time))
            {
                return time;
            }
        }
        return default;
    }

    /// <summary>
    /// 写
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(_dateFormatString));
    }
}

/// <summary>
/// TimeOnlyNullableConverter
/// </summary>
public class TimeOnlyNullableConverter : JsonConverter<TimeOnly?>
{
    private readonly string _dateFormatString;

    /// <summary>
    /// 构造函数
    /// </summary>
    public TimeOnlyNullableConverter()
    {
        _dateFormatString = "HH:mm:ss";
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dateFormatString"></param>
    public TimeOnlyNullableConverter(string dateFormatString)
    {
        _dateFormatString = dateFormatString;
    }

    /// <summary>
    /// 读
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="typeToConvert"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public override TimeOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            if (TimeOnly.TryParse(reader.GetString(), out var time))
            {
                return time;
            }
        }
        return null;
    }

    /// <summary>
    /// 写
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    public override void Write(Utf8JsonWriter writer, TimeOnly? value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value?.ToString(_dateFormatString));
    }
}
