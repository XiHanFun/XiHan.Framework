#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DomainService
// Guid:9371cd5b-c0c1-4907-af52-3c9ec9e371c6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/2 5:54:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.MultiTenancy;

namespace XiHan.Framework.Ddd.Domain.Services;

/// <summary>
/// 领域服务基类
/// </summary>
public abstract class DomainService : IDomainService
{
    /// <summary>
    /// 领域服务提供者
    /// </summary>
    public ITransientCachedServiceProvider TransientCachedServiceProvider { get; set; } = default!;

    //protected IClock Clock => TransientCachedServiceProvider.GetRequiredService<IClock>();

    /// <summary>
    /// 分布式ID生成器
    /// </summary>
    protected IDistributedIdGenerator DistributedIdGenerator => TransientCachedServiceProvider.GetService<IDistributedIdGenerator>(provider => IdGeneratorFactory.CreateSnowflakeIdGenerator_HighWorkload());

    /// <summary>
    /// 当前租户
    /// </summary>
    protected ICurrentTenant CurrentTenant => TransientCachedServiceProvider.GetRequiredService<ICurrentTenant>();

    //protected IAsyncQueryableExecuter AsyncExecuter => TransientCachedServiceProvider.GetRequiredService<IAsyncQueryableExecuter>();

    /// <summary>
    /// 日志工厂
    /// </summary>
    protected ILoggerFactory LoggerFactory => TransientCachedServiceProvider.GetRequiredService<ILoggerFactory>();

    /// <summary>
    /// 日志记录器
    /// </summary>
    protected ILogger Logger => TransientCachedServiceProvider.GetService<ILogger>(provider => LoggerFactory?.CreateLogger(GetType().FullName!) ?? NullLogger.Instance);
}
