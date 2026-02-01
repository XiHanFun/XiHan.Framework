#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ShortJsonConverter
// Guid:6c0b5a49-3e8d-2d7c-1b6a-5f4e3d2c1b0a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/08/29 10:18:04
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Serialization.Json.Converters;

/// <summary>
/// ShortJsonConverter
/// </summary>
public class ShortJsonConverter : JsonConverter<short>
{
    /// <summary>
    /// 读
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="typeToConvert"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public override short Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt16(),
            JsonTokenType.String when short.TryParse(reader.GetString(), out var value) => value,
            _ => 0
        };
    }

    /// <summary>
    /// 写
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    public override void Write(Utf8JsonWriter writer, short value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

/// <summary>
/// ShortNullableConverter
/// </summary>
public class ShortNullableConverter : JsonConverter<short?>
{
    /// <summary>
    /// 读
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="typeToConvert"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public override short? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt16(),
            JsonTokenType.String when short.TryParse(reader.GetString(), out var value) => value,
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
    public override void Write(Utf8JsonWriter writer, short? value, JsonSerializerOptions options)
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
