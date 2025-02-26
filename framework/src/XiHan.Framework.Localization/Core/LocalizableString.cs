#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LocalizableString
// Guid:8c123456-1a2b-3c4d-5e6f-7890abcdef12
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/01 10:30:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System.Globalization;
using XiHan.Framework.Localization.Extensions;

namespace XiHan.Framework.Localization.Core;

/// <summary>
/// 可本地化字符串，用于将固定字符串转换为可本地化文本
/// </summary>
public class LocalizableString
{
    private readonly string _name;
    private readonly string? _resourceName;
    private readonly Type? _resourceType;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">资源键</param>
    public LocalizableString(string name)
    {
        _name = name;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">资源键</param>
    /// <param name="resourceType">资源类型</param>
    public LocalizableString(string name, Type resourceType)
    {
        _name = name;
        _resourceType = resourceType;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">资源键</param>
    /// <param name="resourceName">资源名称</param>
    public LocalizableString(string name, string resourceName)
    {
        _name = name;
        _resourceName = resourceName;
    }

    /// <summary>
    /// 获取当前文化下的本地化字符串
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns>本地化后的字符串</returns>
    public virtual string Localize(IServiceProvider serviceProvider)
    {
        return Localize(serviceProvider, null);
    }

    /// <summary>
    /// 获取指定文化下的本地化字符串
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="culture">指定文化</param>
    /// <returns>本地化后的字符串</returns>
    public virtual string Localize(IServiceProvider serviceProvider, CultureInfo? culture)
    {
        IStringLocalizer localizer;

        if (_resourceType != null)
        {
            var localizerFactory = serviceProvider.GetRequiredService<IStringLocalizerFactory>();
            localizer = localizerFactory.Create(_resourceType);
        }
        else if (!string.IsNullOrEmpty(_resourceName))
        {
            localizer = serviceProvider.GetXiHanLocalizer(_resourceName);
        }
        else
        {
            return _name;
        }

        var currentCulture = culture ?? CultureInfo.CurrentUICulture;
        var oldCulture = CultureInfo.CurrentUICulture;

        try
        {
            if (currentCulture != oldCulture)
            {
                CultureInfo.CurrentUICulture = currentCulture;
            }

            return localizer[_name];
        }
        finally
        {
            if (currentCulture != oldCulture)
            {
                CultureInfo.CurrentUICulture = oldCulture;
            }
        }
    }

    /// <summary>
    /// 使用原始的未本地化名称
    /// </summary>
    /// <returns>原始名称</returns>
    public override string ToString()
    {
        return _name;
    }
}
