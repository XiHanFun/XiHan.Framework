// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;
using XiHan.Framework.Utils.Enums;

namespace XiHan.Framework.Utils.Core;

/// <summary>
/// 枚举帮助类
/// 提供枚举反射、元数据解析、缓存和常用转换能力
/// </summary>
public static class EnumHelper
{
    #region 缓存

    private readonly record struct EnumItemsCacheKey(Type EnumType, bool IncludeHidden, bool Ordered);

    /// <summary>
    /// 枚举信息缓存
    /// </summary>
    private static readonly ConcurrentDictionary<Type, EnumInfo> EnumInfoCache = new();

    /// <summary>
    /// 枚举项缓存
    /// </summary>
    private static readonly ConcurrentDictionary<EnumItemsCacheKey, IReadOnlyList<EnumItem>> EnumItemsCache = new();

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

        var cacheKey = new EnumItemsCacheKey(enumType, includeHidden, ordered);
        var cachedItems = EnumItemsCache.GetOrAdd(cacheKey, static key =>
        {
            var items = BuildEnumItems(key.EnumType, key.IncludeHidden, key.Ordered);
            return items;
        });

        // 返回拷贝，避免调用方修改缓存对象
        return [.. cachedItems];
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
            Icon = item.Icon,
            Order = item.Order,
            Hidden = item.Hidden,
            Disabled = item.Disabled,
            LocalizationKey = item.LocalizationKey,
            ResourceName = item.ResourceName,
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
            var localizationResource = type.GetCustomAttribute<EnumLocalizationResourceAttribute>();

            return new EnumInfo
            {
                Type = type,
                UnderlyingType = underlyingType,
                Description = description,
                IsFlags = isFlagsEnum,
                Values = [.. values.Cast<object>()],
                Names = names,
                LocalizationResourceName = localizationResource?.ResourceName,
                LocalizationKeyPrefix = localizationResource?.KeyPrefix
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

        if (value is TEnum enumValue)
        {
            return enumValue;
        }

        if (value is string stringValue)
        {
            return Parse<TEnum>(stringValue);
        }

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

    #region 描述与本地化元数据

    /// <summary>
    /// 获取枚举值的描述
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <returns>描述信息</returns>
    public static string GetDescription<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        return GetDescription(typeof(TEnum), value);
    }

    /// <summary>
    /// 获取枚举值的描述
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <param name="value">枚举值</param>
    /// <returns>描述信息</returns>
    public static string GetDescription(Type enumType, object value)
    {
        ArgumentNullException.ThrowIfNull(enumType);
        ArgumentNullException.ThrowIfNull(value);

        var item = GetEnumItem(enumType, value);
        return item?.Description ?? value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 获取枚举值的主题
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <returns>主题名称</returns>
    public static string? GetTheme<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        return GetTheme(typeof(TEnum), value);
    }

    /// <summary>
    /// 获取枚举值的主题
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <param name="value">枚举值</param>
    /// <returns>主题名称</returns>
    public static string? GetTheme(Type enumType, object value)
    {
        ArgumentNullException.ThrowIfNull(enumType);
        ArgumentNullException.ThrowIfNull(value);

        var item = GetEnumItem(enumType, value);
        return item?.Theme;
    }

    /// <summary>
    /// 获取枚举值的本地化键
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <returns>本地化键</returns>
    public static string? GetLocalizationKey<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        return GetLocalizationKey(typeof(TEnum), value);
    }

    /// <summary>
    /// 获取枚举值的本地化键
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <param name="value">枚举值</param>
    /// <returns>本地化键</returns>
    public static string? GetLocalizationKey(Type enumType, object value)
    {
        ArgumentNullException.ThrowIfNull(enumType);
        ArgumentNullException.ThrowIfNull(value);

        var item = GetEnumItem(enumType, value);
        return item?.LocalizationKey;
    }

    /// <summary>
    /// 获取枚举值的本地化资源名
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <returns>本地化资源名</returns>
    public static string? GetLocalizationResourceName<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        return GetLocalizationResourceName(typeof(TEnum), value);
    }

    /// <summary>
    /// 获取枚举值的本地化资源名
    /// </summary>
    /// <param name="enumType">枚举类型</param>
    /// <param name="value">枚举值</param>
    /// <returns>本地化资源名</returns>
    public static string? GetLocalizationResourceName(Type enumType, object value)
    {
        ArgumentNullException.ThrowIfNull(enumType);
        ArgumentNullException.ThrowIfNull(value);

        var item = GetEnumItem(enumType, value);
        return item?.ResourceName;
    }

    #endregion

    #region 私有方法

    private static IReadOnlyList<EnumItem> BuildEnumItems(Type enumType, bool includeHidden, bool ordered)
    {
        var items = new List<EnumItem>();
        var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
        var typeLocalization = enumType.GetCustomAttribute<EnumLocalizationResourceAttribute>();

        foreach (var field in fields)
        {
            var item = CreateEnumItem(field, typeLocalization);
            if (!includeHidden && item.Hidden)
            {
                continue;
            }

            items.Add(item);
        }

        if (ordered)
        {
            items = [.. items.OrderBy(x => x.Order).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)];
        }

        return items;
    }

    /// <summary>
    /// 创建枚举项
    /// </summary>
    /// <param name="field">字段信息</param>
    /// <param name="typeLocalization">类型级本地化配置</param>
    /// <returns>枚举项</returns>
    private static EnumItem CreateEnumItem(FieldInfo field, EnumLocalizationResourceAttribute? typeLocalization)
    {
        var value = field.GetValue(null)!;
        var name = field.Name;
        var enumType = field.DeclaringType ?? throw new InvalidOperationException("枚举字段缺少声明类型。");

        var displayAttr = field.GetCustomAttribute<EnumDisplayAttribute>();
        var descriptionAttr = field.GetCustomAttribute<DescriptionAttribute>();
        var enumMemberAttr = field.GetCustomAttribute<EnumMemberAttribute>();
        var orderAttr = field.GetCustomAttribute<EnumOrderAttribute>();
        var hiddenAttr = field.GetCustomAttribute<EnumHiddenAttribute>();
        var disabledAttr = field.GetCustomAttribute<EnumDisabledAttribute>();
        var iconAttr = field.GetCustomAttribute<EnumIconAttribute>();
        var themeAttr = field.GetCustomAttribute<EnumThemeAttribute>();
        var fieldLocalization = field.GetCustomAttribute<EnumLocalizationResourceAttribute>();
        var localizationKeyAttr = field.GetCustomAttribute<EnumLocalizationKeyAttribute>();

        var description = displayAttr?.Description
                          ?? descriptionAttr?.Description
                          ?? enumMemberAttr?.Value
                          ?? name;

        var theme = displayAttr?.Theme ?? themeAttr?.Theme;
        var order = orderAttr?.Order ?? displayAttr?.Order ?? int.MaxValue;
        var hidden = hiddenAttr != null || (displayAttr?.Hidden ?? false);
        var disabled = disabledAttr?.Disabled ?? displayAttr?.Disabled ?? false;
        var icon = displayAttr?.Icon ?? iconAttr?.Icon;

        var localizationKey = localizationKeyAttr?.LocalizationKey ?? displayAttr?.LocalizationKey;
        var localizationResource = fieldLocalization?.ResourceName
                                   ?? displayAttr?.ResourceName
                                   ?? typeLocalization?.ResourceName;

        if (string.IsNullOrWhiteSpace(localizationKey))
        {
            var keyPrefix = fieldLocalization?.KeyPrefix ?? typeLocalization?.KeyPrefix;
            localizationKey = string.IsNullOrWhiteSpace(keyPrefix)
                ? $"{enumType.Name}.{name}"
                : $"{keyPrefix}.{enumType.Name}.{name}";
        }

        var extra = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Order"] = order
        };

        if (!string.IsNullOrWhiteSpace(icon))
        {
            extra["Icon"] = icon;
        }

        if (disabled)
        {
            extra["Disabled"] = true;
        }

        if (displayAttr?.Extra != null)
        {
            foreach (var pair in displayAttr.Extra)
            {
                extra[pair.Key] = pair.Value;
            }
        }

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
            Icon = icon,
            Order = order,
            Hidden = hidden,
            Disabled = disabled,
            LocalizationKey = localizationKey,
            ResourceName = localizationResource,
            Extra = extra.Count > 0 ? extra : null
        };
    }

    private static EnumItem? GetEnumItem(Type enumType, object value)
    {
        if (!enumType.IsEnum)
        {
            throw new ArgumentException($"类型 {enumType.Name} 不是枚举类型", nameof(enumType));
        }

        var enumName = value is Enum enumValue
            ? enumValue.ToString()
            : Enum.GetName(enumType, value) ?? value.ToString();

        if (string.IsNullOrWhiteSpace(enumName))
        {
            return null;
        }

        return GetEnumItems(enumType, includeHidden: true, ordered: false)
            .FirstOrDefault(x => string.Equals(x.Key, enumName, StringComparison.Ordinal));
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
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
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
        ArgumentNullException.ThrowIfNull(enumType);

        EnumInfoCache.TryRemove(enumType, out _);
        NameValueCache.TryRemove(enumType, out _);
        ValueNameCache.TryRemove(enumType, out _);

        foreach (var key in EnumItemsCache.Keys.Where(x => x.EnumType == enumType).ToList())
        {
            EnumItemsCache.TryRemove(key, out _);
        }
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
