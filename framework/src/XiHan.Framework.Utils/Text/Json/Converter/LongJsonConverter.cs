#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2023 ZhaiFanhua All Rights Reserved.
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
using XiHan.Framework.Utils.Extensions.System;

namespace XiHan.Framework.Utils.Text.Json.Converter;

/// <summary>
/// LongJsonConverter
/// 参考定义:
/// <see href="https://learn.microsoft.com/zh-cn/dotnet/standard/serialization/system-text-json/converters-how-to">如何在 .NET 中编写用于 JSON 序列化(封送)的自定义转换器</see>
/// </summary>
public class LongJsonConverter : JsonConverter<long>
{
    /// <summary>
    /// 读
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="typeToConvert"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.Number ? reader.GetInt64() : reader.GetString().ParseToLong();
    }

    /// <summary>
    /// 写
    /// Guid数据在前端会出现丢失精度，故转换为string类型
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
