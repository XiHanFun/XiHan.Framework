#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConverterExtensions
// Guid:6f5e4d3c-2b1a-0f9e-8d7c-6b5a4f3e2d1c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/19 23:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Globalization;
using XiHan.Framework.Utils.Core;

namespace XiHan.Framework.Utils.Extensions;

/// <summary>
/// 类型转换扩展方法
/// </summary>
public static class ConverterExtensions
{
    #region Object 扩展

    /// <summary>
    /// 转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换后的值</returns>
    public static T ConvertTo<T>(this object? value, T defaultValue = default!)
    {
        return ConvertHelper.ConvertTo(value, defaultValue);
    }

    /// <summary>
    /// 尝试转换为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="value">要转换的值</param>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    public static bool TryConvertTo<T>(this object? value, out T result)
    {
        return ConvertHelper.TryConvertTo(value, out result);
    }

    /// <summary>
    /// 转换为布尔值
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>布尔值</returns>
    public static bool ConvertToBool(this object? value, bool defaultValue = false)
    {
        return ConvertHelper.ToBool(value, defaultValue);
    }

    /// <summary>
    /// 转换为字节
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>字节值</returns>
    public static byte ConvertToByte(this object? value, byte defaultValue = 0)
    {
        return ConvertHelper.ToByte(value, defaultValue);
    }

    /// <summary>
    /// 转换为有符号字节
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>有符号字节值</returns>
    public static sbyte ConvertToSByte(this object? value, sbyte defaultValue = 0)
    {
        return ConvertHelper.ToSByte(value, defaultValue);
    }

    /// <summary>
    /// 转换为短整数
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>短整数值</returns>
    public static short ConvertToShort(this object? value, short defaultValue = 0)
    {
        return ConvertHelper.ToShort(value, defaultValue);
    }

    /// <summary>
    /// 转换为无符号短整数
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>无符号短整数值</returns>
    public static ushort ConvertToUShort(this object? value, ushort defaultValue = 0)
    {
        return ConvertHelper.ToUShort(value, defaultValue);
    }

    /// <summary>
    /// 转换为整数
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>整数值</returns>
    public static int ConvertToInt(this object? value, int defaultValue = 0)
    {
        return ConvertHelper.ToInt(value, defaultValue);
    }

    /// <summary>
    /// 转换为无符号整数
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>无符号整数值</returns>
    public static uint ConvertToUInt(this object? value, uint defaultValue = 0)
    {
        return ConvertHelper.ToUInt(value, defaultValue);
    }

    /// <summary>
    /// 转换为长整数
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>长整数值</returns>
    public static long ConvertToLong(this object? value, long defaultValue = 0)
    {
        return ConvertHelper.ToLong(value, defaultValue);
    }

    /// <summary>
    /// 转换为无符号长整数
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>无符号长整数值</returns>
    public static ulong ConvertToULong(this object? value, ulong defaultValue = 0)
    {
        return ConvertHelper.ToULong(value, defaultValue);
    }

    /// <summary>
    /// 转换为单精度浮点数
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>单精度浮点数值</returns>
    public static float ConvertToFloat(this object? value, float defaultValue = 0f)
    {
        return ConvertHelper.ToFloat(value, defaultValue);
    }

    /// <summary>
    /// 转换为双精度浮点数
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>双精度浮点数值</returns>
    public static double ConvertToDouble(this object? value, double defaultValue = 0.0)
    {
        return ConvertHelper.ToDouble(value, defaultValue);
    }

    /// <summary>
    /// 转换为十进制数
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>十进制数值</returns>
    public static decimal ConvertToDecimal(this object? value, decimal defaultValue = 0m)
    {
        return ConvertHelper.ToDecimal(value, defaultValue);
    }

    /// <summary>
    /// 转换为日期时间
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>日期时间值</returns>
    public static DateTime ConvertToDateTime(this object? value, DateTime defaultValue = default)
    {
        return ConvertHelper.ToDateTime(value, defaultValue);
    }

    /// <summary>
    /// 转换为带时区的日期时间
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>带时区的日期时间值</returns>
    public static DateTimeOffset ConvertToDateTimeOffset(this object? value, DateTimeOffset defaultValue = default)
    {
        return ConvertHelper.ToDateTimeOffset(value, defaultValue);
    }

    /// <summary>
    /// 转换为 GUId
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>GUId 值</returns>
    public static Guid ConvertToGuid(this object? value, Guid defaultValue = default)
    {
        return ConvertHelper.ToGuid(value, defaultValue);
    }

    /// <summary>
    /// 转换为枚举值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>枚举值</returns>
    public static TEnum ConvertToEnum<TEnum>(this object? value, TEnum defaultValue = default!)
        where TEnum : struct, Enum
    {
        if (value == null)
        {
            return defaultValue;
        }

        if (EnumHelper.TryToEnum<TEnum>(value, out var result))
        {
            return result;
        }

        return defaultValue;
    }

    /// <summary>
    /// 转换为数组
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="value">要转换的值</param>
    /// <returns>数组</returns>
    public static T[] ConvertToArray<T>(this object? value)
    {
        return ConvertHelper.ToArray<T>(value);
    }

    /// <summary>
    /// 转换为列表
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="value">要转换的值</param>
    /// <returns>列表</returns>
    public static List<T> ConvertToList<T>(this object? value)
    {
        return ConvertHelper.ToList<T>(value);
    }

    #endregion

    #region 安全转换方法

    /// <summary>
    /// 安全转换为布尔值（不抛出异常）
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换结果</returns>
    public static bool ConvertToBoolSafe(this object? value, bool defaultValue = false)
    {
        try
        {
            return ConvertHelper.ToBool(value, defaultValue);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 安全转换为字节（不抛出异常）
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换结果</returns>
    public static byte ConvertToByteSafe(this object? value, byte defaultValue = 0)
    {
        try
        {
            return ConvertHelper.ToByte(value, defaultValue);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 安全转换为有符号字节（不抛出异常）
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换结果</returns>
    public static sbyte ConvertToSByteSafe(this object? value, sbyte defaultValue = 0)
    {
        try
        {
            return ConvertHelper.ToSByte(value, defaultValue);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 安全转换为短整数（不抛出异常）
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换结果</returns>
    public static short ConvertToShortSafe(this object? value, short defaultValue = 0)
    {
        try
        {
            return ConvertHelper.ToShort(value, defaultValue);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 安全转换为无符号短整数（不抛出异常）
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换结果</returns>
    public static ushort ConvertToUShortSafe(this object? value, ushort defaultValue = 0)
    {
        try
        {
            return ConvertHelper.ToUShort(value, defaultValue);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 安全转换为整数（不抛出异常）
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换结果</returns>
    public static int ConvertToIntSafe(this object? value, int defaultValue = 0)
    {
        try
        {
            return ConvertHelper.ToInt(value, defaultValue);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 安全转换为无符号整数（不抛出异常）
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换结果</returns>
    public static uint ConvertToUIntSafe(this object? value, uint defaultValue = 0)
    {
        try
        {
            return ConvertHelper.ToUInt(value, defaultValue);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 安全转换为长整数（不抛出异常）
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换结果</returns>
    public static long ConvertToLongSafe(this object? value, long defaultValue = 0)
    {
        try
        {
            return ConvertHelper.ToLong(value, defaultValue);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 安全转换为无符号长整数（不抛出异常）
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换结果</returns>
    public static ulong ConvertToULongSafe(this object? value, ulong defaultValue = 0)
    {
        try
        {
            return ConvertHelper.ToULong(value, defaultValue);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 安全转换为单精度浮点数（不抛出异常）
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换结果</returns>
    public static float ConvertToFloatSafe(this object? value, float defaultValue = 0f)
    {
        try
        {
            return ConvertHelper.ToFloat(value, defaultValue);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 安全转换为双精度浮点数（不抛出异常）
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换结果</returns>
    public static double ConvertToDoubleSafe(this object? value, double defaultValue = 0.0)
    {
        try
        {
            return ConvertHelper.ToDouble(value, defaultValue);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 安全转换为十进制数（不抛出异常）
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换结果</returns>
    public static decimal ConvertToDecimalSafe(this object? value, decimal defaultValue = 0m)
    {
        try
        {
            return ConvertHelper.ToDecimal(value, defaultValue);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 安全转换为日期时间（不抛出异常）
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换结果</returns>
    public static DateTime ConvertToDateTimeSafe(this object? value, DateTime defaultValue = default)
    {
        try
        {
            return ConvertHelper.ToDateTime(value, defaultValue);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 安全转换为带时区的日期时间（不抛出异常）
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换结果</returns>
    public static DateTimeOffset ConvertToDateTimeOffsetSafe(this object? value, DateTimeOffset defaultValue = default)
    {
        try
        {
            return ConvertHelper.ToDateTimeOffset(value, defaultValue);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 安全转换为 GUId（不抛出异常）
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>转换结果</returns>
    public static Guid ConvertToGuidSafe(this object? value, Guid defaultValue = default)
    {
        try
        {
            return ConvertHelper.ToGuid(value, defaultValue);
        }
        catch
        {
            return defaultValue;
        }
    }

    #endregion

    #region 格式化转换

    /// <summary>
    /// 转换为格式化字符串
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="format">格式字符串</param>
    /// <param name="provider">格式提供程序</param>
    /// <returns>格式化字符串</returns>
    public static string ConvertToFormattedString(this object? value, string? format = null, IFormatProvider? provider = null)
    {
        if (value == null)
        {
            return string.Empty;
        }

        provider ??= CultureInfo.CurrentCulture;

        return value switch
        {
            IFormattable formattable => formattable.ToString(format, provider),
            _ => value.ToString() ?? string.Empty
        };
    }

    /// <summary>
    /// 转换为不变文化格式字符串
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="format">格式字符串</param>
    /// <returns>格式化字符串</returns>
    public static string ConvertToInvariantString(this object? value, string? format = null)
    {
        return value.ConvertToFormattedString(format, CultureInfo.InvariantCulture);
    }

    #endregion

    #region 空值处理

    /// <summary>
    /// 转换为非空字符串
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>非空字符串</returns>
    public static string ConvertToNonNullString(this object? value, string defaultValue = "")
    {
        return value?.ToString() ?? defaultValue;
    }

    /// <summary>
    /// 转换为可空类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="value">要转换的值</param>
    /// <returns>可空类型值</returns>
    public static T? ConvertToNullable<T>(this object? value) where T : struct
    {
        if (value == null)
        {
            return null;
        }

        if (ConvertHelper.TryConvertTo<T>(value, out var result))
        {
            return result;
        }

        return null;
    }

    #endregion
}
