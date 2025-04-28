#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonLocalizationResourceProvider
// Guid:1d0b5c4a-9e8d-7f6a-5b4c-3e2d1f0a9b8c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 12:36:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;
using XiHan.Framework.Core.Localization;
using XiHan.Framework.Localization.Options;
using XiHan.Framework.Localization.Resources;
using XiHan.Framework.VirtualFileSystem;

namespace XiHan.Framework.Localization.Provider;

/// <summary>
/// JSON本地化资源提供程序
/// </summary>
public class JsonLocalizationResourceProvider : IResourceStringProvider
{
    private readonly ILogger<JsonLocalizationResourceProvider> _logger;
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _resourceCache;
    private readonly IVirtualFileSystem _virtualFileSystem;
    private readonly XiHanLocalizationOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="virtualFileSystem">虚拟文件系统</param>
    /// <param name="options">本地化选项</param>
    public JsonLocalizationResourceProvider(
        ILogger<JsonLocalizationResourceProvider> logger,
        IVirtualFileSystem virtualFileSystem,
        IOptions<XiHanLocalizationOptions> options)
    {
        _logger = logger;
        _virtualFileSystem = virtualFileSystem;
        _resourceCache = new ConcurrentDictionary<string, Dictionary<string, string>>();
        _options = options.Value;
    }

    /// <summary>
    /// 获取指定名称的本地化字符串
    /// </summary>
    /// <param name="resource">本地化资源</param>
    /// <param name="name">资源名称</param>
    /// <param name="cultureName">文化名称</param>
    /// <returns>本地化字符串，未找到时返回null</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public string? GetString(ILocalizationResource resource, string name, string cultureName)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentNullException(nameof(name));
        }

        //// 优先尝试从当前资源获取
        //var currentValue = GetStringFromResource(resource, name, cultureName);
        //if (currentValue is not null)
        //{
        //    return currentValue;
        //}

        // 如果当前资源没有找到，尝试从基础资源中查找
        foreach (var baseResource in resource.BaseResources)
        {
            var baseValue = GetString(baseResource, name, cultureName);
            if (baseValue is not null)
            {
                return baseValue;
            }
        }

        // 如果文化特定的资源没有找到，尝试使用默认文化
        return cultureName != resource.DefaultCulture ? GetString(resource, name, resource.DefaultCulture) : null;
    }

    /// <summary>
    /// 获取指定名称的本地化字符串集合
    /// </summary>
    /// <param name="resource">本地化资源</param>
    /// <param name="cultureName">文化名称</param>
    /// <param name="includeParentCultures">是否包含父级文化</param>
    /// <returns>本地化字符串集合</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<LocalizedString> GetAllStrings(ILocalizationResource resource, string cultureName, bool includeParentCultures)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var result = new Dictionary<string, LocalizedString>();

        // 收集基础资源的字符串
        if (includeParentCultures)
        {
            foreach (var baseResource in resource.BaseResources)
            {
                foreach (var localizedString in GetAllStrings(baseResource, cultureName, true))
                {
                    result.TryAdd(localizedString.Name, localizedString);
                }
            }
        }

        // 添加当前资源的字符串，覆盖基础资源
        var strings = GetAllStringsFromResource(resource, cultureName);
        foreach (var s in strings)
        {
            result[s.Name] = s;
        }

        return result.Values;
    }

    /// <summary>
    /// 获取支持的文化列表
    /// </summary>
    /// <param name="resource">本地化资源</param>
    /// <returns>支持的文化列表</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IReadOnlyList<string> GetSupportedCultures(ILocalizationResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var cultures = new HashSet<string>();

        // 如果是虚拟文件资源，检测所有可用的文化
        if (resource is VirtualFileLocalizationResource vfResource)
        {
            // 从文件系统中检测可用的文化
            var basePath = vfResource.BasePath;
            var resourceName = vfResource.ResourceName;

            try
            {
                // 获取目录内容
                var directoryContents = _virtualFileSystem.GetDirectoryContents(basePath);
                if (directoryContents.Exists)
                {
                    // 文件名格式应为：ResourceName.culture.json
                    var filePattern = $"{resourceName}.";
                    foreach (var file in directoryContents)
                    {
                        if (file.IsDirectory || !file.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!file.Name.StartsWith(filePattern, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // 从文件名提取文化代码
                        var fileName = file.Name;
                        var withoutExtension = fileName[..fileName.LastIndexOf('.')];

                        if (!withoutExtension.Contains('.'))
                        {
                            continue;
                        }

                        var cultureName = withoutExtension[(withoutExtension.LastIndexOf('.') + 1)..];

                        // 验证文化代码的有效性
                        if (CultureHelper.IsValidCultureCode(cultureName))
                        {
                            _logger.LogDebug("找到有效的文化文件: {FileName}, 文化代码: {Culture}", fileName, cultureName);
                            _ = cultures.Add(cultureName);
                        }
                        else
                        {
                            _logger.LogWarning("文件名包含无效的文化代码: {FileName}, 文化代码: {Culture}", fileName, cultureName);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("目录不存在: {BasePath}", basePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描本地化资源目录时出错: {BasePath}", basePath);
            }

            // 至少添加默认文化
            if (cultures.Count == 0)
            {
                _logger.LogInformation("未找到本地化文件，添加默认文化: {Culture}", resource.DefaultCulture);
                _ = cultures.Add(resource.DefaultCulture);
            }
        }

        // 添加基础资源的文化
        foreach (var baseResource in resource.BaseResources)
        {
            foreach (var culture in GetSupportedCultures(baseResource))
            {
                _ = cultures.Add(culture);
            }
        }

        return [.. cultures];
    }

    /// <summary>
    /// 将JSON对象扁平化为键值对
    /// </summary>
    /// <param name="element">JSON元素</param>
    /// <param name="prefix">前缀</param>
    /// <param name="dictionary">结果字典</param>
    private static void FlattenJsonObject(JsonElement element, string prefix, Dictionary<string, string> dictionary)
    {
        foreach (var property in element.EnumerateObject())
        {
            var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}:{property.Name}";

            switch (property.Value.ValueKind)
            {
                case JsonValueKind.String:
                    dictionary[key] = property.Value.GetString() ?? string.Empty;
                    break;

                case JsonValueKind.Object:
                    FlattenJsonObject(property.Value, key, dictionary);
                    break;

                default:
                    dictionary[key] = property.Value.ToString();
                    break;
            }
        }
    }

    /// <summary>
    /// 获取指定资源的字符串
    /// </summary>
    /// <param name="resource">本地化资源</param>
    /// <param name="name">资源名称</param>
    /// <param name="cultureName">文化名称</param>
    /// <returns>本地化字符串，未找到时返回null</returns>
    private string? GetStringFromResource(ILocalizationResource resource, string name, string cultureName)
    {
        if (resource is not VirtualFileLocalizationResource vfResource)
        {
            return null;
        }

        var cacheKey = $"{resource.ResourceName}:{cultureName}";

        // 尝试从缓存中获取
        if (_resourceCache.TryGetValue(cacheKey, out var stringDictionary))
        {
            return stringDictionary is not null && stringDictionary.TryGetValue(name, out var cacheValue) ? cacheValue : null;
        }

        // 如果缓存中没有，尝试加载资源
        stringDictionary = LoadResourceFile(vfResource, cultureName);
        if (stringDictionary is not null)
        {
            _resourceCache[cacheKey] = stringDictionary;
        }

        // 如果找到了资源字典，尝试获取特定的字符串
        return stringDictionary is not null && stringDictionary.TryGetValue(name, out var oValue) ? oValue : null;
    }

    /// <summary>
    /// 获取指定资源的所有字符串
    /// </summary>
    /// <param name="resource">本地化资源</param>
    /// <param name="cultureName">文化名称</param>
    /// <returns>本地化字符串集合</returns>
    private IEnumerable<LocalizedString> GetAllStringsFromResource(ILocalizationResource resource, string cultureName)
    {
        if (resource is not VirtualFileLocalizationResource vfResource)
        {
            return [];
        }

        var cacheKey = $"{resource.ResourceName}:{cultureName}";

        // 尝试从缓存中获取
        if (_resourceCache.TryGetValue(cacheKey, out var stringDictionary))
        {
            return stringDictionary is not null
                ? stringDictionary.Select(kvp => new LocalizedString(kvp.Key, kvp.Value, false))
                : [];
        }

        // 如果缓存中没有，尝试加载资源
        stringDictionary = LoadResourceFile(vfResource, cultureName);
        if (stringDictionary is not null)
        {
            _resourceCache[cacheKey] = stringDictionary;
        }

        // 如果找到了资源字典，返回所有字符串
        return stringDictionary is not null
            ? stringDictionary.Select(kvp => new LocalizedString(kvp.Key, kvp.Value, false))
            : [];
    }

    /// <summary>
    /// 加载资源文件
    /// </summary>
    /// <param name="resource">虚拟文件资源</param>
    /// <param name="cultureName">文化名称</param>
    /// <returns>资源字典，加载失败时返回null</returns>
    private Dictionary<string, string>? LoadResourceFile(VirtualFileLocalizationResource resource, string cultureName)
    {
        try
        {
            using var stream = resource.GetStream(cultureName);
            if (stream is null)
            {
                _logger.LogDebug("未找到文化资源文件: {Resource}.{Culture}.json", resource.ResourceName, cultureName);
                return null;
            }

            _logger.LogDebug("加载文化资源文件: {Resource}.{Culture}.json", resource.ResourceName, cultureName);

            using var jsonDocument = JsonDocument.Parse(stream);
            var root = jsonDocument.RootElement;

            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (root.ValueKind == JsonValueKind.Object)
            {
                FlattenJsonObject(root, "", dictionary);
            }

            _logger.LogDebug("从文件加载了 {Count} 个本地化条目", dictionary.Count);
            return dictionary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载资源文件失败: {ResourceName}, 文化: {Culture}",
                resource.ResourceName, cultureName);
            return null;
        }
    }
}
