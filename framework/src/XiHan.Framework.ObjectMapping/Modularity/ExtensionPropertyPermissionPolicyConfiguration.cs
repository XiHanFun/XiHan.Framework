#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExtensionPropertyPermissionPolicyConfiguration
// Guid:f1cceabb-a12d-41c0-8b9e-1026fa3494e5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 7:27:51
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
