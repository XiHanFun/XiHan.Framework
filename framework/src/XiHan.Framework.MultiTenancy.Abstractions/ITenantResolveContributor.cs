// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 租户解析贡献者接口
/// </summary>
public interface ITenantResolveContributor
{
    /// <summary>
    /// 名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 解析租户
    /// </summary>
    /// <param name="context">租户解析上下文</param>
    /// <returns></returns>
    Task ResolveAsync(ITenantResolveContext context);
}
