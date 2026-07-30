// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Threading.Extensions.DependencyInjection;

/// <summary>
/// 曦寒框架线程服务集合扩展
/// </summary>
public static class XiHanThreadingServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒线程基础服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanThreading(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ICancellationTokenProvider>(NullCancellationTokenProvider.Instance);
        services.AddSingleton(typeof(IAmbientScopeProvider<>), typeof(AmbientDataContextAmbientScopeProvider<>));

        return services;
    }
}
