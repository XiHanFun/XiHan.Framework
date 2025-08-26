#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumExtensions
// Guid:9ce569e4-6869-4251-8dc5-fad69e9d56e6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/22 1:56:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Conversions;
using XiHan.Framework.Utils.Reflections;
using XiHan.Framework.Utils.Themes;

namespace XiHan.Framework.Utils.Extensions;

/// <summary>
/// 枚举扩展方法
/// </summary>
public static class EnumExtensions
{
    #region 获取枚举值

    /// <summary>
    /// 获取枚举的整数值
    /// </summary>
    /// <param name="enumValue">枚举值</param>
    /// <returns>整数值</returns>
    public static int GetValue(this Enum enumValue)
    {
        return Convert.ToInt32(enumValue);
    }

    /// <summary>
    /// 获取枚举的整数值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>整数值</returns>
    public static int GetValue<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
    {
        return Convert.ToInt32(enumValue);
    }

    /// <summary>
    /// 获取枚举的名称
    /// </summary>
    /// <param name="enumValue">枚举值</param>
    /// <returns>枚举名称</returns>
    public static string GetName(this Enum enumValue)
    {
        return enumValue.ToString();
    }

    /// <summary>
    /// 获取枚举的名称
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>枚举名称</returns>
    public static string GetName<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
    {
        return enumValue.ToString();
    }

    #endregion 获取枚举值

    #region 获取枚举描述

    /// <summary>
    /// 获取枚举的描述信息
    /// </summary>
    /// <param name="enumValue">枚举值</param>
    /// <returns>描述信息</returns>
    public static string GetDescription(this Enum enumValue)
    {
        var fieldInfo = enumValue.GetType().GetField(enumValue.ToString());
        return fieldInfo?.GetDescriptionValue() ?? string.Empty;
    }

    /// <summary>
    /// 获取枚举的描述信息
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>描述信息</returns>
    public static string GetDescription<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
    {
        var fieldInfo = typeof(TEnum).GetField(enumValue.ToString());
        return fieldInfo?.GetDescriptionValue() ?? string.Empty;
    }

    #endregion 获取枚举描述

    #region 获取枚举主题

    /// <summary>
    /// 获取枚举的主题信息
    /// </summary>
    /// <param name="enumValue">枚举值</param>
    /// <returns>主题信息</returns>
    public static ThemeColor GetTheme(this Enum enumValue)
    {
        var fieldInfo = enumValue.GetType().GetField(enumValue.ToString());
        return fieldInfo?.GetThemeColorValue() ?? new ThemeColor();
    }

    /// <summary>
    /// 获取枚举的主题信息
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>主题信息</returns>
    public static ThemeColor GetTheme<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
    {
        var fieldInfo = typeof(TEnum).GetField(enumValue.ToString());
        return fieldInfo?.GetThemeColorValue() ?? new ThemeColor();
    }

    #endregion 获取枚举主题

    #region 枚举转换

    /// <summary>
    /// 尝试转换为指定枚举类型
    /// </summary>
    /// <typeparam name="TEnum">目标枚举类型</typeparam>
    /// <param name="enumValue">源枚举值</param>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    public static bool TryConvertTo<TEnum>(this Enum enumValue, out TEnum result) where TEnum : struct, Enum
    {
        try
        {
            var value = enumValue.GetValue();
            if (Enum.IsDefined(typeof(TEnum), value))
            {
                result = (TEnum)Enum.ToObject(typeof(TEnum), value);
                return true;
            }
        }
        catch
        {
            // 忽略异常
        }

        result = default;
        return false;
    }

    /// <summary>
    /// 转换为指定枚举类型
    /// </summary>
    /// <typeparam name="TEnum">目标枚举类型</typeparam>
    /// <param name="enumValue">源枚举值</param>
    /// <returns>转换结果</returns>
    /// <exception cref="InvalidCastException">无法转换时抛出异常</exception>
    public static TEnum ConvertTo<TEnum>(this Enum enumValue) where TEnum : struct, Enum
    {
        return enumValue.TryConvertTo<TEnum>(out var result)
            ? result
            : throw new InvalidCastException($"无法将枚举 {enumValue.GetType().Name}.{enumValue} 转换为 {typeof(TEnum).Name}");
    }

    #endregion 枚举转换

    #region 枚举验证

    /// <summary>
    /// 检查枚举是否有指定的标志
    /// </summary>
    /// <param name="enumValue">枚举值</param>
    /// <param name="flag">要检查的标志</param>
    /// <returns>是否包含标志</returns>
    public static bool HasFlag(this Enum enumValue, Enum flag)
    {
        return enumValue.HasFlag(flag);
    }

    /// <summary>
    /// 检查枚举是否有指定的标志
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <param name="flag">要检查的标志</param>
    /// <returns>是否包含标志</returns>
    public static bool HasFlag<TEnum>(this TEnum enumValue, TEnum flag) where TEnum : struct, Enum
    {
        return enumValue.HasFlag(flag);
    }

    /// <summary>
    /// 检查枚举是否为默认值
    /// </summary>
    /// <param name="enumValue">枚举值</param>
    /// <returns>是否为默认值</returns>
    public static bool IsDefault(this Enum enumValue)
    {
        return enumValue.GetValue() == 0;
    }

    /// <summary>
    /// 检查枚举是否为默认值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>是否为默认值</returns>
    public static bool IsDefault<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
    {
        return enumValue.GetValue() == 0;
    }

    #endregion 枚举验证

    #region 枚举操作

    /// <summary>
    /// 获取枚举的下一个值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">当前枚举值</param>
    /// <param name="circular">是否循环，默认为true</param>
    /// <returns>下一个枚举值</returns>
    public static TEnum GetNext<TEnum>(this TEnum enumValue, bool circular = true) where TEnum : struct, Enum
    {
        var values = EnumHelper<TEnum>.GetValues();
        var currentIndex = Array.IndexOf(values, enumValue);

        if (currentIndex == -1)
        {
            return values.Length > 0 ? values[0] : default;
        }

        var nextIndex = currentIndex + 1;
        return nextIndex >= values.Length ? circular ? values[0] : enumValue : values[nextIndex];
    }

    /// <summary>
    /// 获取枚举的前一个值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">当前枚举值</param>
    /// <param name="circular">是否循环，默认为true</param>
    /// <returns>前一个枚举值</returns>
    public static TEnum GetPrevious<TEnum>(this TEnum enumValue, bool circular = true) where TEnum : struct, Enum
    {
        var values = EnumHelper<TEnum>.GetValues();
        var currentIndex = Array.IndexOf(values, enumValue);

        if (currentIndex == -1)
        {
            return values.Length > 0 ? values[^1] : default;
        }

        var prevIndex = currentIndex - 1;
        return prevIndex < 0 ? circular ? values[^1] : enumValue : values[prevIndex];
    }

    #endregion 枚举操作
}
