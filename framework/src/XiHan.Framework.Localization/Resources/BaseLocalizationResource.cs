#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BaseLocalizationResource
// Guid:9a8b7c6d-5e4f-3d2c-1b0a-9f8e7d6c5b4a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 12:28:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Exceptions;

namespace XiHan.Framework.Localization.Resources;

/// <summary>
/// 基础本地化资源
/// </summary>
public abstract class BaseLocalizationResource : ILocalizationResource
{
    private readonly List<ILocalizationResource> _baseResources;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="resourceName"></param>
    /// <param name="defaultCulture"></param>
    /// <param name="basePath"></param>
    /// <param name="priority"></param>
    /// <exception cref="ArgumentNullException"></exception>
    protected BaseLocalizationResource(string resourceName, string defaultCulture = "en", string? basePath = "", int priority = 0)
    {
        ResourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
        DefaultCulture = defaultCulture ?? throw new ArgumentNullException(nameof(defaultCulture));
        BasePath = basePath ?? string.Empty;
        Priority = priority;
        _baseResources = [];
    }

    /// <summary>
    /// 资源名称
    /// </summary>
    public string ResourceName { get; }

    /// <summary>
    /// 默认文化代码
    /// </summary>
    public string DefaultCulture { get; }

    /// <summary>
    /// 基础目录路径
    /// </summary>
    public string BasePath { get; }

    /// <summary>
    /// 优先级(值越大优先级越高)
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// 继承资源列表(按优先级排序)
    /// </summary>
    public IReadOnlyList<ILocalizationResource> BaseResources => _baseResources.AsReadOnly();

    /// <summary>
    /// 继承自指定资源
    /// </summary>
    /// <param name="resource"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="XiHanException"></exception>
    public virtual ILocalizationResource InheritFrom(ILocalizationResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (resource == this)
        {
            throw new XiHanException("Resource can't inherit from itself!");
        }

        if (BaseResources.Contains(resource))
        {
            return this;
        }

        _baseResources.Add(resource);
        // 按优先级降序排序
        _baseResources.Sort((x, y) => y.Priority.CompareTo(x.Priority));

        return this;
    }

    /// <summary>
    /// 添加继承资源
    /// </summary>
    /// <param name="resourceType"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="XiHanException"></exception>
    public virtual ILocalizationResource InheritFrom(Type resourceType)
    {
        return resourceType is null
            ? throw new ArgumentNullException(nameof(resourceType))
            : !typeof(ILocalizationResource).IsAssignableFrom(resourceType)
                ? throw new ArgumentException($"Given type ({resourceType.AssemblyQualifiedName}) must implement the {typeof(ILocalizationResource).FullName} interface!")
                : Activator.CreateInstance(resourceType) is not ILocalizationResource resource
                    ? throw new XiHanException($"Could not create resource instance from given type: {resourceType.AssemblyQualifiedName}")
                    : InheritFrom(resource);
    }

    /// <summary>
    /// 获取资源支持的所有文化
    /// </summary>
    /// <returns>支持的文化名称列表</returns>
    public virtual IEnumerable<string> GetSupportedCultures()
    {
        var cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // 添加默认文化
            DefaultCulture
        };

        // 从基础资源中获取更多的文化
        foreach (var baseResource in BaseResources)
        {
            foreach (var culture in baseResource.GetSupportedCultures())
            {
                _ = cultures.Add(culture);
            }
        }

        return cultures;
    }
}
