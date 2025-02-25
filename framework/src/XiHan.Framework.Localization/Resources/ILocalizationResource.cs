#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ILocalizationResource
// Guid:d2b6c4a1-9e5f-4d3d-8b7a-6c5f4e3d2a1b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 12:20:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Localization.Resources;

/// <summary>
/// 本地化资源接口
/// </summary>
public interface ILocalizationResource
{
    /// <summary>
    /// 资源名称
    /// </summary>
    string ResourceName { get; }

    /// <summary>
    /// 默认文化代码
    /// </summary>
    string DefaultCulture { get; }

    /// <summary>
    /// 基础目录路径
    /// </summary>
    string BasePath { get; }

    /// <summary>
    /// 优先级（值越大优先级越高）
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// 继承资源列表（按优先级排序）
    /// </summary>
    IReadOnlyList<ILocalizationResource> BaseResources { get; }

    /// <summary>
    /// 继承自指定资源
    /// </summary>
    /// <param name="resource">基础资源</param>
    /// <returns>当前资源实例</returns>
    ILocalizationResource InheritFrom(ILocalizationResource resource);

    /// <summary>
    /// 添加继承资源
    /// </summary>
    /// <param name="resourceType">资源类型</param>
    /// <returns>当前资源实例</returns>
    ILocalizationResource InheritFrom(Type resourceType);
}
