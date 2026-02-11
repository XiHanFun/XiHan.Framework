#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ApplicationServiceBase
// Guid:de7e7b9e-2499-4632-9a80-b8741d65ab7d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Application.Contracts.Services;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.Application.Services;

/// <summary>
/// 应用服务基类
/// </summary>
public abstract class ApplicationServiceBase : IApplicationService, ITransientDependency
{
    /// <summary>
    /// 服务提供者
    /// </summary>
    public ICachedServiceProvider ServiceProvider { get; set; } = null!;

    /// <summary>
    /// 日志记录器
    /// </summary>
    protected ILogger Logger => LazyLogger.Value;

    /// <summary>
    /// 懒加载日志记录器
    /// </summary>
    private Lazy<ILogger> LazyLogger => new(() => ServiceProvider?.GetService<ILogger<ApplicationServiceBase>>() ?? NullLogger<ApplicationServiceBase>.Instance);
}
