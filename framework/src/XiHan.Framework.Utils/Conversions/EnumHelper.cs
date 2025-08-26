#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumHelper
// Guid:eb3bf937-4be6-405e-9885-4292db553217
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/22 1:56:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Reflection;
using XiHan.Framework.Utils.Conversions.Dtos;
using XiHan.Framework.Utils.Extensions;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Utils.Conversions;

/// <summary>
/// 枚举辅助类
/// </summary>
public static class EnumHelper
{
    // 枚举信息缓存
    private static readonly ConcurrentDictionary<Type, IEnumerable<EnumInfo>> EnumInfosCatch = [];

    // 枚举值缓存
    private static readonly ConcurrentDictionary<Type, Dictionary<string, int>> EnumValuesCatch = [];

    // 枚举描述缓存
    private static readonly ConcurrentDictionary<Type, Dictionary<int, string>> EnumDescriptionsCatch = [];

    // 枚举类型缓存
    private static readonly ConcurrentDictionary<Assembly, IEnumerable<Type>> EnumTypesCatch = [];

    #region 枚举验证

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
    /// <param name="ignoreCase">是否忽略大小写</param>
    /// <returns>如果名称在枚举中定义，则为true；否则为false</returns>
    public static bool IsDefined<TEnum>(string name, bool ignoreCase = false) where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(name, ignoreCase, out _);
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
        return type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => field.GetDescriptionValue())
            .Any(desc => desc == description);
    }

    #endregion 枚举验证

    #region 获取枚举描述

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
    /// 根据枚举名称获取描述信息
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="name">枚举名称</param>
    /// <returns>描述信息</returns>
    public static string GetDescription<TEnum>(string name) where TEnum : struct, Enum
    {
        try
        {
            var enumObj = GetEnum<TEnum>(name);
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

    #region 获取枚举对象

    /// <summary>
    /// 根据枚举类型和枚举名称获取枚举对象
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumName">枚举名称</param>
    /// <param name="ignoreCase">是否忽略大小写</param>
    /// <returns>枚举对象</returns>
    public static TEnum GetEnum<TEnum>(string enumName, bool ignoreCase = false) where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(enumName, ignoreCase, out var result)
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
    /// 尝试根据枚举名称获取枚举对象
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="enumName">枚举名称</param>
    /// <param name="result">输出的枚举对象</param>
    /// <param name="ignoreCase">是否忽略大小写</param>
    /// <returns>是否成功获取</returns>
    public static bool TryGetEnum<TEnum>(string enumName, out TEnum result, bool ignoreCase = false) where TEnum : struct, Enum
    {
        return Enum.TryParse(enumName, ignoreCase, out result);
    }

    /// <summary>
    /// 尝试根据枚举值获取枚举对象
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <param name="result">输出的枚举对象</param>
    /// <returns>是否成功获取</returns>
    public static bool TryGetEnum<TEnum>(int value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;
        if (!Enum.IsDefined(typeof(TEnum), value))
        {
            return false;
        }

        result = (TEnum)Enum.ToObject(typeof(TEnum), value);
        return true;
    }

    /// <summary>
    /// 尝试根据枚举描述获取枚举对象
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="description">枚举描述</param>
    /// <param name="result">输出的枚举对象</param>
    /// <returns>是否成功获取</returns>
    public static bool TryGetEnumByDescription<TEnum>(string description, out TEnum result) where TEnum : struct, Enum
    {
        result = default;
        var type = typeof(TEnum);
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var desc = field.GetDescriptionValue();
            if (desc == description)
            {
                result = (TEnum)field.GetValue(null)!;
                return true;
            }
        }
        return false;
    }

    #endregion 获取枚举对象

    #region 获取枚举信息

    /// <summary>
    /// 获取枚举信息列表
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <returns>枚举信息列表</returns>
    public static IEnumerable<EnumInfo> GetEnumInfos(Type enumType)
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
        var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
        var enumInfos = fields.Select(field => new EnumInfo
        {
            Key = field.Name,
            Value = (int)field.GetRawConstantValue()!,
            Label = field.GetDescriptionValue(),
            Theme = field.GetThemeColorValue().Theme,
            Color = field.GetThemeColorValue().Color
        }).ToList();

        // 加入缓存
        EnumInfosCatch.TryAdd(enumType, enumInfos);
        return enumInfos;
    }

    /// <summary>
    /// 获取枚举信息列表
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>枚举信息列表</returns>
    public static IEnumerable<EnumInfo> GetEnumInfos<TEnum>() where TEnum : struct, Enum
    {
        return GetEnumInfos(typeof(TEnum));
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

    #region 枚举类型查找

    /// <summary>
    /// 从程序集中查找所有枚举类型
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <returns>枚举类型集合</returns>
    public static IEnumerable<Type> GetEnumTypes(Assembly assembly)
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
    public static Type? GetEnumTypeByName(Assembly assembly, string typeName)
    {
        var enumTypes = GetEnumTypes(assembly);
        return enumTypes.FirstOrDefault(o => o.Name == typeName);
    }

    /// <summary>
    /// 从当前程序集中查找所有枚举类型
    /// </summary>
    /// <returns>枚举类型集合</returns>
    public static IEnumerable<Type> GetEnumTypes()
    {
        return GetEnumTypes(Assembly.GetCallingAssembly());
    }

    #endregion 枚举类型查找

    #region 枚举随机选择

    /// <summary>
    /// 随机获取一个枚举值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>随机枚举值</returns>
    public static TEnum GetRandomEnum<TEnum>() where TEnum : struct, Enum
    {
        var values = GetValues<TEnum>();
        var random = new Random();
        return values[random.Next(values.Length)];
    }

    /// <summary>
    /// 随机获取多个枚举值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="count">数量</param>
    /// <param name="allowDuplicates">是否允许重复</param>
    /// <returns>随机枚举值集合</returns>
    public static IEnumerable<TEnum> GetRandomEnums<TEnum>(int count, bool allowDuplicates = false) where TEnum : struct, Enum
    {
        var values = GetValues<TEnum>();
        var random = new Random();
        var result = new List<TEnum>();

        if (allowDuplicates)
        {
            for (var i = 0; i < count; i++)
            {
                result.Add(values[random.Next(values.Length)]);
            }
        }
        else
        {
            var availableValues = values.ToList();
            for (var i = 0; i < count && availableValues.Count > 0; i++)
            {
                var index = random.Next(availableValues.Count);
                result.Add(availableValues[index]);
                availableValues.RemoveAt(index);
            }
        }

        return result;
    }

    #endregion 枚举随机选择

    #region 枚举统计

    /// <summary>
    /// 获取枚举的数量
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>枚举数量</returns>
    public static int GetCount<TEnum>() where TEnum : struct, Enum
    {
        return GetValues<TEnum>().Length;
    }

    /// <summary>
    /// 获取枚举的最小值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>最小值</returns>
    public static TEnum GetMinValue<TEnum>() where TEnum : struct, Enum
    {
        var values = GetValues<TEnum>();
        return values.Min();
    }

    /// <summary>
    /// 获取枚举的最大值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>最大值</returns>
    public static TEnum GetMaxValue<TEnum>() where TEnum : struct, Enum
    {
        var values = GetValues<TEnum>();
        return values.Max();
    }

    #endregion 枚举统计

    #region 枚举基础操作

    /// <summary>
    /// 获取枚举的所有值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>枚举值数组</returns>
    public static TEnum[] GetValues<TEnum>() where TEnum : struct, Enum
    {
        return Enum.GetValues<TEnum>();
    }

    /// <summary>
    /// 获取枚举的所有名称
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>枚举名称数组</returns>
    public static string[] GetNames<TEnum>() where TEnum : struct, Enum
    {
        return Enum.GetNames<TEnum>();
    }

    #endregion 枚举基础操作
}

/// <summary>
/// 泛型枚举辅助类
/// </summary>
/// <typeparam name="TEnum">枚举类型</typeparam>
public static class EnumHelper<TEnum> where TEnum : struct, Enum
{
    private static readonly Lazy<TEnum[]> Values = new(() => Enum.GetValues<TEnum>());
    private static readonly Lazy<string[]> Names = new(() => Enum.GetNames<TEnum>());
    private static readonly Lazy<Dictionary<string, int>> NameValueDict = new(() => EnumHelper.GetNameValueDict<TEnum>());
    private static readonly Lazy<Dictionary<int, string>> ValueDescriptionDict = new(() => EnumHelper.GetValueDescriptionDict<TEnum>());
    private static readonly Lazy<IEnumerable<EnumInfo>> EnumInfos = new(() => EnumHelper.GetEnumInfos<TEnum>());

    /// <summary>
    /// 获取枚举的数量
    /// </summary>
    /// <returns>枚举数量</returns>
    public static int Count => GetValues().Length;

    /// <summary>
    /// 获取枚举的最小值
    /// </summary>
    /// <returns>最小值</returns>
    public static TEnum MinValue => GetValues().Min();

    /// <summary>
    /// 获取枚举的最大值
    /// </summary>
    /// <returns>最大值</returns>
    public static TEnum MaxValue => GetValues().Max();

    /// <summary>
    /// 获取枚举的所有值
    /// </summary>
    /// <returns>枚举值数组</returns>
    public static TEnum[] GetValues() => Values.Value;

    /// <summary>
    /// 获取枚举的所有名称
    /// </summary>
    /// <returns>枚举名称数组</returns>
    public static string[] GetNames() => Names.Value;

    /// <summary>
    /// 获取枚举名称和值的字典
    /// </summary>
    /// <returns>名称值字典</returns>
    public static Dictionary<string, int> GetNameValueDict() => NameValueDict.Value;

    /// <summary>
    /// 获取枚举值和描述的字典
    /// </summary>
    /// <returns>值描述字典</returns>
    public static Dictionary<int, string> GetValueDescriptionDict() => ValueDescriptionDict.Value;

    /// <summary>
    /// 获取枚举信息列表
    /// </summary>
    /// <returns>枚举信息列表</returns>
    public static IEnumerable<EnumInfo> GetEnumInfos() => EnumInfos.Value;

    /// <summary>
    /// 检查指定的值是否定义在枚举中
    /// </summary>
    /// <param name="value">要检查的值</param>
    /// <returns>如果值在枚举中定义，则为true；否则为false</returns>
    public static bool IsDefined(int value) => EnumHelper.IsDefined<TEnum>(value);

    /// <summary>
    /// 检查指定的名称是否定义在枚举中
    /// </summary>
    /// <param name="name">要检查的名称</param>
    /// <param name="ignoreCase">是否忽略大小写</param>
    /// <returns>如果名称在枚举中定义，则为true；否则为false</returns>
    public static bool IsDefined(string name, bool ignoreCase = false) => EnumHelper.IsDefined<TEnum>(name, ignoreCase);

    /// <summary>
    /// 随机获取一个枚举值
    /// </summary>
    /// <returns>随机枚举值</returns>
    public static TEnum GetRandomValue() => EnumHelper.GetRandomEnum<TEnum>();
}
