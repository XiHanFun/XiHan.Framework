#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAbacAttributeCollector
// Guid:de9a5bab-36ef-4ba8-9f6b-ea6847f07f83
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/10 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Claims;

namespace XiHan.Framework.Authorization.Abac;

/// <summary>
/// ABAC 属性收集器接口
/// </summary>
public interface IAbacAttributeCollector
{
    /// <summary>
    /// 收集 ABAC 属性
    /// </summary>
    /// <param name="principal">当前用户主体</param>
    /// <param name="resource">请求资源对象</param>
    /// <param name="permissionCode">权限编码</param>
    /// <param name="policyCode">ABAC 策略编码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>属性集合</returns>
    Task<AbacAttributeSet> CollectAsync(
        ClaimsPrincipal principal,
        object? resource,
        string permissionCode,
        string policyCode,
        CancellationToken cancellationToken = default);
}
