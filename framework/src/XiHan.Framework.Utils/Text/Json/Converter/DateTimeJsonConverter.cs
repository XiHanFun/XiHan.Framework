#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2022 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DateTimeJsonConverter
// Guid:fded905f-17ef-4373-afbc-f2716e06f072
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-12-05 下午 05:33:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Extensions.System;

namespace XiHan.Framework.Utils.System.Text.Json.Converter;

/// <summary>
/// DateTimeJsonConverter
/// 参考定义:
/// <see href="https://learn.microsoft.com/zh-cn/dotnet/standard/serialization/system-text-json/converters-how-to">如何在 .NET 中编写用于 JSON 序列化(封送)的自定义转换器</see>
/// </summary>
public class DateTimeJsonConverter : JsonConverter<DateTime>
{
    private readonly string _dateFormatString;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DateTimeJsonConverter()
    {
        _dateFormatString = "yyyy-MM-dd HH:mm:ss";
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dateFormatString"></param>
    public DateTimeJsonConverter(string dateFormatString)
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
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.String ? reader.GetDateTime() : reader.GetString().ParseToDateTime();
    }

    /// <summary>
    /// 写
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(_dateFormatString));
    }
}

/// <summary>
/// DateTimeNullableConverter
/// 参考定义:
/// <see href="https://learn.microsoft.com/zh-cn/dotnet/standard/serialization/system-text-json/converters-how-to">如何在 .NET 中编写用于 JSON 序列化(封送)的自定义转换器</see>
/// </summary>
public class DateTimeNullableConverter : JsonConverter<DateTime?>
{
    /// <summary>
    /// 读
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="typeToConvert"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return string.IsNullOrEmpty(reader.GetString()) ? default(DateTime?) : reader.GetString().ParseToDateTime();
    }

    /// <summary>
    /// 写
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ParseToDateTime());
    }
}