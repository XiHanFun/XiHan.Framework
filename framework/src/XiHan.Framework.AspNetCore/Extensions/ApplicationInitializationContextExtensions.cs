#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ApplicationInitializationContextExtensions
// Guid:2a868cbd-ce74-4eb0-a130-097955dd029e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/10 6:48:07
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Application;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.AspNetCore.Extensions;

/// <summary>
/// 应用程序初始化上下文扩展
/// </summary>
public static class ApplicationInitializationContextExtensions
{
    /// <summary>
    /// 获取应用程序构建器
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static IApplicationBuilder GetApplicationBuilder(this ApplicationInitializationContext context)
    {
        var applicationBuilder = context.ServiceProvider.GetRequiredService<IObjectAccessor<IApplicationBuilder>>().Value;

        _ = CheckHelper.NotNull(applicationBuilder, nameof(applicationBuilder));

        return applicationBuilder!;
    }

    /// <summary>
    /// 获取应用程序构建器
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static IApplicationBuilder? GetApplicationBuilderOrNull(this ApplicationInitializationContext context)
    {
        return context.ServiceProvider.GetRequiredService<IObjectAccessor<IApplicationBuilder>>().Value;
    }

    /// <summary>
    /// 获取环境
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static IWebHostEnvironment GetEnvironment(this ApplicationInitializationContext context)
    {
        return context.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
    }

    /// <summary>
    /// 获取环境
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static IWebHostEnvironment? GetEnvironmentOrNull(this ApplicationInitializationContext context)
    {
        return context.ServiceProvider.GetService<IWebHostEnvironment>();
    }

    /// <summary>
    /// 获取配置
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static IConfiguration GetConfiguration(this ApplicationInitializationContext context)
    {
        return context.ServiceProvider.GetRequiredService<IConfiguration>();
    }

    /// <summary>
    /// 获取日志工厂
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static ILoggerFactory GetLoggerFactory(this ApplicationInitializationContext context)
    {
        return context.ServiceProvider.GetRequiredService<ILoggerFactory>();
    }
}
