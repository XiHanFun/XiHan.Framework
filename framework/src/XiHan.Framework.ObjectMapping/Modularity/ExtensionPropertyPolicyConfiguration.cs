#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExtensionPropertyPolicyConfiguration
// Guid:8da9356a-4e21-4c28-84c2-be1e10677375
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 7:26:34
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.ObjectMapping.Modularity;

/// <summary>
/// 扩展属性策略配置
/// </summary>
public class ExtensionPropertyPolicyConfiguration
{
    /// <summary>
    /// 初始化扩展属性策略配置
    /// </summary>
    public ExtensionPropertyPolicyConfiguration()
    {
        GlobalFeatures = new ExtensionPropertyGlobalFeaturePolicyConfiguration();
        Features = new ExtensionPropertyFeaturePolicyConfiguration();
        Permissions = new ExtensionPropertyPermissionPolicyConfiguration();
    }

    /// <summary>
    /// 全局功能策略配置
    /// </summary>
    public ExtensionPropertyGlobalFeaturePolicyConfiguration GlobalFeatures { get; set; }

    /// <summary>
    /// 功能策略配置
    /// </summary>
    public ExtensionPropertyFeaturePolicyConfiguration Features { get; set; }

    /// <summary>
    /// 权限策略配置
    /// </summary>
    public ExtensionPropertyPermissionPolicyConfiguration Permissions { get; set; }
}
