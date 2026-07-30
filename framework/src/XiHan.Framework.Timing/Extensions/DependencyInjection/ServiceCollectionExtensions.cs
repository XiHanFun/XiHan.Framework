// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Timing.Extensions.DependencyInjection;

/// <summary>
/// 服务集合扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加 XiHan 时间服务
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddXiHanTiming(this IServiceCollection services)
    {
        services.AddOptions<XiHanClockOptions>();

        services.AddSingleton<IClock, Clock>();
        services.AddSingleton<ITimezoneProvider, TZConvertTimezoneProvider>();
        services.AddTransient<ICurrentTimezoneProvider, CurrentTimezoneProvider>();

        return services;
    }
}
