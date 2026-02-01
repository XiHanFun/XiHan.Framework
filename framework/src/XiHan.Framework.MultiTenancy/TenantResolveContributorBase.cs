#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TenantResolveContributorBase
// Guid:665b90a8-1ceb-46df-96b5-3334ae533864
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 07:04:31
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
