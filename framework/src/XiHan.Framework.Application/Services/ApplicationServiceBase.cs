// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
