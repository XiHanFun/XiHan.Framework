#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanStringLocalizer
// Guid:6a7b8c9d-0e1f-4a5b-9c6d-7e8f9a0b1c2d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 12:22:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Localization;
using System.Globalization;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Localization.Resources;

namespace XiHan.Framework.Localization.Core;

/// <summary>
/// 曦寒字符串本地化器
/// </summary>
public class XiHanStringLocalizer : IXiHanStringLocalizer
{
    private readonly ILocalizationResource _resource;
    private readonly IStringLocalizerFactory _factory;
    private readonly IResourceStringProvider _resourceStringProvider;
    private readonly CultureInfo _culture;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="resource"></param>
    /// <param name="factory"></param>
    /// <param name="resourceStringProvider"></param>
    /// <param name="cultureName"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public XiHanStringLocalizer(
        ILocalizationResource resource,
        IStringLocalizerFactory factory,
        IResourceStringProvider resourceStringProvider,
        string? cultureName = null)
    {
        _resource = resource ?? throw new ArgumentNullException(nameof(resource));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _resourceStringProvider = resourceStringProvider ?? throw new ArgumentNullException(nameof(resourceStringProvider));
        _culture = cultureName != null ? new CultureInfo(cultureName) : CultureInfo.CurrentUICulture;
    }

    /// <summary>
    /// 获取指定语言的本地化字符串
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public LocalizedString this[string name]
    {
        get
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var value = GetStringSafely(name, _culture);
            return new LocalizedString(name, value ?? name, resourceNotFound: value == null);
        }
    }

    /// <summary>
    /// 获取指定语言的本地化字符串，支持参数插值
    /// </summary>
    /// <param name="name"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var format = GetStringSafely(name, _culture);
            var value = string.Format(_culture, format ?? name, arguments);
            return new LocalizedString(name, value, resourceNotFound: format == null);
        }
    }

    /// <summary>
    /// 获取所有本地化字符串
    /// </summary>
    /// <param name="includeParentCultures"></param>
    /// <returns></returns>
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        return _resourceStringProvider.GetAllStrings(_resource, _culture.Name, includeParentCultures);
    }

    /// <summary>
    /// 获取指定语言的本地化字符串
    /// </summary>
    /// <param name="name"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public LocalizedString GetWithCulture(string name, string culture)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Name cannot be null or empty", nameof(name));
        }

        if (string.IsNullOrEmpty(culture))
        {
            throw new ArgumentException("Culture cannot be null or empty", nameof(culture));
        }

        var cultureInfo = new CultureInfo(culture);
        var value = GetStringSafely(name, cultureInfo);
        return new LocalizedString(name, value ?? name, resourceNotFound: value == null);
    }

    /// <summary>
    /// 获取指定语言的本地化字符串，支持参数插值
    /// </summary>
    /// <param name="name"></param>
    /// <param name="culture"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public LocalizedString GetWithCulture(string name, string culture, params object[] arguments)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Name cannot be null or empty", nameof(name));
        }

        if (string.IsNullOrEmpty(culture))
        {
            throw new ArgumentException("Culture cannot be null or empty", nameof(culture));
        }

        var cultureInfo = new CultureInfo(culture);
        var format = GetStringSafely(name, cultureInfo);
        var value = string.Format(cultureInfo, format ?? name, arguments);
        return new LocalizedString(name, value, resourceNotFound: format == null);
    }

    /// <summary>
    /// 获取当前资源可用的所有文化
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<string> GetSupportedCultures()
    {
        return _resourceStringProvider.GetSupportedCultures(_resource);
    }

    /// <summary>
    /// 获取根资源路径
    /// </summary>
    /// <returns></returns>
    public string GetResourceBasePath()
    {
        return _resource.BasePath;
    }

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    /// <param name="name"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    private string GetStringSafely(string name, CultureInfo culture)
    {
        try
        {
            return _resourceStringProvider.GetString(_resource, name, culture.Name)
                ?? _resourceStringProvider.GetString(_resource, name, _resource.DefaultCulture)
                ?? name;
        }
        catch (Exception ex)
        {
            throw new XiHanException($"Error retrieving localized string: {name}", ex);
        }
    }
}
