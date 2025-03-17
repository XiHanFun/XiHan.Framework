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
using System.Reflection;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Utils.System;

/// <summary>
/// 枚举扩展方法
/// </summary>
public static class EnumExtensions
{
    // 枚举信息缓存
    private static readonly ConcurrentDictionary<Type, IEnumerable<EnumInfo>> EnumInfosCatch = [];

    // 枚举类型缓存
    private static ConcurrentDictionary<string, Type> EnumTypeDict = [];

    /// <summary>
    /// 根据键获取单个枚举的值
    /// </summary>
    /// <param name="keyEnum"></param>
    /// <returns></returns>
    public static int GetValue(this Enum keyEnum)
    {
        var enumName = keyEnum.ToString();
        var field = keyEnum.GetType().GetField(enumName);
        return field == null ? throw new ArgumentException(null, nameof(keyEnum)) : (int)field.GetRawConstantValue()!;
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
        return field == null
            ? string.Empty
            : field.GetDescriptionValue();
    }

    /// <summary>
    /// 根据名称匹配枚举
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static TEnum GetEnumByName<TEnum>(this string name)
        where TEnum : struct
    {
        var tEnum = Enum.Parse<TEnum>(name, true);
        return tEnum;
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
    /// <param name="typeName"></param>
    /// <returns></returns>
    public static Type? TryToGetEnumType(this Assembly assembly, string typeName)
    {
        // 枚举缓存为空则重新加载枚举类型字典
        EnumTypeDict ??= LoadEnumTypeDict(assembly);

        // 按名称查找
        return EnumTypeDict.TryGetValue(typeName, out var value) ? value : null;
    }

    /// <summary>
    /// 从程序集中加载所有枚举类型
    /// </summary>
    /// <param name="assembly"></param>
    /// <returns></returns>
    public static ConcurrentDictionary<string, Type> LoadEnumTypeDict(this Assembly assembly)
    {
        // 取程序集中所有类型
        var typeArray = assembly.GetTypes();

        // 过滤非枚举类型，转成字典格式并返回
        var dict = typeArray.Where(o => o.IsEnum).ToDictionary(o => o.Name, o => o);
        var enumTypeDict = new ConcurrentDictionary<string, Type>(dict);
        return enumTypeDict;
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
