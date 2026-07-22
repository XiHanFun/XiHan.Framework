// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Serialization.Json.Converters;

/// <summary>
/// ByteJsonConverter
/// </summary>
public class ByteJsonConverter : JsonConverter<byte>
{
    /// <summary>
    /// 读
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="typeToConvert"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public override byte Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetByte(),
            JsonTokenType.String when byte.TryParse(reader.GetString(), out var value) => value,
            _ => 0
        };
    }

    /// <summary>
    /// 写
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    public override void Write(Utf8JsonWriter writer, byte value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

/// <summary>
/// ByteNullableConverter
/// </summary>
public class ByteNullableConverter : JsonConverter<byte?>
{
    /// <summary>
    /// 读
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="typeToConvert"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public override byte? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetByte(),
            JsonTokenType.String when byte.TryParse(reader.GetString(), out var value) => value,
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
    public override void Write(Utf8JsonWriter writer, byte? value, JsonSerializerOptions options)
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
