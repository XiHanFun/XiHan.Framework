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

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using XiHan.Framework.Utils.Attributes;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Utils.System;

/// <summary>
/// 枚举扩展方法
/// </summary>
public static class EnumExtensions
{
    // 枚举类型缓存
    private static readonly ConcurrentDictionary<Assembly, IEnumerable<Type>> EnumTypesCatch = [];

    // 枚举信息缓存
    private static readonly ConcurrentDictionary<Type, IEnumerable<EnumInfo>> EnumInfosCatch = [];

    // 枚举值缓存
    private static readonly ConcurrentDictionary<Type, Dictionary<string, int>> EnumValuesCatch = [];

    // 枚举描述缓存
    private static readonly ConcurrentDictionary<Type, Dictionary<int, string>> EnumDescriptionsCatch = [];

    #region 获取枚举值

    /// <summary>
    /// 根据键获取单个枚举的值
    /// </summary>
    /// <param name="keyEnum">枚举对象</param>
    /// <returns>枚举值</returns>
    public static int GetValue(this Enum keyEnum)
    {
        var enumName = keyEnum.ToString();
        var field = keyEnum.GetType().GetField(enumName);
        return field is null ? throw new ArgumentException(null, nameof(keyEnum)) : (int)field.GetRawConstantValue()!;
    }

    /// <summary>
    /// 根据枚举类型和枚举名称获取枚举值
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <param name="enumName">枚举名称</param>
    /// <returns>枚举值</returns>
    public static int GetValue(Type enumType, string enumName)
    {
        if (!enumType.IsEnum)
        {
            throw new ArgumentException("类型不是枚举类型", nameof(enumType));
        }

        var field = enumType.GetField(enumName, BindingFlags.Public | BindingFlags.Static);
        return field is null
            ? throw new ArgumentException($"枚举类型 {enumType.Name} 中不存在名称为 {enumName} 的枚举值", nameof(enumName))
            : (int)field.GetRawConstantValue()!;
    }

    /// <summary>
    /// 获取指定枚举类型的所有值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>枚举值集合</returns>
    public static IEnumerable<int> GetValues<TEnum>() where TEnum : struct, Enum
    {
        var values = Enum.GetValues<TEnum>();
        foreach (var value in values)
        {
            yield return Convert.ToInt32(value);
        }
    }

    /// <summary>
    /// 获取指定枚举类型的所有值
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <returns>枚举值集合</returns>
    public static IEnumerable<int> GetValues(Type enumType)
    {
        if (!enumType.IsEnum)
        {
            throw new ArgumentException("类型不是枚举类型", nameof(enumType));
        }

        var values = Enum.GetValues(enumType);
        foreach (var value in values)
        {
            yield return Convert.ToInt32(value);
        }
    }

    #endregion 获取枚举值

    #region 获取枚举对象

    /// <summary>
    /// 根据枚举类型和枚举名称获取枚举对象
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumName">枚举名称</param>
    /// <returns>枚举对象</returns>
    public static TEnum GetEnum<TEnum>(string enumName) where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(enumName, out var result)
            ? result
            : throw new ArgumentException($"枚举类型 {typeof(TEnum).Name} 中不存在名称为 {enumName} 的枚举值", nameof(enumName));
    }

    /// <summary>
    /// 根据枚举值获取枚举对象
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <returns>枚举对象</returns>
    public static TEnum GetEnum<TEnum>(int value) where TEnum : struct, Enum
    {
        return Enum.IsDefined(typeof(TEnum), value)
            ? (TEnum)Enum.ToObject(typeof(TEnum), value)
            : throw new ArgumentException($"枚举类型 {typeof(TEnum).Name} 中不存在值为 {value} 的枚举", nameof(value));
    }

    /// <summary>
    /// 根据枚举描述获取枚举对象
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="description">枚举描述</param>
    /// <returns>枚举对象</returns>
    public static TEnum GetEnumByDescription<TEnum>(string description) where TEnum : struct, Enum
    {
        var type = typeof(TEnum);
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var desc = field.GetDescriptionValue();
            if (desc == description)
            {
                return (TEnum)field.GetValue(null)!;
            }
        }
        throw new ArgumentException($"枚举类型 {typeof(TEnum).Name} 中不存在描述为 {description} 的枚举", nameof(description));
    }

    /// <summary>
    /// 获取指定枚举类型的所有枚举对象
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>枚举对象集合</returns>
    public static IEnumerable<TEnum> GetEnums<TEnum>() where TEnum : struct, Enum
    {
        return Enum.GetValues<TEnum>();
    }

    #endregion 获取枚举对象

    #region 获取枚举描述

    /// <summary>
    /// 根据键获取单个枚举的描述信息
    /// </summary>
    /// <param name="keyEnum">枚举对象</param>
    /// <returns>描述信息</returns>
    public static string GetDescription(this Enum keyEnum)
    {
        var enumName = keyEnum.ToString();
        var field = keyEnum.GetType().GetField(enumName);
        return field is null
            ? string.Empty
            : field.GetDescriptionValue();
    }

    /// <summary>
    /// 根据枚举类型和枚举值获取描述信息
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <returns>描述信息</returns>
    public static string GetDescription<TEnum>(int value) where TEnum : struct, Enum
    {
        try
        {
            var enumObj = GetEnum<TEnum>(value);
            return enumObj.GetDescription();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 获取指定枚举类型的所有描述信息
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>描述信息集合</returns>
    public static IEnumerable<string> GetDescriptions<TEnum>() where TEnum : struct, Enum
    {
        var type = typeof(TEnum);
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            yield return field.GetDescriptionValue();
        }
    }

    #endregion 获取枚举描述

    #region 获取枚举主题

    /// <summary>
    /// 根据键获取单个枚举的主题信息
    /// </summary>
    /// <param name="keyEnum">枚举对象</param>
    /// <returns>主题信息</returns>
    public static ThemeColor GetTheme(this Enum keyEnum)
    {
        var enumName = keyEnum.ToString();
        var field = keyEnum.GetType().GetField(enumName);
        return field is null
            ? new ThemeColor { Theme = "default", Color = "#35495E" }
            : field.GetThemeColorValue();
    }

    #endregion 获取枚举主题

    #region 获取枚举信息

    /// <summary>
    /// 获取枚举信息列表
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <returns>枚举信息列表</returns>
    public static IEnumerable<EnumInfo> GetEnumInfos(this Type enumType)
    {
        if (!enumType.IsEnum)
        {
            throw new ArgumentException("类型不是枚举类型", nameof(enumType));
        }

        // 缓存中有则直接返回
        if (EnumInfosCatch.TryGetValue(enumType, out var enumInfoList))
        {
            return enumInfoList;
        }

        // 枚举字段
        var enumInfos = new List<EnumInfo>();
        var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (var field in fields)
        {
            enumInfos.Add(new EnumInfo
            {
                Key = field.Name,
                Value = (int)field.GetRawConstantValue()!,
                Label = field.GetDescriptionValue(),
                Theme = field.GetThemeColorValue().Theme,
                Color = field.GetThemeColorValue().Color
            });
        }

        // 加入缓存
        EnumInfosCatch.TryAdd(enumType, enumInfos);
        return enumInfos;
    }

    /// <summary>
    /// 获取枚举名称和值的字典
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>名称值字典</returns>
    public static Dictionary<string, int> GetNameValueDict<TEnum>() where TEnum : struct, Enum
    {
        var type = typeof(TEnum);

        // 缓存中有则直接返回
        if (EnumValuesCatch.TryGetValue(type, out var dict))
        {
            return dict;
        }

        dict = [];
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (var field in fields)
        {
            dict.Add(field.Name, (int)field.GetRawConstantValue()!);
        }

        // 加入缓存
        EnumValuesCatch.TryAdd(type, dict);
        return dict;
    }

    /// <summary>
    /// 获取枚举值和描述的字典
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>值描述字典</returns>
    public static Dictionary<int, string> GetValueDescriptionDict<TEnum>() where TEnum : struct, Enum
    {
        var type = typeof(TEnum);

        // 缓存中有则直接返回
        if (EnumDescriptionsCatch.TryGetValue(type, out var dict))
        {
            return dict;
        }

        dict = [];
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (var field in fields)
        {
            dict.Add((int)field.GetRawConstantValue()!, field.GetDescriptionValue());
        }

        // 加入缓存
        EnumDescriptionsCatch.TryAdd(type, dict);
        return dict;
    }

    #endregion 获取枚举信息

    #region 枚举检查

    /// <summary>
    /// 检查指定的值是否定义在枚举中
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">要检查的值</param>
    /// <returns>如果值在枚举中定义，则为true；否则为false</returns>
    public static bool IsDefined<TEnum>(int value) where TEnum : struct, Enum
    {
        return Enum.IsDefined(typeof(TEnum), value);
    }

    /// <summary>
    /// 检查指定的名称是否定义在枚举中
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="name">要检查的名称</param>
    /// <returns>如果名称在枚举中定义，则为true；否则为false</returns>
    public static bool IsDefined<TEnum>(string name) where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(name, out _);
    }

    /// <summary>
    /// 检查指定的描述是否在枚举中使用
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="description">要检查的描述</param>
    /// <returns>如果描述在枚举中使用，则为true；否则为false</returns>
    public static bool IsDescriptionDefined<TEnum>(string description) where TEnum : struct, Enum
    {
        var type = typeof(TEnum);
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var desc = field.GetDescriptionValue();
            if (desc == description)
            {
                return true;
            }
        }
        return false;
    }

    #endregion 枚举检查

    #region 枚举类型查找

    /// <summary>
    /// 从程序集中查找所有枚举类型
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <returns>枚举类型集合</returns>
    public static IEnumerable<Type> GetEnumTypes(this Assembly assembly)
    {
        // 缓存中有则直接返回
        if (EnumTypesCatch.TryGetValue(assembly, out var enumTypeList))
        {
            return enumTypeList;
        }

        // 取程序集中所有类型
        var typeArray = assembly.GetTypes();
        // 过滤非枚举类型
        var enumTypes = typeArray.Where(o => o.IsEnum).ToList();

        // 加入缓存
        EnumTypesCatch.TryAdd(assembly, enumTypes);
        return enumTypes;
    }

    /// <summary>
    /// 从程序集中查找指定名称的枚举类型
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <param name="typeName">枚举类型名称</param>
    /// <returns>枚举类型</returns>
    public static Type? GetEnumTypeByName(this Assembly assembly, string typeName)
    {
        var enumTypes = assembly.GetEnumTypes();
        return enumTypes.FirstOrDefault(o => o.Name == typeName);
    }

    #endregion 枚举类型查找
}

/// <summary>
/// 枚举信息
/// </summary>
public record EnumInfo : ThemeColor
{
    /// <summary>
    /// 键
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 值
    /// </summary>
    public int Value { get; init; }

    /// <summary>
    /// 描述
    /// </summary>
    public string Label { get; init; } = string.Empty;
}
