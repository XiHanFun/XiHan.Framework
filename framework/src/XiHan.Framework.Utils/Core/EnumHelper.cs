#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumHelper
// Guid:9e8d7c6b-5a4f-3e2d-1c0b-9a8b7c6d5e4f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/19 23:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;
using XiHan.Framework.Utils.Enums;

namespace XiHan.Framework.Utils.Core;

/// <summary>
/// 枚举帮助类
/// </summary>
public static class EnumHelper
{
    #region 缓存

    /// <summary>
    /// 枚举信息缓存
    /// </summary>
    private static readonly ConcurrentDictionary<Type, EnumInfo> EnumInfoCache = new();

    /// <summary>
    /// 枚举项缓存
    /// </summary>
    private static readonly ConcurrentDictionary<Type, List<EnumItem>> EnumItemsCache = new();

    /// <summary>
    /// 枚举名称-值缓存
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Dictionary<string, object>> NameValueCache = new();

    /// <summary>
    /// 枚举值-名称缓存
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Dictionary<object, string>> ValueNameCache = new();

    #endregion

    #region 获取枚举信息

    /// <summary>
    /// 获取枚举项列表
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="includeHidden">是否包含隐藏项</param>
    /// <param name="ordered">是否排序</param>
    /// <returns>枚举项列表</returns>
    public static List<EnumItem> GetEnumItems<TEnum>(bool includeHidden = false, bool ordered = true)
        where TEnum : struct, Enum
    {
        return GetEnumItems(typeof(TEnum), includeHidden, ordered);
    }

    /// <summary>
    /// 获取枚举项列表
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <param name="includeHidden">是否包含隐藏项</param>
    /// <param name="ordered">是否排序</param>
    /// <returns>枚举项列表</returns>
    public static List<EnumItem> GetEnumItems(Type enumType, bool includeHidden = false, bool ordered = true)
    {
        ArgumentNullException.ThrowIfNull(enumType);

        if (!enumType.IsEnum)
        {
            throw new ArgumentException($"类型 {enumType.Name} 不是枚举类型", nameof(enumType));
        }

        var cacheKey = $"{enumType.FullName}_{includeHidden}_{ordered}";

        return EnumItemsCache.GetOrAdd(enumType, _ =>
        {
            var items = new List<EnumItem>();
            var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);

            foreach (var field in fields)
            {
                if (!includeHidden && field.GetCustomAttribute<EnumHiddenAttribute>() != null)
                {
                    continue;
                }

                var item = CreateEnumItem(field);
                items.Add(item);
            }

            if (ordered)
            {
                items = [.. items.OrderBy(x => x.Extra?.GetValueOrDefault("Order", int.MaxValue) ?? int.MaxValue).ThenBy(x => x.Key)];
            }

            return items;
        });
    }

    /// <summary>
    /// 获取强类型枚举项列表
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="includeHidden">是否包含隐藏项</param>
    /// <param name="ordered">是否排序</param>
    /// <returns>强类型枚举项列表</returns>
    public static List<EnumItem<TEnum>> GetTypedEnumItems<TEnum>(bool includeHidden = false, bool ordered = true)
        where TEnum : struct, Enum
    {
        var items = GetEnumItems<TEnum>(includeHidden, ordered);
        return [.. items.Select(item => new EnumItem<TEnum>
        {
            Key = item.Key,
            Value = (TEnum)item.Value,
            Description = item.Description,
            Theme = item.Theme,
            Extra = item.Extra
        })];
    }

    /// <summary>
    /// 获取枚举信息
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>枚举信息</returns>
    public static EnumInfo GetEnumInfo<TEnum>() where TEnum : struct, Enum
    {
        return GetEnumInfo(typeof(TEnum));
    }

    /// <summary>
    /// 获取枚举信息
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <returns>枚举信息</returns>
    public static EnumInfo GetEnumInfo(Type enumType)
    {
        ArgumentNullException.ThrowIfNull(enumType);

        if (!enumType.IsEnum)
        {
            throw new ArgumentException($"类型 {enumType.Name} 不是枚举类型", nameof(enumType));
        }

        return EnumInfoCache.GetOrAdd(enumType, type =>
        {
            var underlyingType = Enum.GetUnderlyingType(type);
            var values = Enum.GetValues(type);
            var names = Enum.GetNames(type);

            var description = type.GetCustomAttribute<DescriptionAttribute>()?.Description ?? type.Name;
            var isFlagsEnum = type.GetCustomAttribute<FlagsAttribute>() != null;

            return new EnumInfo
            {
                Type = type,
                UnderlyingType = underlyingType,
                Description = description,
                IsFlags = isFlagsEnum,
                Values = [.. values.Cast<object>()],
                Names = names
            };
        });
    }

    #endregion

    #region 枚举转换

    /// <summary>
    /// 将字符串转换为枚举值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">字符串值</param>
    /// <param name="ignoreCase">是否忽略大小写</param>
    /// <returns>枚举值</returns>
    public static TEnum Parse<TEnum>(string value, bool ignoreCase = true) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("值不能为空", nameof(value));
        }

        return Enum.Parse<TEnum>(value, ignoreCase);
    }

    /// <summary>
    /// 尝试将字符串转换为枚举值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">字符串值</param>
    /// <param name="result">转换结果</param>
    /// <param name="ignoreCase">是否忽略大小写</param>
    /// <returns>是否转换成功</returns>
    public static bool TryParse<TEnum>(string? value, out TEnum result, bool ignoreCase = true)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = default;
            return false;
        }

        return Enum.TryParse(value, ignoreCase, out result);
    }

    /// <summary>
    /// 将数值转换为枚举值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">数值</param>
    /// <returns>枚举值</returns>
    public static TEnum ToEnum<TEnum>(object value) where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(value);

        var enumType = typeof(TEnum);
        var underlyingType = Enum.GetUnderlyingType(enumType);

        // 如果值已经是目标枚举类型
        if (value is TEnum enumValue)
        {
            return enumValue;
        }

        // 如果值是字符串，尝试按名称解析
        if (value is string stringValue)
        {
            return Parse<TEnum>(stringValue);
        }

        // 将值转换为底层类型
        var convertedValue = Convert.ChangeType(value, underlyingType);
        return (TEnum)Enum.ToObject(enumType, convertedValue);
    }

    /// <summary>
    /// 尝试将对象转换为枚举值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">要转换的值</param>
    /// <param name="result">转换结果</param>
    /// <returns>是否转换成功</returns>
    public static bool TryToEnum<TEnum>(object? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;

        if (value == null)
        {
            return false;
        }

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

    #endregion

    #region 枚举验证

    /// <summary>
    /// 判断是否为有效的枚举值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <returns>是否有效</returns>
    public static bool IsValidEnum<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        return Enum.IsDefined(value);
    }

    /// <summary>
    /// 判断是否为有效的枚举值
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <param name="value">值</param>
    /// <returns>是否有效</returns>
    public static bool IsValidEnum(Type enumType, object value)
    {
        ArgumentNullException.ThrowIfNull(enumType);
        ArgumentNullException.ThrowIfNull(value);

        if (!enumType.IsEnum)
        {
            throw new ArgumentException($"类型 {enumType.Name} 不是枚举类型", nameof(enumType));
        }

        return Enum.IsDefined(enumType, value);
    }

    /// <summary>
    /// 判断字符串是否为有效的枚举名称
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="name">枚举名称</param>
    /// <param name="ignoreCase">是否忽略大小写</param>
    /// <returns>是否有效</returns>
    public static bool IsValidEnumName<TEnum>(string name, bool ignoreCase = true) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var names = GetNameValueCache<TEnum>();
        return ignoreCase
            ? names.Keys.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
            : names.ContainsKey(name);
    }

    #endregion

    #region 名称和值的映射

    /// <summary>
    /// 根据枚举名称获取枚举值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="name">枚举名称</param>
    /// <returns>枚举值</returns>
    public static TEnum GetValueByName<TEnum>(string name) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("枚举名称不能为空", nameof(name));
        }

        var nameValueMap = GetNameValueCache<TEnum>();

        if (nameValueMap.TryGetValue(name, out var value))
        {
            return (TEnum)value;
        }

        // 尝试忽略大小写匹配
        var kvp = nameValueMap.FirstOrDefault(x =>
            string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase));

        if (kvp.Key != null)
        {
            return (TEnum)kvp.Value;
        }

        throw new ArgumentException($"找不到名称为 '{name}' 的枚举值", nameof(name));
    }

    /// <summary>
    /// 根据枚举值获取枚举名称
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <returns>枚举名称</returns>
    public static string GetNameByValue<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var valueNameMap = GetValueNameCache<TEnum>();

        if (valueNameMap.TryGetValue(value, out var name))
        {
            return name;
        }

        return value.ToString();
    }

    #endregion

    #region 描述相关

    /// <summary>
    /// 获取枚举值的描述
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <returns>描述信息</returns>
    public static string GetDescription<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var field = typeof(TEnum).GetField(value.ToString());
        if (field == null)
        {
            return value.ToString();
        }

        // 优先使用 EnumDisplayAttribute
        var displayAttr = field.GetCustomAttribute<EnumDisplayAttribute>();
        if (displayAttr != null)
        {
            return displayAttr.Description;
        }

        // 其次使用 DescriptionAttribute
        var descAttr = field.GetCustomAttribute<DescriptionAttribute>();
        if (descAttr != null)
        {
            return descAttr.Description;
        }

        // 最后使用 EnumMemberAttribute
        var memberAttr = field.GetCustomAttribute<EnumMemberAttribute>();
        if (memberAttr != null && !string.IsNullOrEmpty(memberAttr.Value))
        {
            return memberAttr.Value;
        }

        return value.ToString();
    }

    /// <summary>
    /// 获取枚举值的主题
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <returns>主题名称</returns>
    public static string? GetTheme<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var field = typeof(TEnum).GetField(value.ToString());
        if (field == null)
        {
            return null;
        }

        // 优先使用 EnumDisplayAttribute
        var displayAttr = field.GetCustomAttribute<EnumDisplayAttribute>();
        if (displayAttr?.Theme != null)
        {
            return displayAttr.Theme;
        }

        // 其次使用 EnumThemeAttribute
        var themeAttr = field.GetCustomAttribute<EnumThemeAttribute>();
        return themeAttr?.Theme;
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 创建枚举项
    /// </summary>
    /// <param name="field">字段信息</param>
    /// <returns>枚举项</returns>
    private static EnumItem CreateEnumItem(FieldInfo field)
    {
        var value = field.GetValue(null)!;
        var name = field.Name;

        // 获取描述
        var description = name;
        var displayAttr = field.GetCustomAttribute<EnumDisplayAttribute>();
        if (displayAttr != null)
        {
            description = displayAttr.Description;
        }
        else
        {
            var descAttr = field.GetCustomAttribute<DescriptionAttribute>();
            if (descAttr != null)
            {
                description = descAttr.Description;
            }
            else
            {
                var memberAttr = field.GetCustomAttribute<EnumMemberAttribute>();
                if (memberAttr?.Value != null)
                {
                    description = memberAttr.Value;
                }
            }
        }

        // 获取主题
        string? theme;
        if (displayAttr?.Theme != null)
        {
            theme = displayAttr.Theme;
        }
        else
        {
            var themeAttr = field.GetCustomAttribute<EnumThemeAttribute>();
            theme = themeAttr?.Theme;
        }

        // 获取扩展属性
        var extra = new Dictionary<string, object>();

        // 排序
        var orderAttr = field.GetCustomAttribute<EnumOrderAttribute>();
        if (orderAttr != null)
        {
            extra["Order"] = orderAttr.Order;
        }
        else if (displayAttr != null)
        {
            extra["Order"] = displayAttr.Order;
        }

        // 扩展属性
        var extraAttrs = field.GetCustomAttributes<EnumExtraAttribute>();
        foreach (var extraAttr in extraAttrs)
        {
            extra[extraAttr.Key] = extraAttr.Value;
        }

        return new EnumItem
        {
            Key = name,
            Value = value,
            Description = description,
            Theme = theme,
            Extra = extra.Count > 0 ? extra : null
        };
    }

    /// <summary>
    /// 获取名称-值缓存
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>名称-值字典</returns>
    private static Dictionary<string, object> GetNameValueCache<TEnum>() where TEnum : struct, Enum
    {
        var enumType = typeof(TEnum);
        return NameValueCache.GetOrAdd(enumType, type =>
        {
            var result = new Dictionary<string, object>();
            var names = Enum.GetNames(type);
            var values = Enum.GetValues(type);

            for (var i = 0; i < names.Length; i++)
            {
                result[names[i]] = values.GetValue(i)!;
            }

            return result;
        });
    }

    /// <summary>
    /// 获取值-名称缓存
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>值-名称字典</returns>
    private static Dictionary<object, string> GetValueNameCache<TEnum>() where TEnum : struct, Enum
    {
        var enumType = typeof(TEnum);
        return ValueNameCache.GetOrAdd(enumType, type =>
        {
            var result = new Dictionary<object, string>();
            var names = Enum.GetNames(type);
            var values = Enum.GetValues(type);

            for (var i = 0; i < names.Length; i++)
            {
                result[values.GetValue(i)!] = names[i];
            }

            return result;
        });
    }

    #endregion

    #region 缓存管理

    /// <summary>
    /// 清除指定类型的缓存
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    public static void ClearCache<TEnum>() where TEnum : struct, Enum
    {
        var enumType = typeof(TEnum);
        ClearCache(enumType);
    }

    /// <summary>
    /// 清除指定类型的缓存
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    public static void ClearCache(Type enumType)
    {
        EnumInfoCache.TryRemove(enumType, out _);
        EnumItemsCache.TryRemove(enumType, out _);
        NameValueCache.TryRemove(enumType, out _);
        ValueNameCache.TryRemove(enumType, out _);
    }

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    public static void ClearAllCaches()
    {
        EnumInfoCache.Clear();
        EnumItemsCache.Clear();
        NameValueCache.Clear();
        ValueNameCache.Clear();
    }

    #endregion
}