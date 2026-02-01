#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITenantResolveContributor
// Guid:448849d6-ddf4-447e-a41b-08835fb58fbd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 07:01:31
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
