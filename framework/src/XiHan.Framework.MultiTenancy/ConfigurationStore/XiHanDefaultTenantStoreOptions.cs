// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
