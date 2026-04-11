#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumLocalizationService
// Guid:cdb653ca-0ec7-4391-af68-c49e85ba1114
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/11 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using XiHan.Framework.Localization.Abstractions.Enums;
using XiHan.Framework.Localization.Options;
using XiHan.Framework.Utils.Core;
using XiHan.Framework.Utils.Enums;

namespace XiHan.Framework.Localization.Services;

/// <summary>
/// 默认枚举本地化服务
/// </summary>
public sealed class EnumLocalizationService : IEnumLocalizationService
{
    private readonly object _syncLock = new();
    private readonly JsonLocalizationResourceStore _resourceStore;
    private readonly IOptionsMonitor<XiHanLocalizationOptions> _optionsMonitor;
    private readonly ConcurrentDictionary<string, Type> _resolvedTypeCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Type> _enumFullNameMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Type> _enumShortNameMap = new(StringComparer.OrdinalIgnoreCase);
    private bool _typeCacheInitialized;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="resourceStore">本地化资源存储</param>
    /// <param name="optionsMonitor">本地化选项监控器</param>
    public EnumLocalizationService(
        JsonLocalizationResourceStore resourceStore,
        IOptionsMonitor<XiHanLocalizationOptions> optionsMonitor)
    {
        _resourceStore = resourceStore;
        _optionsMonitor = optionsMonitor;
    }

    /// <inheritdoc />
    public LocalizedEnumDefinition Get(Type enumType, EnumLocalizationQuery? query = null)
    {
        ArgumentNullException.ThrowIfNull(enumType);

        if (!enumType.IsEnum)
        {
            throw new ArgumentException($"类型 {enumType.Name} 不是枚举类型。", nameof(enumType));
        }

        var normalizedQuery = NormalizeQuery(query);
        return BuildDefinition(enumType, normalizedQuery);
    }

    /// <inheritdoc />
    public LocalizedEnumDefinition Get(string enumTypeName, EnumLocalizationQuery? query = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(enumTypeName);
        var enumType = ResolveEnumType(enumTypeName);
        return Get(enumType, query);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, LocalizedEnumDefinition> GetMany(
        IEnumerable<string> enumTypeNames,
        EnumLocalizationQuery? query = null)
    {
        ArgumentNullException.ThrowIfNull(enumTypeNames);

        var normalizedQuery = NormalizeQuery(query);
        var result = new Dictionary<string, LocalizedEnumDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var enumTypeName in enumTypeNames
                     .Where(static x => !string.IsNullOrWhiteSpace(x))
                     .Select(static x => x.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            result[enumTypeName] = Get(enumTypeName, normalizedQuery);
        }

        return result;
    }

    /// <inheritdoc />
    public bool TryGet(
        string enumTypeName,
        out LocalizedEnumDefinition? result,
        EnumLocalizationQuery? query = null)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(enumTypeName))
        {
            return false;
        }

        try
        {
            result = Get(enumTypeName, query);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static EnumLocalizationQuery NormalizeQuery(EnumLocalizationQuery? query)
    {
        return query ?? new EnumLocalizationQuery();
    }

    private static CultureInfo ResolveCulture(string? cultureName, XiHanLocalizationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(cultureName))
        {
            try
            {
                return CultureInfo.GetCultureInfo(cultureName);
            }
            catch (CultureNotFoundException)
            {
                // 忽略并继续回退
            }
        }

        if (!string.IsNullOrWhiteSpace(CultureInfo.CurrentUICulture.Name))
        {
            return CultureInfo.CurrentUICulture;
        }

        return CultureInfo.GetCultureInfo(options.DefaultCulture);
    }

    private static string ResolveResourceName(
        EnumItem? enumItem,
        EnumInfo enumInfo,
        XiHanLocalizationOptions options)
    {
        var resourceName = enumItem?.ResourceName
                           ?? enumInfo.LocalizationResourceName
                           ?? options.EnumResourceName;

        if (string.IsNullOrWhiteSpace(resourceName))
        {
            resourceName = options.DefaultResourceName;
        }

        return resourceName;
    }

    private static IReadOnlyList<string> BuildLocalizationKeyCandidates(
        Type enumType,
        EnumItem enumItem,
        EnumInfo enumInfo,
        XiHanLocalizationOptions options)
    {
        var result = new List<string>(capacity: 6);
        var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (distinct.Add(key))
            {
                result.Add(key);
            }
        }

        Add(enumItem.LocalizationKey);

        var keyPrefix = enumInfo.LocalizationKeyPrefix;
        if (string.IsNullOrWhiteSpace(keyPrefix))
        {
            keyPrefix = options.EnumLocalizationKeyPrefix;
        }

        if (!string.IsNullOrWhiteSpace(keyPrefix))
        {
            Add($"{keyPrefix}.{enumType.Name}.{enumItem.Key}");
        }

        Add($"{enumType.Name}.{enumItem.Key}");
        Add($"{enumType.Name}_{enumItem.Key}");
        Add(enumItem.Key);

        return result;
    }

    private LocalizedEnumDefinition BuildDefinition(Type enumType, EnumLocalizationQuery query)
    {
        var options = _optionsMonitor.CurrentValue;
        var culture = ResolveCulture(query.CultureName, options);
        var enumInfo = EnumHelper.GetEnumInfo(enumType);
        var enumItems = EnumHelper.GetEnumItems(enumType, query.IncludeHidden, query.Ordered);

        var definition = new LocalizedEnumDefinition
        {
            EnumName = enumType.Name,
            FullName = enumType.FullName ?? enumType.Name,
            DisplayName = enumInfo.Description,
            CultureName = culture.Name,
            IsFlags = enumInfo.IsFlags,
            UnderlyingTypeName = enumInfo.UnderlyingType.FullName ?? enumInfo.UnderlyingType.Name,
            ResourceName = ResolveResourceName(null, enumInfo, options)
        };

        var localizedItems = new List<LocalizedEnumItem>(enumItems.Count);
        foreach (var enumItem in enumItems)
        {
            var resourceName = ResolveResourceName(enumItem, enumInfo, options);
            var keys = BuildLocalizationKeyCandidates(enumType, enumItem, enumInfo, options);
            var (label, resolvedKey) = ResolveLocalizedLabel(
                resourceName,
                culture,
                keys,
                enumItem.Description);

            localizedItems.Add(new LocalizedEnumItem
            {
                Name = enumItem.Key,
                Value = enumItem.Value,
                ValueText = enumItem.Value.ToString() ?? string.Empty,
                Label = label,
                Description = enumItem.Description,
                Theme = enumItem.Theme,
                Icon = enumItem.Icon,
                Order = enumItem.Order,
                Hidden = enumItem.Hidden,
                Disabled = enumItem.Disabled,
                ResourceName = resourceName,
                LocalizationKey = resolvedKey,
                Extra = enumItem.Extra
            });
        }

        definition.Items = localizedItems;
        return definition;
    }

    private (string Label, string? LocalizationKey) ResolveLocalizedLabel(
        string resourceName,
        CultureInfo culture,
        IReadOnlyList<string> localizationKeys,
        string fallback)
    {
        foreach (var key in localizationKeys)
        {
            if (_resourceStore.TryGetString(resourceName, culture, key, out var value)
                && !string.IsNullOrWhiteSpace(value))
            {
                return (value, key);
            }
        }

        return (fallback, localizationKeys.FirstOrDefault());
    }

    private Type ResolveEnumType(string enumTypeName)
    {
        var normalizedName = enumTypeName.Trim();
        return _resolvedTypeCache.GetOrAdd(normalizedName, static (name, instance) =>
        {
            instance.EnsureTypeCacheInitialized();

            if (instance._enumFullNameMap.TryGetValue(name, out var enumTypeFromFullName))
            {
                return enumTypeFromFullName;
            }

            if (instance._enumShortNameMap.TryGetValue(name, out var enumTypeFromShortName))
            {
                return enumTypeFromShortName;
            }

            throw new KeyNotFoundException($"未找到枚举类型：{name}");
        }, this);
    }

    private void EnsureTypeCacheInitialized()
    {
        if (_typeCacheInitialized)
        {
            return;
        }

        lock (_syncLock)
        {
            if (_typeCacheInitialized)
            {
                return;
            }

            BuildTypeCache();
            _typeCacheInitialized = true;
        }
    }

    private void BuildTypeCache()
    {
        _enumFullNameMap.Clear();
        _enumShortNameMap.Clear();
        _resolvedTypeCache.Clear();

        var duplicatedShortNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(static x => x != null).Cast<Type>().ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var type in types.Where(static t => t.IsEnum))
            {
                if (!string.IsNullOrWhiteSpace(type.FullName))
                {
                    _enumFullNameMap[type.FullName] = type;
                }

                if (duplicatedShortNames.Contains(type.Name))
                {
                    continue;
                }

                if (_enumShortNameMap.TryGetValue(type.Name, out var existingType)
                    && existingType != type)
                {
                    _enumShortNameMap.Remove(type.Name);
                    duplicatedShortNames.Add(type.Name);
                    continue;
                }

                _enumShortNameMap[type.Name] = type;
            }
        }
    }
}
