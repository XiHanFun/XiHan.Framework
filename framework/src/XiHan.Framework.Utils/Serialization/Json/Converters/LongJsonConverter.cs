#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LongJsonConverter
// Guid:6c399d59-ef12-4354-a5b6-d9af73b04f8b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2023-04-25 下午 11:04:51
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Serialization.Json.Converters;

/// <summary>
/// LongJsonConverter
/// </summary>
public class LongJsonConverter : JsonConverter<long>
{
    // 是否超过最大长度 17 再处理
    private readonly bool _isMax17;

    /// <summary>
    /// 构造函数
    /// </summary>
    public LongJsonConverter()
    {
        _isMax17 = false;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="isMax17"></param>
    public LongJsonConverter(bool isMax17)
    {
        _isMax17 = isMax17;
    }

    /// <summary>
    /// 读
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="typeToConvert"></param>
    /// <param name="options"></param>
    /// <returns></returns>
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
    /// 写
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        switch (_isMax17 && value > 99999999999999999)
        {
            case true:
                writer.WriteStringValue(value.ToString());
                break;

            case false:
                writer.WriteNumberValue(value);
                break;
        }
    }
}

/// <summary>
/// LongNullableConverter
/// </summary>
public class LongNullableConverter : JsonConverter<long?>
{
    // 是否超过最大长度 17 再处理
    private readonly bool _isMax17;

    /// <summary>
    /// 构造函数
    /// </summary>
    public LongNullableConverter()
    {
        _isMax17 = false;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="isMax17"></param>
    public LongNullableConverter(bool isMax17)
    {
        _isMax17 = isMax17;
    }

    /// <summary>
    /// 读
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="typeToConvert"></param>
    /// <param name="options"></param>
    /// <returns></returns>
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
    /// 写
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        switch (value.HasValue)
        {
            case true when _isMax17 && value.Value > 99999999999999999:
                writer.WriteStringValue(value.Value.ToString());
                break;

            case true:
                writer.WriteNumberValue(value.Value);
                break;

            case false:
                writer.WriteNullValue();
                break;
        }
    }
}
