// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Options;

namespace XiHan.Framework.Core.Extensions.DependencyInjection;

/// <summary>
/// 服务集合动态选项管理器扩展方法
/// </summary>
public static class ServiceCollectionDynamicOptionsManagerExtensions
{
    /// <summary>
    /// 添加动态选项
    /// </summary>
    /// <typeparam name="TOptions"></typeparam>
    /// <typeparam name="TManager"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddXiHanDynamicOptions<TOptions, TManager>(this IServiceCollection services)
        where TOptions : class
        where TManager : XiHanDynamicOptionsManager<TOptions>
    {
        services.Replace(ServiceDescriptor.Scoped<IOptions<TOptions>, TManager>());
        services.Replace(ServiceDescriptor.Scoped<IOptionsSnapshot<TOptions>, TManager>());

        return services;
    }
}
