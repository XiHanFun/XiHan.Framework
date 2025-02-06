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

namespace XiHan.Framework.Utils.Text.Json.Converters;

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
        if (reader.TokenType == JsonTokenType.String)
        {
            if (long.TryParse(reader.GetString(), out var l))
            {
                return l;
            }
        }
        else if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt64();
        }
        return 0;
    }

    /// <summary>
    /// 写
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        if (_isMax17)
        {
            if (value > 99999999999999999)
            {
                writer.WriteStringValue(value.ToString());
                return;
            }
        }
        writer.WriteNumberValue(value);
    }
}
