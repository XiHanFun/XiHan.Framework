#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LocalizationResourceManager
// Guid:8e9f0d1c-2a3b-4c5d-6e7f-8a9b0c1d2e3f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 12:25:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Localization.Resources;
using XiHan.Framework.Localization.VirtualFileSystem;

namespace XiHan.Framework.Localization.Core;

/// <summary>
/// 本地化资源管理器
/// </summary>
public class LocalizationResourceManager : ILocalizationResourceManager
{
    private readonly ConcurrentDictionary<string, ILocalizationResource> _resources;
    private readonly ILogger<LocalizationResourceManager> _logger;
    private readonly IVirtualFileResourceFactory _virtualFileResourceFactory;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="virtualFileResourceFactory"></param>
    public LocalizationResourceManager(
        ILogger<LocalizationResourceManager> logger,
        IVirtualFileResourceFactory virtualFileResourceFactory)
    {
        _resources = new ConcurrentDictionary<string, ILocalizationResource>(StringComparer.OrdinalIgnoreCase);
        _logger = logger;
        _virtualFileResourceFactory = virtualFileResourceFactory;
    }

    /// <summary>
    /// 获取指定名称的本地化资源
    /// </summary>
    /// <param name="resourceName"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="XiHanException"></exception>
    public ILocalizationResource GetResource(string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName))
        {
            throw new ArgumentException("Resource name cannot be null or empty", nameof(resourceName));
        }

        if (!_resources.TryGetValue(resourceName, out var resource))
        {
            // 添加详细错误日志
            _logger.LogError("尝试访问未注册的本地化资源: {ResourceName}", resourceName);
            _logger.LogDebug("已注册资源列表: {Resources}",
                string.Join(", ", _resources.Keys));

            throw new XiHanException($"Localization resource '{resourceName}' not found!");
        }

        return resource;
    }

    /// <summary>
    /// 获取所有注册的本地化资源
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<ILocalizationResource> GetResources()
    {
        return _resources.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// 注册本地化资源
    /// </summary>
    /// <param name="resource"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="XiHanException"></exception>
    public void AddResource(ILocalizationResource resource)
    {
        if (resource == null)
        {
            throw new ArgumentNullException(nameof(resource));
        }

        if (_resources.TryGetValue(resource.ResourceName, out var existingResource))
        {
            // 如果已存在同名资源，检查是否为同一类型
            if (existingResource.GetType() != resource.GetType())
            {
                throw new XiHanException(
                    $"There is already a resource with name '{resource.ResourceName}' but with different type: {existingResource.GetType().AssemblyQualifiedName}");
            }

            _logger.LogWarning("Localization resource '{ResourceName}' is already registered, skipping...", resource.ResourceName);
            return;
        }

        if (!_resources.TryAdd(resource.ResourceName, resource))
        {
            throw new XiHanException($"Could not add resource '{resource.ResourceName}' to the dictionary!");
        }

        _logger.LogInformation("Added localization resource: {ResourceName}", resource.ResourceName);
    }

    /// <summary>
    /// 添加虚拟文件资源
    /// </summary>
    /// <param name="virtualPath"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public ILocalizationResource AddVirtualFileResource(string virtualPath)
    {
        if (string.IsNullOrEmpty(virtualPath))
        {
            throw new ArgumentException("Virtual path cannot be null or empty", nameof(virtualPath));
        }

        var resource = _virtualFileResourceFactory.Create(virtualPath);
        AddResource(resource);
        return resource;
    }
}
