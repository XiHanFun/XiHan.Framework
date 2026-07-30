// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
            // ResourceManager 兜底要求 location 为可加载的程序集；JSON-first 资源（如 ApiResponse/Errors）
            // 无 backing 程序集，此处 Create 会抛 FileNotFound（把 location 当程序集名 Assembly.Load）。
            // 容错为「无 ResourceManager 兜底（仅 JSON）」，避免 JSON 资源因兜底加载程序集失败而整体崩溃。
            IStringLocalizer fallback;
            try
            {
                fallback = _resourceManagerFactory.Create(baseName, location);
            }
            catch
            {
                fallback = NullStringLocalizer.Instance;
            }

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

    /// <summary>
    /// 空本地化器：当 ResourceManager 兜底不可用（资源无 backing 程序集）时占位，所有键按「未找到」返回，
    /// 实际取值仅靠 JSON 资源存储（<see cref="XiHanJsonStringLocalizer"/> 先查 JSON、miss 才用此兜底）。
    /// </summary>
    private sealed class NullStringLocalizer : IStringLocalizer
    {
        public static readonly NullStringLocalizer Instance = new();

        public LocalizedString this[string name] => new(name, name, resourceNotFound: true);

        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: true);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
