// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.MultiTenancy;

/// <summary>
/// 曦寒多租户解析选项
/// </summary>
public class XiHanTenantResolveOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:MultiTenancy:Resolve";

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanTenantResolveOptions()
    {
        TenantResolvers = [];
        HeaderKeys = ["X-Tenant-Id", "x-tenant-id", "TenantId"];
        QueryStringKeys = ["tenantId", "tenant"];
    }

    /// <summary>
    /// 租户解析器集合
    /// </summary>
    [NotNull]
    public List<ITenantResolveContributor> TenantResolvers { get; }

    /// <summary>
    /// 是否启用 Header 租户解析
    /// </summary>
    public bool EnableHeaderResolve { get; set; } = true;

    /// <summary>
    /// Header 租户键集合（按优先级顺序）
    /// </summary>
    [NotNull]
    public string[] HeaderKeys { get; set; }

    /// <summary>
    /// 是否启用 QueryString 租户解析
    /// </summary>
    public bool EnableQueryStringResolve { get; set; } = true;

    /// <summary>
    /// QueryString 租户键集合（按优先级顺序）
    /// </summary>
    [NotNull]
    public string[] QueryStringKeys { get; set; }

    /// <summary>
    /// 回退租户（当无法解析租户时使用该租户）
    /// </summary>
    public string? FallbackTenant { get; set; }
}
