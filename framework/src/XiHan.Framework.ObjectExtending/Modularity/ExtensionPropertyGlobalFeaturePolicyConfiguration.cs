#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExtensionPropertyGlobalFeaturePolicyConfiguration
// Guid:d2559a73-a073-493e-90d9-ebb9aeea1276
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 7:27:07
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.ObjectExtending.Modularity;

/// <summary>
/// 扩展属性全局功能策略配置
/// </summary>
public class ExtensionPropertyGlobalFeaturePolicyConfiguration
{
    /// <summary>
    /// 全局功能名称数组
    /// </summary>
    public string[] Features { get; set; } = [];

    /// <summary>
    /// 是否需要满足所有全局功能条件
    /// </summary>
    public bool RequiresAll { get; set; } = default!;
}
