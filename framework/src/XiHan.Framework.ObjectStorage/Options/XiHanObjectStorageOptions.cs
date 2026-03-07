#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanObjectStorageOptions
// Guid:f31e5ef6-78d8-4b0a-ab8e-c84f540b2bb9
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 01:42:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.ObjectStorage.Constants;

namespace XiHan.Framework.ObjectStorage.Options;

/// <summary>
/// 曦寒对象存储配置选项
/// </summary>
public class XiHanObjectStorageOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:ObjectStorage";

    /// <summary>
    /// 默认存储提供程序名称
    /// </summary>
    public string DefaultProvider { get; set; } = ObjectStorageProviderNames.Local;

    /// <summary>
    /// 启用的存储提供程序列表
    /// </summary>
    public string[] EnabledProviders { get; set; } = [ObjectStorageProviderNames.Local];

    /// <summary>
    /// 路由键与提供程序映射
    /// </summary>
    /// <remarks>
    /// Key: 业务路由键（如 avatar、attachment）
    /// Value: 提供程序名称（如 Local、MinIO）
    /// </remarks>
    public Dictionary<string, string> RouteProviderMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 当存在路由键但未命中映射时是否抛异常
    /// </summary>
    public bool StrictRouteMatch { get; set; } = false;
}
