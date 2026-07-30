// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ObjectMapping.Modularity;

/// <summary>
/// 扩展属性权限策略配置
/// </summary>
public class ExtensionPropertyPermissionPolicyConfiguration
{
    /// <summary>
    /// 权限名称数组
    /// </summary>
    public string[] PermissionNames { get; set; } = [];

    /// <summary>
    /// 是否需要满足所有权限条件
    /// </summary>
    public bool RequiresAll { get; set; } = default!;
}
