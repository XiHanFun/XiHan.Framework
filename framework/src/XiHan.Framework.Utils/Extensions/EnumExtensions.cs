#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumExtensions
// Guid:5f4e3d2c-1b0a-9e8d-7c6b-5a4f3e2d1c0b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/19 23:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Core;
using XiHan.Framework.Utils.Enums;

namespace XiHan.Framework.Utils.Extensions;

/// <summary>
/// 枚举扩展方法
/// </summary>
public static class EnumExtensions
{
    #region 获取描述和主题

    /// <summary>
    /// 获取枚举值的描述
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>描述信息</returns>
    public static string GetDescription<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
    {
        return EnumHelper.GetDescription(enumValue);
    }

    /// <summary>
    /// 获取枚举值的主题
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>主题名称</returns>
    public static string? GetTheme<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
    {
        return EnumHelper.GetTheme(enumValue);
    }

    /// <summary>
    /// 获取枚举值的显示名称（描述优先，否则返回名称）
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>显示名称</returns>
    public static string GetDisplayName<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
    {
        var description = enumValue.GetDescription();
        return string.IsNullOrEmpty(description) ? enumValue.ToString() : description;
    }

    #endregion

    #region 枚举验证

    /// <summary>
    /// 判断枚举值是否已定义
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>是否已定义</returns>
    public static bool IsDefined<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
    {
        return EnumHelper.IsValidEnum(enumValue);
    }

    /// <summary>
    /// 判断枚举是否包含指定标志（仅适用于 Flags 枚举）
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
    /// 判断枚举是否包含任意指定标志（仅适用于 Flags 枚举）
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <param name="flags">要检查的标志</param>
    /// <returns>是否包含任意标志</returns>
    public static bool HasAnyFlag<TEnum>(this TEnum enumValue, params TEnum[] flags) where TEnum : struct, Enum
    {
        return flags.Any(flag => enumValue.HasFlag(flag));
    }

    /// <summary>
    /// 判断枚举是否包含所有指定标志（仅适用于 Flags 枚举）
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <param name="flags">要检查的标志</param>
    /// <returns>是否包含所有标志</returns>
    public static bool HasAllFlags<TEnum>(this TEnum enumValue, params TEnum[] flags) where TEnum : struct, Enum
    {
        return flags.All(flag => enumValue.HasFlag(flag));
    }

    #endregion

    #region 枚举转换

    /// <summary>
    /// 转换为整数值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>整数值</returns>
    public static int ToInt<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
    {
        return Convert.ToInt32(enumValue);
    }

    /// <summary>
    /// 转换为长整数值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>长整数值</returns>
    public static long ToLong<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
    {
        return Convert.ToInt64(enumValue);
    }

    /// <summary>
    /// 转换为字节值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>字节值</returns>
    public static byte ToByte<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
    {
        return Convert.ToByte(enumValue);
    }

    /// <summary>
    /// 转换为短整数值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>短整数值</returns>
    public static short ToShort<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
    {
        return Convert.ToInt16(enumValue);
    }

    /// <summary>
    /// 转换为枚举项对象
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>枚举项对象</returns>
    public static EnumItem<TEnum> ToEnumItem<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
    {
        return new EnumItem<TEnum>
        {
            Key = enumValue.ToString(),
            Value = enumValue,
            Description = enumValue.GetDescription(),
            Theme = enumValue.GetTheme()
        };
    }

    #endregion

    #region 枚举操作（Flags 枚举）

    /// <summary>
    /// 添加标志（仅适用于 Flags 枚举）
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <param name="flag">要添加的标志</param>
    /// <returns>添加标志后的枚举值</returns>
    public static TEnum AddFlag<TEnum>(this TEnum enumValue, TEnum flag) where TEnum : struct, Enum
    {
        var enumType = typeof(TEnum);
        if (!enumType.IsDefined(typeof(FlagsAttribute), false))
        {
            throw new ArgumentException($"枚举类型 {enumType.Name} 不是 Flags 枚举");
        }

        var underlyingType = Enum.GetUnderlyingType(enumType);
        dynamic value1 = Convert.ChangeType(enumValue, underlyingType)!;
        dynamic value2 = Convert.ChangeType(flag, underlyingType)!;
        var result = value1 | value2;
        return (TEnum)Enum.ToObject(enumType, result);
    }

    /// <summary>
    /// 移除标志（仅适用于 Flags 枚举）
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <param name="flag">要移除的标志</param>
    /// <returns>移除标志后的枚举值</returns>
    public static TEnum RemoveFlag<TEnum>(this TEnum enumValue, TEnum flag) where TEnum : struct, Enum
    {
        var enumType = typeof(TEnum);
        if (!enumType.IsDefined(typeof(FlagsAttribute), false))
        {
            throw new ArgumentException($"枚举类型 {enumType.Name} 不是 Flags 枚举");
        }

        var underlyingType = Enum.GetUnderlyingType(enumType);
        dynamic value1 = Convert.ChangeType(enumValue, underlyingType)!;
        dynamic value2 = Convert.ChangeType(flag, underlyingType)!;
        var result = value1 & ~value2;
        return (TEnum)Enum.ToObject(enumType, result);
    }

    /// <summary>
    /// 切换标志（仅适用于 Flags 枚举）
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <param name="flag">要切换的标志</param>
    /// <returns>切换标志后的枚举值</returns>
    public static TEnum ToggleFlag<TEnum>(this TEnum enumValue, TEnum flag) where TEnum : struct, Enum
    {
        return enumValue.HasFlag(flag) ? enumValue.RemoveFlag(flag) : enumValue.AddFlag(flag);
    }

    /// <summary>
    /// 获取所有标志（仅适用于 Flags 枚举）
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <returns>包含的所有标志</returns>
    public static IEnumerable<TEnum> GetFlags<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
    {
        var enumType = typeof(TEnum);
        if (!enumType.IsDefined(typeof(FlagsAttribute), false))
        {
            throw new ArgumentException($"枚举类型 {enumType.Name} 不是 Flags 枚举");
        }

        var flags = new List<TEnum>();
        var allValues = Enum.GetValues<TEnum>();

        foreach (var value in allValues)
        {
            if (enumValue.HasFlag(value) && !value.Equals(default(TEnum)))
            {
                flags.Add(value);
            }
        }

        return flags;
    }

    #endregion

    #region 枚举导航

    /// <summary>
    /// 获取下一个枚举值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">当前枚举值</param>
    /// <param name="loop">是否循环（到达末尾时回到开头）</param>
    /// <returns>下一个枚举值，如果是最后一个且不循环则返回 null</returns>
    public static TEnum? GetNext<TEnum>(this TEnum enumValue, bool loop = false) where TEnum : struct, Enum
    {
        var values = Enum.GetValues<TEnum>();
        var currentIndex = Array.IndexOf(values, enumValue);

        if (currentIndex == -1)
        {
            return null;
        }

        if (currentIndex == values.Length - 1)
        {
            return loop ? values[0] : null;
        }

        return values[currentIndex + 1];
    }

    /// <summary>
    /// 获取上一个枚举值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">当前枚举值</param>
    /// <param name="loop">是否循环（到达开头时回到末尾）</param>
    /// <returns>上一个枚举值，如果是第一个且不循环则返回 null</returns>
    public static TEnum? GetPrevious<TEnum>(this TEnum enumValue, bool loop = false) where TEnum : struct, Enum
    {
        var values = Enum.GetValues<TEnum>();
        var currentIndex = Array.IndexOf(values, enumValue);

        if (currentIndex == -1)
        {
            return null;
        }

        if (currentIndex == 0)
        {
            return loop ? values[^1] : null;
        }

        return values[currentIndex - 1];
    }

    /// <summary>
    /// 获取枚举的所有值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="_"></param>
    /// <returns>所有枚举值</returns>
    public static TEnum[] GetAllValues<TEnum>(this TEnum _) where TEnum : struct, Enum
    {
        return Enum.GetValues<TEnum>();
    }

    /// <summary>
    /// 获取枚举的所有名称
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="_"></param>
    /// <returns>所有枚举名称</returns>
    public static string[] GetAllNames<TEnum>(this TEnum _) where TEnum : struct, Enum
    {
        return Enum.GetNames<TEnum>();
    }

    #endregion

    #region 枚举比较

    /// <summary>
    /// 比较两个枚举值的大小
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <param name="other">要比较的枚举值</param>
    /// <returns>比较结果</returns>
    public static int CompareTo<TEnum>(this TEnum enumValue, TEnum other) where TEnum : struct, Enum
    {
        var underlyingType = Enum.GetUnderlyingType(typeof(TEnum));
        var value1 = Convert.ChangeType(enumValue, underlyingType);
        var value2 = Convert.ChangeType(other, underlyingType);

        if (value1 is IComparable comparable)
        {
            return comparable.CompareTo(value2);
        }

        return 0;
    }

    /// <summary>
    /// 判断枚举值是否在指定范围内
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>是否在范围内</returns>
    public static bool IsBetween<TEnum>(this TEnum enumValue, TEnum min, TEnum max) where TEnum : struct, Enum
    {
        return enumValue.CompareTo(min) >= 0 && enumValue.CompareTo(max) <= 0;
    }

    #endregion

    #region 字符串相关

    /// <summary>
    /// 获取枚举值的字符串表示（支持自定义格式）
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <param name="format">格式（G=名称, D=数值, X=十六进制, F=标志格式）</param>
    /// <returns>格式化的字符串</returns>
    public static string ToString<TEnum>(this TEnum enumValue, string format) where TEnum : struct, Enum
    {
        return enumValue.ToString(format);
    }

    /// <summary>
    /// 获取枚举值的 JSON 字符串表示
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumValue">枚举值</param>
    /// <param name="useDescription">是否使用描述作为值</param>
    /// <returns>JSON 字符串</returns>
    public static string ToJsonString<TEnum>(this TEnum enumValue, bool useDescription = false) where TEnum : struct, Enum
    {
        var value = useDescription ? enumValue.GetDescription() : enumValue.ToString();
        return @$"""{value}""";
    }

    #endregion
}