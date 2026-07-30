// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 基本租户信息
/// </summary>
public class BasicTenantInfo
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="tenantId"></param>
    /// <param name="name"></param>
    public BasicTenantInfo(long? tenantId, string? name = null)
    {
        TenantId = tenantId;
        Name = name;
    }

    /// <summary>
    /// 租户唯一标识符
    /// </summary>
    public long? TenantId { get; }

    /// <summary>
    /// 租户名称
    /// </summary>
    public string? Name { get; }
}
