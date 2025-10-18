#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanTenantResolveOptions
// Guid:f414dbbf-ec97-4608-a287-96db4528a92e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 7:00:23
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.MultiTenancy;

/// <summary>
/// 曦寒多租户解析选项
/// </summary>
public class XiHanTenantResolveOptions
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanTenantResolveOptions()
    {
        TenantResolvers = [];
    }

    /// <summary>
    /// 租户解析器集合
    /// </summary>
    [NotNull]
    public List<ITenantResolveContributor> TenantResolvers { get; }

    /// <summary>
    /// 回退租户（当无法解析租户时使用该租户）
    /// </summary>
    public string? FallbackTenant { get; set; }
}
