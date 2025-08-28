#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonConverterFactory
// Guid:39084726-1b5a-0a49-8837-2d1c0b0a9765
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2025-01-06 下午 03:25:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Serialization.Json.Converters;

namespace XiHan.Framework.Utils.Serialization.Json;

/// <summary>
/// JSON 转换器工厂
/// 提供常用类型转换器的统一配置
/// </summary>
public static class JsonConverterFactory
{
    /// <summary>
    /// 获取所有基础数值类型转换器
    /// </summary>
    /// <returns>转换器列表</returns>
    public static List<JsonConverter> GetNumericConverters()
    {
        return
        [
            new IntJsonConverter(),
            new IntNullableConverter(),
            new LongJsonConverter(),
            new LongNullableConverter(),
            new FloatJsonConverter(),
            new FloatNullableConverter(),
            new DoubleJsonConverter(),
            new DoubleNullableConverter(),
            new DecimalJsonConverter(),
            new DecimalNullableConverter(),
            new ByteJsonConverter(),
            new ByteNullableConverter(),
            new ShortJsonConverter(),
            new ShortNullableConverter(),
            new UIntJsonConverter(),
            new UIntNullableConverter()
        ];
    }

    /// <summary>
    /// 获取所有日期时间类型转换器
    /// </summary>
    /// <param name="dateFormat">日期格式</param>
    /// <param name="timeFormat">时间格式</param>
    /// <param name="isUtc">是否使用 UTC 时间</param>
    /// <returns>转换器列表</returns>
    public static List<JsonConverter> GetDateTimeConverters(string dateFormat = "yyyy-MM-dd", string timeFormat = "HH:mm:ss", bool isUtc = false)
    {
        return
        [
            new DateOnlyJsonConverter(dateFormat),
            new DateOnlyNullableConverter(dateFormat),
            new TimeOnlyJsonConverter(timeFormat),
            new TimeOnlyNullableConverter(timeFormat),
            new DateTimeJsonConverter($"{dateFormat} {timeFormat}", isUtc),
            new DateTimeNullableConverter($"{dateFormat} {timeFormat}", isUtc),
            new DateTimeOffsetJsonConverter($"{dateFormat} {timeFormat}", isUtc),
            new DateTimeOffsetNullableConverter($"{dateFormat} {timeFormat}", isUtc)
        ];
    }

    /// <summary>
    /// 获取其他常用类型转换器
    /// </summary>
    /// <returns>转换器列表</returns>
    public static List<JsonConverter> GetCommonConverters()
    {
        return
        [
            new BooleanJsonConverter(),
            new BooleanNullableConverter(),
            new GuidJsonConverter(),
            new GuidNullableConverter()
        ];
    }

    /// <summary>
    /// 获取所有转换器
    /// </summary>
    /// <param name="dateFormat">日期格式</param>
    /// <param name="timeFormat">时间格式</param>
    /// <param name="isUtc">是否使用 UTC 时间</param>
    /// <returns>完整的转换器列表</returns>
    public static List<JsonConverter> GetAllConverters(string dateFormat = "yyyy-MM-dd", string timeFormat = "HH:mm:ss", bool isUtc = false)
    {
        var converters = new List<JsonConverter>();
        converters.AddRange(GetNumericConverters());
        converters.AddRange(GetDateTimeConverters(dateFormat, timeFormat, isUtc));
        converters.AddRange(GetCommonConverters());
        return converters;
    }

    /// <summary>
    /// 配置 JsonSerializerOptions 使用所有转换器
    /// </summary>
    /// <param name="options">JSON 序列化选项</param>
    /// <param name="dateFormat">日期格式</param>
    /// <param name="timeFormat">时间格式</param>
    /// <param name="isUtc">是否使用 UTC 时间</param>
    /// <returns>配置后的选项</returns>
    public static JsonSerializerOptions ConfigureConverters(this JsonSerializerOptions options, string dateFormat = "yyyy-MM-dd", string timeFormat = "HH:mm:ss", bool isUtc = false)
    {
        var converters = GetAllConverters(dateFormat, timeFormat, isUtc);
        foreach (var converter in converters)
        {
            options.Converters.Add(converter);
        }
        return options;
    }

    /// <summary>
    /// 创建预配置的 JsonSerializerOptions
    /// </summary>
    /// <param name="writeIndented">是否缩进</param>
    /// <param name="camelCase">是否使用驼峰命名</param>
    /// <param name="dateFormat">日期格式</param>
    /// <param name="timeFormat">时间格式</param>
    /// <param name="isUtc">是否使用 UTC 时间</param>
    /// <returns>配置完整的 JsonSerializerOptions</returns>
    public static JsonSerializerOptions CreateOptions(bool writeIndented = true, bool camelCase = true, string dateFormat = "yyyy-MM-dd", string timeFormat = "HH:mm:ss", bool isUtc = false)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = writeIndented,
            PropertyNamingPolicy = camelCase ? JsonNamingPolicy.CamelCase : null,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        return options.ConfigureConverters(dateFormat, timeFormat, isUtc);
    }
}
