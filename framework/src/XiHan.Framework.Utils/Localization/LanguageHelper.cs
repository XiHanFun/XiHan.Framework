#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LanguageHelper
// Guid:2115c528-b5b9-4b7a-938d-65908c5ec334
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/04/29 00:47:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Globalization;
using System.Resources;

namespace XiHan.Framework.Utils.Localization;

/// <summary>
/// 多语言支持帮助类
/// </summary>
public static class LanguageHelper
{
    private static readonly Dictionary<string, ResourceManager> ResourceManagers = [];
    private static CultureInfo _currentCulture = CultureInfo.CurrentCulture;

    /// <summary>
    /// 设置当前文化
    /// </summary>
    /// <param name="cultureName">文化名称，如 "zh-CN", "en-US"</param>
    public static void SetCurrentCulture(string cultureName)
    {
        _currentCulture = new CultureInfo(cultureName);
    }

    /// <summary>
    /// 获取当前文化
    /// </summary>
    /// <returns>当前文化</returns>
    public static CultureInfo GetCurrentCulture()
    {
        return _currentCulture;
    }

    /// <summary>
    /// 注册资源管理器
    /// </summary>
    /// <param name="name">资源名称</param>
    /// <param name="resourceManager">资源管理器</param>
    public static void RegisterResourceManager(string name, ResourceManager resourceManager)
    {
        ResourceManagers[name] = resourceManager;
    }

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    /// <param name="resourceName">资源名称</param>
    /// <param name="key">资源键</param>
    /// <returns>本地化字符串</returns>
    public static string GetString(string resourceName, string key)
    {
        return ResourceManagers.TryGetValue(resourceName, out var resourceManager)
            ? resourceManager.GetString(key, _currentCulture) ?? key
            : key;
    }

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    /// <param name="resourceName">资源名称</param>
    /// <param name="key">资源键</param>
    /// <param name="args">格式化参数</param>
    /// <returns>本地化字符串</returns>
    public static string GetString(string resourceName, string key, params object[] args)
    {
        var format = GetString(resourceName, key);
        return string.Format(_currentCulture, format, args);
    }

    /// <summary>
    /// 获取所有支持的文化
    /// </summary>
    /// <returns>文化列表</returns>
    public static IEnumerable<CultureInfo> GetSupportedCultures()
    {
        return CultureInfo.GetCultures(CultureTypes.AllCultures);
    }

    /// <summary>
    /// 检查文化是否支持
    /// </summary>
    /// <param name="cultureName">文化名称</param>
    /// <returns>是否支持</returns>
    public static bool IsCultureSupported(string cultureName)
    {
        try
        {
            var culture = new CultureInfo(cultureName);
            return GetSupportedCultures().Contains(culture);
        }
        catch
        {
            return false;
        }
    }
}
