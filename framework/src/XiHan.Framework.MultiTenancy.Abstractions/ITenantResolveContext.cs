// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
