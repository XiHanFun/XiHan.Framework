#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanDefaultTenantStoreOptions
// Guid:0d54387d-1ea6-4bea-862a-445d9bc8da19
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 06:47:43
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.MultiTenancy.ConfigurationStore;

/// <summary>
/// 曦寒默认租户存储选项
/// </summary>
public class XiHanDefaultTenantStoreOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:MultiTenancy:DefaultStore";

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanDefaultTenantStoreOptions()
    {
        Tenants = [];
    }

    /// <summary>
    /// 租户配置集合
    /// </summary>
    public TenantConfiguration[] Tenants { get; set; }
}
