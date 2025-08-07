#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumJsonConverter
// Guid:7d588227-b2e5-456b-943c-46f7195de595
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/4 3:06:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Utils.Enums;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.Text.Json.Converters;

/// <summary>
/// 枚举 JSON 转换器
/// </summary>
/// <typeparam name="TEnum">枚举类型</typeparam>
public class EnumJsonConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="mode">转换模式</param>
    public EnumJsonConverter(EnumConvertMode mode = EnumConvertMode.Name)
    {
        Mode = mode;
    }

    /// <summary>
    /// 转换模式
    /// </summary>
    public EnumConvertMode Mode { get; set; } = EnumConvertMode.Name;

    /// <summary>
    /// 读取 JSON
    /// </summary>
    /// <param name="reader">读取器</param>
    /// <param name="typeToConvert">要转换的类型</param>
    /// <param name="options">选项</param>
    /// <returns>枚举值</returns>
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => Mode switch
            {
                EnumConvertMode.Name => EnumHelper.GetEnum<TEnum>(reader.GetString()!),
                EnumConvertMode.Description => EnumHelper.GetEnumByDescription<TEnum>(reader.GetString()!),
                _ => EnumHelper.GetEnum<TEnum>(reader.GetString()!)
            },
            JsonTokenType.Number => EnumHelper.GetEnum<TEnum>(reader.GetInt32()),
            _ => throw new JsonException($"无法将 {reader.TokenType} 转换为 {typeof(TEnum).Name}")
        };
    }

    /// <summary>
    /// 写入 JSON
    /// </summary>
    /// <param name="writer">写入器</param>
    /// <param name="value">枚举值</param>
    /// <param name="options">选项</param>
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        switch (Mode)
        {
            case EnumConvertMode.Name:
                writer.WriteStringValue(value.GetName());
                break;

            case EnumConvertMode.Value:
                writer.WriteNumberValue(value.GetValue());
                break;

            case EnumConvertMode.Description:
                writer.WriteStringValue(value.GetDescription());
                break;

            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
}

/// <summary>
/// 枚举转换模式
/// </summary>
public enum EnumConvertMode
{
    /// <summary>
    /// 名称
    /// </summary>
    Name,

    /// <summary>
    /// 值
    /// </summary>
    Value,

    /// <summary>
    /// 描述
    /// </summary>
    Description
}
