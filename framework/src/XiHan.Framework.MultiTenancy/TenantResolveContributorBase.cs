// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.MultiTenancy;

/// <summary>
/// 租户解析贡献者基类
/// </summary>
public abstract class TenantResolveContributorBase : ITenantResolveContributor
{
    /// <summary>
    /// 名称
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// 解析租户
    /// </summary>
    /// <param name="context">租户解析上下文</param>
    /// <returns></returns>
    public abstract Task ResolveAsync(ITenantResolveContext context);
}
