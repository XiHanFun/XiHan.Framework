﻿#region <<版权版本注释>>

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
using System.Reflection;
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

    /// <summary>
    /// 根据键获取单个枚举的值
    /// </summary>
    /// <param name="keyEnum"></param>
    /// <returns></returns>
    public static int GetValue(this Enum keyEnum)
    {
        var enumName = keyEnum.ToString();
        var field = keyEnum.GetType().GetField(enumName);
        return field is null ? throw new ArgumentException(null, nameof(keyEnum)) : (int)field.GetRawConstantValue()!;
    }

    /// <summary>
    /// 根据键获取单个枚举的描述信息
    /// </summary>
    /// <param name="keyEnum"></param>
    /// <returns></returns>
    public static string GetDescription(this Enum keyEnum)
    {
        var enumName = keyEnum.ToString();
        var field = keyEnum.GetType().GetField(enumName);
        return field is null
            ? string.Empty
            : field.GetDescriptionValue();
    }

    /// <summary>
    /// 根据键获取单个枚举的主题信息
    /// </summary>
    /// <param name="keyEnum"></param>
    /// <returns></returns>
    public static string GetTheme(this Enum keyEnum)
    {
        var enumName = keyEnum.ToString();
        var field = keyEnum.GetType().GetField(enumName);
        return field is null
            ? string.Empty
            : field.GetThemeValue();
    }

    /// <summary>
    /// 获取枚举信息列表
    /// </summary>
    /// <param name="enumType"></param>
    /// <returns></returns>
    public static IEnumerable<EnumInfo> GetEnumInfos(this Type enumType)
    {
        if (!enumType.IsEnum)
        {
            throw new ArgumentException("类型不是枚举类型", nameof(enumType));
        }

        // 缓存中有则直接返回
        var enumInfos = new List<EnumInfo>();
        if (EnumInfosCatch.TryGetValue(enumType, out var enumInfoList))
        {
            return enumInfoList;
        }

        // 枚举字段
        var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (var field in fields)
        {
            enumInfos.Add(new EnumInfo
            {
                Key = field.Name,
                Value = (int)field.GetRawConstantValue()!,
                Label = field.GetDescriptionValue(),
                Theme = field.GetThemeValue()
            });
        }

        // 加入缓存
        EnumInfosCatch.TryAdd(enumType, enumInfos);
        return enumInfos;
    }

    /// <summary>
    /// 从程序集中查找指定枚举类型
    /// </summary>
    /// <param name="assembly"></param>
    /// <returns></returns>
    public static IEnumerable<Type> GetEnumTypes(this Assembly assembly)
    {
        // 缓存中有则直接返回
        var enumTypes = new List<Type>();
        if (EnumTypesCatch.TryGetValue(assembly, out var enumTypeList))
        {
            return enumTypeList;
        }

        // 取程序集中所有类型
        var typeArray = assembly.GetTypes();
        // 过滤非枚举类型，转成字典格式并返回
        EnumTypesCatch.TryAdd(assembly, typeArray.Where(o => o.IsEnum));
        return enumTypes;
    }

    /// <summary>
    /// 从程序集中查找指定枚举类型
    /// </summary>
    /// <param name="assembly"></param>
    /// <param name="typeName"></param>
    /// <returns></returns>
    public static Type? GetEnumTypeByName(this Assembly assembly, string typeName)
    {
        var enumTypes = assembly.GetEnumTypes();
        return enumTypes.FirstOrDefault(o => o.Name == typeName);
    }
}

/// <summary>
/// 枚举信息
/// </summary>
public record EnumInfo
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

    /// <summary>
    /// 主题
    /// </summary>
    public string Theme { get; init; } = string.Empty;
}
