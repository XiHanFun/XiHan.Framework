#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BasicTenantInfo
// Guid:fb9608bd-251b-49af-9f00-1515d61dfbf7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 06:29:16
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
    public BasicTenantInfo(Guid? tenantId, string? name = null)
    {
        TenantId = tenantId;
        Name = name;
    }

    /// <summary>
    /// 租户唯一标识符
    /// </summary>
    public Guid? TenantId { get; }

    /// <summary>
    /// 租户名称
    /// </summary>
    public string? Name { get; }
}
