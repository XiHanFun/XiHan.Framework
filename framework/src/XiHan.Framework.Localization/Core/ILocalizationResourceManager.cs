#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ILocalizationResourceManager
// Guid:3a5d0e1e-0b0a-4d2f-9c7d-3d8f1a7b2c1b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 12:18:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Localization.Resources;

namespace XiHan.Framework.Localization.Core;

/// <summary>
/// 本地化资源管理器接口
/// 负责管理所有注册的本地化资源
/// </summary>
public interface ILocalizationResourceManager
{
    /// <summary>
    /// 获取指定名称的本地化资源
    /// </summary>
    /// <param name="resourceName">资源名称</param>
    /// <returns>本地化资源</returns>
    ILocalizationResource GetResource(string resourceName);

    /// <summary>
    /// 获取所有注册的本地化资源
    /// </summary>
    /// <returns>本地化资源列表</returns>
    IReadOnlyList<ILocalizationResource> GetResources();

    /// <summary>
    /// 注册本地化资源
    /// </summary>
    /// <param name="resource">本地化资源</param>
    void AddResource(ILocalizationResource resource);

    /// <summary>
    /// 添加虚拟文件资源
    /// </summary>
    /// <param name="virtualPath">虚拟路径</param>
    /// <returns>本地化资源</returns>
    ILocalizationResource AddVirtualFileResource(string virtualPath);
}
