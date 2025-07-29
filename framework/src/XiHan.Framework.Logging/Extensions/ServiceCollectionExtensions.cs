#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ServiceCollectionExtensions
// Guid:d0e5f6a7-9b8c-4d0e-b7f4-5a6b9c8d0e2f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 11:10:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using XiHan.Framework.Logging.Options;
using XiHan.Framework.Logging.Providers;
using XiHan.Framework.Logging.Services;

namespace XiHan.Framework.Logging.Extensions;

/// <summary>
/// 服务集合扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加 XiHan 日志服务
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddXiHanLogging(this IServiceCollection services)
    {
        return services.AddXiHanLogging(_ => { });
    }

    /// <summary>
    /// 添加 XiHan 日志服务
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configureOptions"></param>
    /// <returns></returns>
    public static IServiceCollection AddXiHanLogging(this IServiceCollection services, Action<XiHanLoggingOptions> configureOptions)
    {
        // 配置选项
        services.Configure(configureOptions);

        // 注册日志服务
        services.TryAddSingleton<IXiHanLoggerFactory, XiHanLoggerFactory>();
        services.TryAddTransient<IXiHanLogger, XiHanLogger>();
        services.TryAddTransient(typeof(IXiHanLogger<>), typeof(XiHanLogger<>));

        // 注册结构化日志服务
        services.TryAddSingleton<IStructuredLogger, StructuredLogger>();

        // 注册性能日志服务
        services.TryAddSingleton<IPerformanceLogger, PerformanceLogger>();

        // 注册日志上下文服务
        services.TryAddScoped<ILogContext, LogContext>();

        return services;
    }

    /// <summary>
    /// 添加 XiHan 文件日志提供器
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static ILoggingBuilder AddXiHanFileLogger(this ILoggingBuilder builder, Action<XiHanFileLoggerOptions>? configure = null)
    {
        builder.Services.Configure<XiHanFileLoggerOptions>(configure ?? (_ => { }));
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, XiHanFileLoggerProvider>());
        return builder;
    }

    /// <summary>
    /// 添加 XiHan 控制台日志提供器
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static ILoggingBuilder AddXiHanConsoleLogger(this ILoggingBuilder builder, Action<XiHanConsoleLoggerOptions>? configure = null)
    {
        builder.Services.Configure<XiHanConsoleLoggerOptions>(configure ?? (_ => { }));
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, XiHanConsoleLoggerProvider>());
        return builder;
    }

    /// <summary>
    /// 配置 Serilog 与 XiHan 集成
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static IServiceCollection AddXiHanSerilog(this IServiceCollection services, Action<LoggerConfiguration>? configure = null)
    {
        services.AddSerilog((serviceProvider, configuration) =>
        {
            var options = serviceProvider.GetService<IOptions<XiHanLoggingOptions>>()?.Value
                         ?? new XiHanLoggingOptions();

            configuration
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "XiHanFramework");

            // 应用自定义配置
            configure?.Invoke(configuration);

            // 应用选项配置
            if (options.IsEnabled)
            {
                configuration
                    .WriteTo.Console(outputTemplate: options.ConsoleOutputTemplate)
                    .WriteTo.Async(a => a.File(
                        options.FileOutputPath,
                        outputTemplate: options.FileOutputTemplate,
                        rollingInterval: options.RollingInterval,
                        retainedFileCountLimit: options.RetainedFileCountLimit,
                        fileSizeLimitBytes: options.FileSizeLimitBytes,
                        rollOnFileSizeLimit: options.RollOnFileSizeLimit));
            }
        });

        return services;
    }
}
