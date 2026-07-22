// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Serialization.Json.Converters;

/// <summary>
/// UIntJsonConverter
/// </summary>
public class UIntJsonConverter : JsonConverter<uint>
{
    /// <summary>
    /// 读
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="typeToConvert"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public override uint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetUInt32(),
            JsonTokenType.String when uint.TryParse(reader.GetString(), out var value) => value,
            _ => 0
        };
    }

    /// <summary>
    /// 写
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    public override void Write(Utf8JsonWriter writer, uint value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

/// <summary>
/// UIntNullableConverter
/// </summary>
public class UIntNullableConverter : JsonConverter<uint?>
{
    /// <summary>
    /// 读
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="typeToConvert"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public override uint? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetUInt32(),
            JsonTokenType.String when uint.TryParse(reader.GetString(), out var value) => value,
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
    public override void Write(Utf8JsonWriter writer, uint? value, JsonSerializerOptions options)
    {
        switch (value.HasValue)
        {
            case true:
                writer.WriteNumberValue(value.Value);
                break;

            case false:
                writer.WriteNullValue();
                break;
        }
    }
}
