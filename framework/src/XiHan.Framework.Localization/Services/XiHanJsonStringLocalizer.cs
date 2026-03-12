#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanJsonStringLocalizer
// Guid:af293049-3db5-4f96-a0fa-c36c7244e9bc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/13 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Localization;
using System.Globalization;

namespace XiHan.Framework.Localization.Services;

/// <summary>
/// JSON 资源本地化器（带 ResourceManager 回退）
/// </summary>
public sealed class XiHanJsonStringLocalizer : IStringLocalizer
{
    private readonly string _resourceName;
    private readonly JsonLocalizationResourceStore _resourceStore;
    private readonly IStringLocalizer _fallbackLocalizer;
    private readonly CultureInfo? _fixedCulture;

    /// <summary>
    /// 初始化本地化器
    /// </summary>
    /// <param name="resourceName"></param>
    /// <param name="resourceStore"></param>
    /// <param name="fallbackLocalizer"></param>
    /// <param name="fixedCulture"></param>
    public XiHanJsonStringLocalizer(
        string resourceName,
        JsonLocalizationResourceStore resourceStore,
        IStringLocalizer fallbackLocalizer,
        CultureInfo? fixedCulture = null)
    {
        _resourceName = resourceName;
        _resourceStore = resourceStore;
        _fallbackLocalizer = fallbackLocalizer;
        _fixedCulture = fixedCulture;
    }

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    public LocalizedString this[string name]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var culture = _fixedCulture ?? CultureInfo.CurrentUICulture;
            if (_resourceStore.TryGetString(_resourceName, culture, name, out var value))
            {
                return new LocalizedString(name, value, resourceNotFound: false);
            }

            var fallback = _fallbackLocalizer[name];
            return fallback.ResourceNotFound
                ? new LocalizedString(name, name, resourceNotFound: true)
                : fallback;
        }
    }

    /// <summary>
    /// 获取带格式化参数的本地化字符串
    /// </summary>
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var culture = _fixedCulture ?? CultureInfo.CurrentUICulture;
            if (_resourceStore.TryGetString(_resourceName, culture, name, out var template))
            {
                var formatted = arguments.Length == 0
                    ? template
                    : string.Format(culture, template, arguments);

                return new LocalizedString(name, formatted, resourceNotFound: false);
            }

            var fallback = _fallbackLocalizer[name, arguments];
            return fallback.ResourceNotFound
                ? new LocalizedString(name, name, resourceNotFound: true)
                : fallback;
        }
    }

    /// <summary>
    /// 获取全部字符串
    /// </summary>
    /// <param name="includeParentCultures"></param>
    /// <returns></returns>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var culture = _fixedCulture ?? CultureInfo.CurrentUICulture;
        var map = new Dictionary<string, LocalizedString>(StringComparer.OrdinalIgnoreCase);

        foreach (var jsonString in _resourceStore.GetAllStrings(_resourceName, culture, includeParentCultures))
        {
            map[jsonString.Name] = jsonString;
        }

        foreach (var fallback in _fallbackLocalizer.GetAllStrings(includeParentCultures))
        {
            map.TryAdd(fallback.Name, fallback);
        }

        return map.Values;
    }

    /// <summary>
    /// 使用指定文化创建本地化器
    /// </summary>
    /// <param name="culture"></param>
    /// <returns></returns>
    public IStringLocalizer WithCulture(CultureInfo culture)
    {
        return new XiHanJsonStringLocalizer(
            _resourceName,
            _resourceStore,
            _fallbackLocalizer,
            culture
        );
    }
}
