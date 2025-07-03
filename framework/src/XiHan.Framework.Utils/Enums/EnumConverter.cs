#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumConverter
// Guid:9ce569e4-6869-4251-8dc5-fad69e9d56e6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/2 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Utils.Enums;

/// <summary>
/// 枚举转换器
/// </summary>
public static class EnumConverter
{
    #region 基本转换

    /// <summary>
    /// 将对象转换为指定的枚举类型
    /// </summary>
    /// <typeparam name="TEnum">目标枚举类型</typeparam>
    /// <param name="value">要转换的值</param>
    /// <returns>转换后的枚举值</returns>
    public static TEnum ToEnum<TEnum>(object value) where TEnum : struct, Enum
    {
        return value switch
        {
            TEnum enumValue => enumValue,
            int intValue => (TEnum)Enum.ToObject(typeof(TEnum), intValue),
            string stringValue => Enum.Parse<TEnum>(stringValue),
            _ => throw new ArgumentException($"无法将类型 {value.GetType().Name} 转换为 {typeof(TEnum).Name}", nameof(value))
        };
    }

    /// <summary>
    /// 尝试将对象转换为指定的枚举类型
    /// </summary>
    /// <typeparam name="TEnum">目标枚举类型</typeparam>
    /// <param name="value">要转换的值</param>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    public static bool TryToEnum<TEnum>(object value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;

        try
        {
            result = ToEnum<TEnum>(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 将枚举值转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>转换后的值</returns>
    public static T ConvertTo<T>(Enum enumValue)
    {
        return (T)Convert.ChangeType(enumValue, typeof(T));
    }

    #endregion 基本转换

    #region 描述转换

    /// <summary>
    /// 根据描述转换为枚举
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="description">描述</param>
    /// <returns>枚举值</returns>
    public static TEnum FromDescription<TEnum>(string description) where TEnum : struct, Enum
    {
        return EnumHelper.GetEnumByDescription<TEnum>(description);
    }

    /// <summary>
    /// 尝试根据描述转换为枚举
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="description">描述</param>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    public static bool TryFromDescription<TEnum>(string description, out TEnum result) where TEnum : struct, Enum
    {
        return EnumHelper.TryGetEnumByDescription(description, out result);
    }

    #endregion 描述转换

    #region 集合转换

    /// <summary>
    /// 将对象集合转换为枚举集合
    /// </summary>
    /// <typeparam name="TEnum">目标枚举类型</typeparam>
    /// <param name="values">要转换的值集合</param>
    /// <returns>转换后的枚举集合</returns>
    public static IEnumerable<TEnum> ToEnumCollection<TEnum>(IEnumerable<object> values) where TEnum : struct, Enum
    {
        return values.Select(ToEnum<TEnum>);
    }

    /// <summary>
    /// 尝试将对象集合转换为枚举集合
    /// </summary>
    /// <typeparam name="TEnum">目标枚举类型</typeparam>
    /// <param name="values">要转换的值集合</param>
    /// <param name="results">转换结果集合</param>
    /// <returns>是否全部转换成功</returns>
    public static bool TryToEnumCollection<TEnum>(IEnumerable<object> values, out IEnumerable<TEnum> results) where TEnum : struct, Enum
    {
        var resultList = new List<TEnum>();

        foreach (var value in values)
        {
            if (TryToEnum<TEnum>(value, out var result))
            {
                resultList.Add(result);
            }
            else
            {
                results = [];
                return false;
            }
        }

        results = resultList;
        return true;
    }

    /// <summary>
    /// 将枚举集合转换为指定类型集合
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="enumValues">枚举值集合</param>
    /// <returns>转换后的值集合</returns>
    public static IEnumerable<T> ConvertToCollection<T>(IEnumerable<Enum> enumValues)
    {
        return enumValues.Select(ConvertTo<T>);
    }

    #endregion 集合转换

    #region 字典转换

    /// <summary>
    /// 将枚举转换为字典（名称-值）
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>名称值字典</returns>
    public static Dictionary<string, int> ToNameValueDictionary<TEnum>() where TEnum : struct, Enum
    {
        return EnumHelper<TEnum>.GetNameValueDict();
    }

    /// <summary>
    /// 将枚举转换为字典（值-描述）
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>值描述字典</returns>
    public static Dictionary<int, string> ToValueDescriptionDictionary<TEnum>() where TEnum : struct, Enum
    {
        return EnumHelper<TEnum>.GetValueDescriptionDict();
    }

    /// <summary>
    /// 将枚举转换为字典（名称-描述）
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>名称描述字典</returns>
    public static Dictionary<string, string> ToNameDescriptionDictionary<TEnum>() where TEnum : struct, Enum
    {
        var result = new Dictionary<string, string>();
        var values = EnumHelper<TEnum>.GetValues();

        foreach (var value in values)
        {
            result[value.GetName()] = value.GetDescription();
        }

        return result;
    }

    /// <summary>
    /// 将枚举转换为完整信息字典
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>完整信息字典</returns>
    public static Dictionary<string, EnumInfo> ToFullInfoDictionary<TEnum>() where TEnum : struct, Enum
    {
        var result = new Dictionary<string, EnumInfo>();
        var enumInfos = EnumHelper<TEnum>.GetEnumInfos();

        foreach (var info in enumInfos)
        {
            result[info.Key] = info;
        }

        return result;
    }

    #endregion 字典转换

    #region 数组转换

    /// <summary>
    /// 将枚举转换为选择项数组
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>选择项数组</returns>
    public static SelectItem[] ToSelectItems<TEnum>() where TEnum : struct, Enum
    {
        var values = EnumHelper<TEnum>.GetValues();
        return [.. values.Select(value => new SelectItem
        {
            Value = value.GetValue().ToString(),
            Text = value.GetDescription(),
            Key = value.GetName()
        })];
    }

    /// <summary>
    /// 将枚举转换为键值对数组
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>键值对数组</returns>
    public static KeyValuePair<string, int>[] ToKeyValuePairs<TEnum>() where TEnum : struct, Enum
    {
        var values = EnumHelper<TEnum>.GetValues();
        return [.. values.Select(value => new KeyValuePair<string, int>(value.GetName(), value.GetValue()))];
    }

    #endregion 数组转换

    #region 位操作转换

    /// <summary>
    /// 将标志枚举转换为单个标志数组
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="flagsValue">标志枚举值</param>
    /// <returns>单个标志数组</returns>
    public static TEnum[] ToFlagArray<TEnum>(TEnum flagsValue) where TEnum : struct, Enum
    {
        var result = new List<TEnum>();
        var allFlags = EnumHelper<TEnum>.GetValues();

        foreach (var flag in allFlags)
        {
            if (flagsValue.HasFlag(flag))
            {
                result.Add(flag);
            }
        }

        return [.. result];
    }

    /// <summary>
    /// 将标志数组转换为标志枚举
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="flags">标志数组</param>
    /// <returns>标志枚举值</returns>
    public static TEnum FromFlagArray<TEnum>(TEnum[] flags) where TEnum : struct, Enum
    {
        var result = 0;
        foreach (var flag in flags)
        {
            result |= flag.GetValue();
        }
        return (TEnum)Enum.ToObject(typeof(TEnum), result);
    }

    #endregion 位操作转换

    #region 字符串格式转换

    /// <summary>
    /// 将枚举转换为格式化字符串
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <param name="format">格式（Name、Value、Description）</param>
    /// <returns>格式化字符串</returns>
    public static string ToString<TEnum>(TEnum value, string format = "Name") where TEnum : struct, Enum
    {
        return format.ToUpper() switch
        {
            "NAME" => value.GetName(),
            "VALUE" => value.GetValue().ToString(),
            "DESCRIPTION" => value.GetDescription(),
            _ => value.ToString()
        };
    }

    /// <summary>
    /// 将枚举集合转换为分隔字符串
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="values">枚举值集合</param>
    /// <param name="separator">分隔符</param>
    /// <param name="format">格式</param>
    /// <returns>分隔字符串</returns>
    public static string ToSeparatedString<TEnum>(IEnumerable<TEnum> values, string separator = ",", string format = "Name") where TEnum : struct, Enum
    {
        return string.Join(separator, values.Select(v => ToString(v, format)));
    }

    #endregion 字符串格式转换
}

/// <summary>
/// 选择项
/// </summary>
public class SelectItem
{
    /// <summary>
    /// 键
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 值
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 文本
    /// </summary>
    public string Text { get; set; } = string.Empty;
}

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

/// <summary>
/// 枚举类型转换器
/// </summary>
/// <typeparam name="TEnum">枚举类型</typeparam>
public class EnumTypeConverter<TEnum> : TypeConverter where TEnum : struct, Enum
{
    /// <summary>
    /// 是否可以从指定类型转换
    /// </summary>
    /// <param name="context">上下文</param>
    /// <param name="sourceType">源类型</param>
    /// <returns>是否可以转换</returns>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || sourceType == typeof(int) || base.CanConvertFrom(context, sourceType);
    }

    /// <summary>
    /// 是否可以转换到指定类型
    /// </summary>
    /// <param name="context">上下文</param>
    /// <param name="destinationType">目标类型</param>
    /// <returns>是否可以转换</returns>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) || destinationType == typeof(int) || base.CanConvertTo(context, destinationType);
    }

    /// <summary>
    /// 从指定类型转换
    /// </summary>
    /// <param name="context">上下文</param>
    /// <param name="culture">区域性</param>
    /// <param name="value">值</param>
    /// <returns>转换结果</returns>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        return value is string stringValue
            ? EnumHelper.TryGetEnum<TEnum>(stringValue, out var resultString) ? resultString : base.ConvertFrom(context, culture, value)
            : value is int intValue
            ? EnumHelper.TryGetEnum<TEnum>(intValue, out var resultInt) ? resultInt : base.ConvertFrom(context, culture, value)
            : base.ConvertFrom(context, culture, value);
    }

    /// <summary>
    /// 转换到指定类型
    /// </summary>
    /// <param name="context">上下文</param>
    /// <param name="culture">区域性</param>
    /// <param name="value">值</param>
    /// <param name="destinationType">目标类型</param>
    /// <returns>转换结果</returns>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is TEnum enumValue)
        {
            if (destinationType == typeof(string))
            {
                return enumValue.GetName();
            }

            if (destinationType == typeof(int))
            {
                return enumValue.GetValue();
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
