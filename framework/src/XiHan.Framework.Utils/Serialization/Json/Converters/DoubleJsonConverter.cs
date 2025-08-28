#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DoubleJsonConverter
// Guid:3f5a8b2c-9d4e-4f1a-8c7b-6e5d4c3b2a19
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2025-01-06 下午 02:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Serialization.Json.Converters;

/// <summary>
/// DoubleJsonConverter
/// </summary>
public class DoubleJsonConverter : JsonConverter<double>
{
    /// <summary>
    /// 读
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="typeToConvert"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetDouble(),
            JsonTokenType.String when double.TryParse(reader.GetString(), out var value) => value,
            _ => 0.0
        };
    }

    /// <summary>
    /// 写
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

/// <summary>
/// DoubleNullableConverter
/// </summary>
public class DoubleNullableConverter : JsonConverter<double?>
{
    /// <summary>
    /// 读
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="typeToConvert"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetDouble(),
            JsonTokenType.String when double.TryParse(reader.GetString(), out var value) => value,
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
    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
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
