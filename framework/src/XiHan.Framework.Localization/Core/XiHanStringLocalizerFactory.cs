#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanStringLocalizerFactory
// Guid:7d6e5f4a-3e2d-1f0a-9b8c-7d6e-5f4a-3e2d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 13:30:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Localization;
using XiHan.Framework.Localization.Provider;
using XiHan.Framework.Localization.Resources;

namespace XiHan.Framework.Localization.Core;

/// <summary>
/// 曦寒字符串本地化器工厂
/// </summary>
public class XiHanStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly ILocalizationResourceManager _resourceManager;
    private readonly IResourceStringProvider _resourceStringProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="resourceManager">资源管理器</param>
    /// <param name="resourceStringProvider">资源字符串提供者</param>
    public XiHanStringLocalizerFactory(
        ILocalizationResourceManager resourceManager,
        IResourceStringProvider resourceStringProvider)
    {
        _resourceManager = resourceManager;
        _resourceStringProvider = resourceStringProvider;
    }

    /// <summary>
    /// 创建特定类型的本地化器
    /// </summary>
    /// <param name="resourceType">资源类型</param>
    /// <returns>字符串本地化器</returns>
    public IStringLocalizer Create(Type resourceType)
    {
        ArgumentNullException.ThrowIfNull(resourceType);

        var resourceName = resourceType.FullName!;
        return Create(resourceName);
    }

    /// <summary>
    /// 创建特定位置的本地化器
    /// </summary>
    /// <param name="baseName">基础名称</param>
    /// <param name="location">位置</param>
    /// <returns>字符串本地化器</returns>
    public IStringLocalizer Create(string baseName, string location)
    {
        if (string.IsNullOrEmpty(baseName))
        {
            throw new ArgumentNullException(nameof(baseName));
        }

        var resourceName = string.IsNullOrEmpty(location) ? baseName : $"{location}.{baseName}";
        return Create(resourceName);
    }

    /// <summary>
    /// 创建特定名称的本地化器
    /// </summary>
    /// <param name="resourceName">资源名称</param>
    /// <returns>字符串本地化器</returns>
    private IStringLocalizer Create(string resourceName)
    {
        try
        {
            var resource = _resourceManager.GetResource(resourceName);
            return new XiHanStringLocalizer(resource, this, _resourceStringProvider);
        }
        catch
        {
            // 如果找不到资源，创建一个空的资源
            var resource = new DefaultLocalizationResource(resourceName);
            _resourceManager.AddResource(resource);
            return new XiHanStringLocalizer(resource, this, _resourceStringProvider);
        }
    }
}
