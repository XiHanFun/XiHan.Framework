// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Serialization.Json.Converters;

/// <summary>
/// long 序列化为 JSON 字符串，避免 JavaScript Number 精度溢出
/// </summary>
public class LongJsonConverter : JsonConverter<long>
{
    /// <summary>
    /// 读取 JSON 值并转换为 long，字符串与数字均可解析，其它标记返回 0
    /// </summary>
    /// <param name="reader">JSON 读取器</param>
    /// <param name="typeToConvert">要转换的目标类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>转换后的 long 值</returns>
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String when long.TryParse(reader.GetString(), out var l) => l,
            JsonTokenType.Number => reader.GetInt64(),
            _ => 0
        };
    }

    /// <summary>
    /// 把 long 值写为 JSON 字符串
    /// </summary>
    /// <param name="writer">JSON 写入器</param>
    /// <param name="value">要写入的值</param>
    /// <param name="options">序列化选项</param>
    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// long? 序列化为 JSON 字符串
/// </summary>
public class LongNullableConverter : JsonConverter<long?>
{
    /// <summary>
    /// 读取 JSON 值并转换为可空 long，字符串与数字均可解析，null 及其它标记返回 null
    /// </summary>
    /// <param name="reader">JSON 读取器</param>
    /// <param name="typeToConvert">要转换的目标类型</param>
    /// <param name="options">序列化选项</param>
    /// <returns>转换后的可空 long 值</returns>
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String when long.TryParse(reader.GetString(), out var l) => l,
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.Null => null,
            _ => null
        };
    }

    /// <summary>
    /// 把可空 long 值写为 JSON 字符串，无值时写入 null
    /// </summary>
    /// <param name="writer">JSON 写入器</param>
    /// <param name="value">要写入的值</param>
    /// <param name="options">序列化选项</param>
    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToString());
    }
}
