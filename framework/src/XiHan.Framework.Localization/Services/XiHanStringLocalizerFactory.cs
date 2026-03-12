#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanStringLocalizerFactory
// Guid:84ca4d5f-5534-4e14-a3f7-33848dbd9d42
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/13 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Localization;
using System.Collections.Concurrent;

namespace XiHan.Framework.Localization.Services;

/// <summary>
/// XiHan 本地化工厂（JSON 优先，ResourceManager 回退）
/// </summary>
public sealed class XiHanStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly JsonLocalizationResourceStore _resourceStore;
    private readonly ResourceManagerStringLocalizerFactory _resourceManagerFactory;
    private readonly ConcurrentDictionary<string, IStringLocalizer> _cache = new();

    /// <summary>
    /// 初始化本地化工厂
    /// </summary>
    /// <param name="resourceStore"></param>
    /// <param name="resourceManagerFactory"></param>
    public XiHanStringLocalizerFactory(
        JsonLocalizationResourceStore resourceStore,
        ResourceManagerStringLocalizerFactory resourceManagerFactory)
    {
        _resourceStore = resourceStore;
        _resourceManagerFactory = resourceManagerFactory;
    }

    /// <summary>
    /// 通过资源类型创建本地化器
    /// </summary>
    /// <param name="resourceSource"></param>
    /// <returns></returns>
    public IStringLocalizer Create(Type resourceSource)
    {
        ArgumentNullException.ThrowIfNull(resourceSource);

        var cacheKey = $"type:{resourceSource.AssemblyQualifiedName}";
        return _cache.GetOrAdd(cacheKey, _ =>
        {
            var fallback = _resourceManagerFactory.Create(resourceSource);
            var resourceName = resourceSource.Name;
            return new XiHanJsonStringLocalizer(resourceName, _resourceStore, fallback);
        });
    }

    /// <summary>
    /// 通过基础名和位置创建本地化器
    /// </summary>
    /// <param name="baseName"></param>
    /// <param name="location"></param>
    /// <returns></returns>
    public IStringLocalizer Create(string baseName, string location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(location);

        var cacheKey = $"name:{location}:{baseName}";
        return _cache.GetOrAdd(cacheKey, _ =>
        {
            var fallback = _resourceManagerFactory.Create(baseName, location);
            var resourceName = ExtractResourceName(baseName);
            return new XiHanJsonStringLocalizer(resourceName, _resourceStore, fallback);
        });
    }

    private static string ExtractResourceName(string baseName)
    {
        var normalized = baseName.Replace('\\', '.').Replace('/', '.');
        var segments = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0
            ? baseName
            : segments[^1];
    }
}
