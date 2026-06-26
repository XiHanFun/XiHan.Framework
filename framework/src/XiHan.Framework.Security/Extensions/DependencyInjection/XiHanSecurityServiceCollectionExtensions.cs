#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanSecurityServiceCollectionExtensions
// Guid:a1b2c3d4-e5f6-7890-abcd-123456789033
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Security.Password;
using XiHan.Framework.Security.Services;

namespace XiHan.Framework.Security.Extensions.DependencyInjection;

/// <summary>
/// 曦寒安全服务集合扩展
/// </summary>
public static class XiHanSecurityServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒安全服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanSecurityServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // 注册密码策略服务（Scoped，因消费方可能需要 Scoped 仓储）
        services.TryAddScoped<IPasswordPolicyService, PasswordPolicyService>();

        // 注册密码历史记录存储（Scoped，默认内存实现，可被应用层替换为数据库实现）
        services.TryAddScoped<IPasswordHistoryStore, DefaultPasswordHistoryStore>();

        return services;
    }
}
