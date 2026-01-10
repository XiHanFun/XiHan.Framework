#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITenantResolveContext
// Guid:457953d6-39a7-4e9a-bf8c-85f113ac789c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 7:02:06
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 租户解析上下文接口
/// </summary>
public interface ITenantResolveContext : IServiceProviderAccessor
{
    /// <summary>
    /// 租户唯一标识或名称
    /// </summary>
    string? TenantIdOrName { get; set; }

    /// <summary>
    /// 是否已处理
    /// </summary>
    bool Handled { get; set; }
}
