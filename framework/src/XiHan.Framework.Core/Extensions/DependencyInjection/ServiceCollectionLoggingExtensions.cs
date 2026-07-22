// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Core.Logging;

namespace XiHan.Framework.Core.Extensions.DependencyInjection;

/// <summary>
/// 获取初始化日志
/// </summary>
public static class ServiceCollectionLoggingExtensions
{
    /// <summary>
    /// 获取初始化日志工厂
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static ILogger<T> GetInitLogger<T>(this IServiceCollection services)
    {
        return services.GetSingletonInstance<IInitLoggerFactory>().Create<T>();
    }
}
