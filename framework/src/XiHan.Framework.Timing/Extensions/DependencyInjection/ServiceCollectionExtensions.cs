#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ServiceCollectionExtensions
// Guid:18668e27-20cc-4d5c-b27b-0a13ac5708ce
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 11:10:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
