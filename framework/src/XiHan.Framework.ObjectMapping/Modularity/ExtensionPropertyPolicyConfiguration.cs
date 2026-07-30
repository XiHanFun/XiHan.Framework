// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
