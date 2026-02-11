#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanLoggingServiceCollectionExtensions
// Guid:5f94b247-a8e7-4b5e-9474-dec266a0e8be
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
using Serilog.Events;
using XiHan.Framework.Logging.Options;
using XiHan.Framework.Logging.Providers;
using XiHan.Framework.Logging.Services;

namespace XiHan.Framework.Logging.Extensions.DependencyInjection;

/// <summary>
/// 服务集合扩展方法
/// </summary>
public static class XiHanLoggingServiceCollectionExtensions
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

        services.AddXiHanSerilog();

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
    private static IServiceCollection AddXiHanSerilog(this IServiceCollection services, Action<LoggerConfiguration>? configure = null)
    {
        // 配置 Serilog
        services.AddSerilog((serviceProvider, configuration) =>
        {
            var loggingOptions = serviceProvider.GetRequiredService<IOptions<XiHanLoggingOptions>>().Value;

            configuration
                .MinimumLevel.Is(ConvertToSerilogLevel(loggingOptions.MinimumLevel))
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "XiHanFramework")
                .WriteTo.Console(outputTemplate: loggingOptions.ConsoleOutputTemplate)
                .WriteTo.Async(a => a.File(
                    loggingOptions.FileOutputPath,
                    outputTemplate: loggingOptions.FileOutputTemplate,
                    rollingInterval: loggingOptions.RollingInterval,
                    retainedFileCountLimit: loggingOptions.RetainedFileCountLimit,
                    fileSizeLimitBytes: loggingOptions.FileSizeLimitBytes,
                    rollOnFileSizeLimit: loggingOptions.RollOnFileSizeLimit));
        });

        return services;
    }

    /// <summary>
    /// 将 Microsoft.Extensions.Logging.LogLevel 转换为 Serilog.Events.LogEventLevel
    /// </summary>
    /// <param name="logLevel"></param>
    /// <returns></returns>
    private static LogEventLevel ConvertToSerilogLevel(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            LogLevel.None => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}
