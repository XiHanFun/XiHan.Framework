// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Serialization.Json.Converters;

/// <summary>
/// 枚举数值序列化转换器：将枚举按其底层数值（int）读写。
/// 用于在已全局启用 <see cref="JsonStringEnumConverter"/>（按名称序列化）的场景下，
/// 通过在特定枚举/属性上标注 <c>[JsonConverter(typeof(NumericEnumConverter&lt;T&gt;))]</c> 覆盖为数字形式。
/// 反序列化兼容「数字」与「字符串名称/数字字符串」两种来源。
/// </summary>
/// <typeparam name="TEnum">枚举类型</typeparam>
public class NumericEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    /// <summary>
    /// 读
    /// </summary>
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number when reader.TryGetInt64(out var number):
                return (TEnum)Enum.ToObject(typeof(TEnum), number);

            case JsonTokenType.String:
                var text = reader.GetString();
                if (Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed))
                {
                    return parsed;
                }

                if (long.TryParse(text, out var numeric))
                {
                    return (TEnum)Enum.ToObject(typeof(TEnum), numeric);
                }

                break;
        }

        throw new JsonException($"无法将 {reader.TokenType} 转换为枚举类型 {typeof(TEnum).Name}");
    }

    /// <summary>
    /// 写
    /// </summary>
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(Convert.ToInt64(value));
    }
}
