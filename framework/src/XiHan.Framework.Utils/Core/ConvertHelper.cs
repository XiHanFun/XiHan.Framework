#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConvertHelper
// Guid:2c1d4e5f-6a7b-8c9d-0e1f-2a3b4c5d6e7f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/19 23:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace XiHan.Framework.Utils.Core;

/// <summary>
/// 类型转换帮助类
/// </summary>
public static class ConvertHelper
{
    #region 通用转换

    /// <summary>
    /// 将对象转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换后的值</returns>
    public static T ConvertTo<T>(object? value, T defaultValue = default!)
    {
        if (TryConvertTo<T>(value, out var result))
        {
            return result;
        }
        return defaultValue;
    }

    /// <summary>
    /// 尝试将对象转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="value">要转换的值</param>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    public static bool TryConvertTo<T>(object? value, out T result)
    {
        result = default!;

        if (value == null)
        {
            return !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null;
        }

        var targetType = typeof(T);
        var sourceType = value.GetType();

        // 相同类型直接返回
        if (sourceType == targetType || targetType.IsAssignableFrom(sourceType))
        {
            result = (T)value;
            return true;
        }

        try
        {
            // 处理 Nullable 类型
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            if (underlyingType != null)
            {
                targetType = underlyingType;
            }

            // 枚举类型转换
            if (targetType.IsEnum)
            {
                if (value is string stringValue)
                {
                    if (Enum.TryParse(targetType, stringValue, true, out var enumResult))
                    {
                        result = (T)enumResult;
                        return true;
                    }
                }
                else
                {
                    var enumValue = Enum.ToObject(targetType, value);
                    result = (T)enumValue;
                    return true;
                }
            }

            // 字符串类型转换
            if (targetType == typeof(string))
            {
                result = (T)(object)value.ToString()!;
                return true;
            }

            // 基础类型转换
            if (targetType.IsPrimitive || targetType == typeof(decimal) || targetType == typeof(DateTime) || targetType == typeof(Guid))
            {
                var convertedValue = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                result = (T)convertedValue;
                return true;
            }

            // JSON 反序列化
            if (value is string jsonString && !string.IsNullOrWhiteSpace(jsonString))
            {
                var jsonResult = JsonSerializer.Deserialize<T>(jsonString);
                if (jsonResult != null)
                {
                    result = jsonResult;
                    return true;
                }
            }

            // 使用 Convert.ChangeType
            var finalResult = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            result = (T)finalResult;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 将对象转换为指定类型
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换后的值</returns>
    public static object? ConvertTo(object? value, Type targetType, object? defaultValue = null)
    {
        if (TryConvertTo(value, targetType, out var result))
        {
            return result;
        }
        return defaultValue;
    }

    /// <summary>
    /// 尝试将对象转换为指定类型
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    public static bool TryConvertTo(object? value, Type targetType, out object? result)
    {
        result = null;

        if (value == null)
        {
            return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;
        }

        var sourceType = value.GetType();

        // 相同类型直接返回
        if (sourceType == targetType || targetType.IsAssignableFrom(sourceType))
        {
            result = value;
            return true;
        }

        try
        {
            // 处理 Nullable 类型
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            if (underlyingType != null)
            {
                targetType = underlyingType;
            }

            // 枚举类型转换
            if (targetType.IsEnum)
            {
                if (value is string stringValue)
                {
                    if (Enum.TryParse(targetType, stringValue, true, out var enumResult))
                    {
                        result = enumResult;
                        return true;
                    }
                }
                else
                {
                    result = Enum.ToObject(targetType, value);
                    return true;
                }
            }

            // 字符串类型转换
            if (targetType == typeof(string))
            {
                result = value.ToString();
                return true;
            }

            // 基础类型转换
            if (targetType.IsPrimitive || targetType == typeof(decimal) || targetType == typeof(DateTime) || targetType == typeof(Guid))
            {
                result = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                return true;
            }

            // 使用 Convert.ChangeType
            result = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region 特定类型转换

    /// <summary>
    /// 转换为布尔值
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>布尔值</returns>
    public static bool ToBool(object? value, bool defaultValue = false)
    {
        if (value == null)
        {
            return defaultValue;
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        if (value is string stringValue)
        {
            if (bool.TryParse(stringValue, out var result))
            {
                return result;
            }

            // 处理常见的字符串表示
            var lowerValue = stringValue.ToLowerInvariant().Trim();
            return lowerValue switch
            {
                "1" or "yes" or "y" or "on" or "true" or "t" => true,
                "0" or "no" or "n" or "off" or "false" or "f" => false,
                _ => defaultValue
            };
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToBoolean(CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// 转换为字节
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>字节值</returns>
    public static byte ToByte(object? value, byte defaultValue = 0)
    {
        if (value == null)
        {
            return defaultValue;
        }

        if (value is byte byteValue)
        {
            return byteValue;
        }

        if (value is string stringValue && byte.TryParse(stringValue, out var result))
        {
            return result;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToByte(CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// 转换为有符号字节
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>有符号字节值</returns>
    public static sbyte ToSByte(object? value, sbyte defaultValue = 0)
    {
        if (value == null)
        {
            return defaultValue;
        }

        if (value is sbyte sbyteValue)
        {
            return sbyteValue;
        }

        if (value is string stringValue && sbyte.TryParse(stringValue, out var result))
        {
            return result;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToSByte(CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// 转换为短整数
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>短整数值</returns>
    public static short ToShort(object? value, short defaultValue = 0)
    {
        if (value == null)
        {
            return defaultValue;
        }

        if (value is short shortValue)
        {
            return shortValue;
        }

        if (value is string stringValue && short.TryParse(stringValue, out var result))
        {
            return result;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToInt16(CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// 转换为无符号短整数
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>无符号短整数值</returns>
    public static ushort ToUShort(object? value, ushort defaultValue = 0)
    {
        if (value == null)
        {
            return defaultValue;
        }

        if (value is ushort ushortValue)
        {
            return ushortValue;
        }

        if (value is string stringValue && ushort.TryParse(stringValue, out var result))
        {
            return result;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToUInt16(CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// 转换为整数
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>整数值</returns>
    public static int ToInt(object? value, int defaultValue = 0)
    {
        if (value == null)
        {
            return defaultValue;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        if (value is string stringValue && int.TryParse(stringValue, out var result))
        {
            return result;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToInt32(CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// 转换为无符号整数
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>无符号整数值</returns>
    public static uint ToUInt(object? value, uint defaultValue = 0)
    {
        if (value == null)
        {
            return defaultValue;
        }

        if (value is uint uintValue)
        {
            return uintValue;
        }

        if (value is string stringValue && uint.TryParse(stringValue, out var result))
        {
            return result;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToUInt32(CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// 转换为长整数
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>长整数值</returns>
    public static long ToLong(object? value, long defaultValue = 0)
    {
        if (value == null)
        {
            return defaultValue;
        }

        if (value is long longValue)
        {
            return longValue;
        }

        if (value is string stringValue && long.TryParse(stringValue, out var result))
        {
            return result;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToInt64(CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// 转换为无符号长整数
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>无符号长整数值</returns>
    public static ulong ToULong(object? value, ulong defaultValue = 0)
    {
        if (value == null)
        {
            return defaultValue;
        }

        if (value is ulong ulongValue)
        {
            return ulongValue;
        }

        if (value is string stringValue && ulong.TryParse(stringValue, out var result))
        {
            return result;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToUInt64(CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// 转换为单精度浮点数
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>单精度浮点数值</returns>
    public static float ToFloat(object? value, float defaultValue = 0f)
    {
        if (value == null)
        {
            return defaultValue;
        }

        if (value is float floatValue)
        {
            return floatValue;
        }

        if (value is string stringValue && float.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToSingle(CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// 转换为双精度浮点数
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>双精度浮点数值</returns>
    public static double ToDouble(object? value, double defaultValue = 0.0)
    {
        if (value == null)
        {
            return defaultValue;
        }

        if (value is double doubleValue)
        {
            return doubleValue;
        }

        if (value is string stringValue && double.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToDouble(CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// 转换为十进制数
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>十进制数值</returns>
    public static decimal ToDecimal(object? value, decimal defaultValue = 0m)
    {
        if (value == null)
        {
            return defaultValue;
        }

        if (value is decimal decimalValue)
        {
            return decimalValue;
        }

        if (value is string stringValue && decimal.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToDecimal(CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// 转换为日期时间
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>日期时间值</returns>
    public static DateTime ToDateTime(object? value, DateTime defaultValue = default)
    {
        if (value == null)
        {
            return defaultValue;
        }

        if (value is DateTime dateTimeValue)
        {
            return dateTimeValue;
        }

        if (value is string stringValue && DateTime.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            return result;
        }

        if (value is IConvertible convertible)
        {
            try
            {
                return convertible.ToDateTime(CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// 转换为带时区的日期时间
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>带时区的日期时间值</returns>
    public static DateTimeOffset ToDateTimeOffset(object? value, DateTimeOffset defaultValue = default)
    {
        if (value == null)
        {
            return defaultValue;
        }

        if (value is DateTimeOffset dateTimeOffsetValue)
        {
            return dateTimeOffsetValue;
        }

        if (value is DateTime dateTime)
        {
            return new DateTimeOffset(dateTime);
        }

        if (value is string stringValue && DateTimeOffset.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            return result;
        }

        try
        {
            // 尝试先转换为 DateTime，然后转为 DateTimeOffset
            var dateTimeResult = ToDateTime(value);
            if (dateTimeResult != default)
            {
                return new DateTimeOffset(dateTimeResult);
            }
        }
        catch
        {
            // 忽略异常，返回默认值
        }

        return defaultValue;
    }

    /// <summary>
    /// 转换为 GUId
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>GUId 值</returns>
    public static Guid ToGuid(object? value, Guid defaultValue = default)
    {
        if (value == null)
        {
            return defaultValue;
        }

        if (value is Guid guidValue)
        {
            return guidValue;
        }

        if (value is string stringValue && Guid.TryParse(stringValue, out var result))
        {
            return result;
        }

        return defaultValue;
    }

    #endregion

    #region 数组和集合转换

    /// <summary>
    /// 转换为数组
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="value">要转换的值</param>
    /// <returns>数组</returns>
    public static T[] ToArray<T>(object? value)
    {
        if (value == null)
        {
            return [];
        }

        if (value is T[] array)
        {
            return array;
        }

        if (value is IEnumerable<T> enumerable)
        {
            return [.. enumerable];
        }

        if (value is IEnumerable nonGenericEnumerable)
        {
            var list = new List<T>();
            foreach (var item in nonGenericEnumerable)
            {
                if (TryConvertTo<T>(item, out var convertedItem))
                {
                    list.Add(convertedItem);
                }
            }
            return [.. list];
        }

        // 尝试作为单个元素
        if (TryConvertTo<T>(value, out var singleItem))
        {
            return [singleItem];
        }

        return [];
    }

    /// <summary>
    /// 转换为列表
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="value">要转换的值</param>
    /// <returns>列表</returns>
    public static List<T> ToList<T>(object? value)
    {
        return [.. ToArray<T>(value)];
    }

    #endregion
}
