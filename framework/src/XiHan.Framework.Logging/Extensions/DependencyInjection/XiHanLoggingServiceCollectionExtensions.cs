// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Configuration;
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
    /// 添加 XiHan 日志服务（从配置文件绑定选项）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanLogging(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<XiHanLoggingOptions>(configuration.GetSection(XiHanLoggingOptions.SectionName));
        return services.AddXiHanLogging(_ => { });
    }

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
        builder.Services.Configure(configure ?? (_ => { }));
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
        builder.Services.Configure(configure ?? (_ => { }));
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, XiHanConsoleLoggerProvider>());
        return builder;
    }

    /// <summary>
    /// 配置 Serilog 与 XiHan 集成
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    /// <remarks>
    /// 这里消费 XiHanLoggingOptions 中「当前真正生效」的那批键：MinimumLevel、两套输出模板、文件滚动/保留/大小，
    /// 以及文件 Sink 的异步三件套 EnableAsyncLogging / AsyncBufferSize / BlockWhenFull。
    /// 仍未参与管道构建的是 ContextProperties、Filters、EnableRequestLogging、RequestLoggingExcludePaths，
    /// 这是既定装配口径而非遗漏：docs/guide/logging.md 的「声明了但当前不生效的选项」表已逐条写明现状与替代做法
    /// （需要按分类分级或追加 Sink，请在应用侧自行装配 Serilog 管道）。改动那几项等于改变宿主的既有日志行为，
    /// 应先更新该文档再动实现。
    /// 另注：EnableStructuredLogging / EnablePerformanceCounters 并非死配置，它们由 XiHanLogger 读取。
    /// </remarks>
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
                .WriteTo.Console(outputTemplate: loggingOptions.ConsoleOutputTemplate);

            // 原来文件 Sink 恒定包一层 WriteTo.Async，且缓冲区大小与满队列策略全用 Serilog 的默认值，
            // 于是 EnableAsyncLogging / AsyncBufferSize / BlockWhenFull 三个选项在全仓没有任何读取点：
            // 宿主关掉异步、调小缓冲、要求队列满时阻塞而不是丢事件，配了都拿不到效果，是对宿主的空承诺。
            // 这里把三者接进管道：关掉异步时直接挂同步文件 Sink（写调用返回即落盘，适合要求日志与崩溃现场
            // 严格同序的宿主，代价是写入方要承担磁盘延迟）；开着时把两个参数透传给 WriteTo.Async。
            // 三个选项的默认值（true / 10000 / false）与 WriteTo.Async 的默认值一一对应，因此默认配置下的
            // 管道形状与修复前完全一致，只有显式改过这三个键的宿主才会看到行为变化。
            if (loggingOptions.EnableAsyncLogging)
            {
                configuration.WriteTo.Async(
                    sinkConfiguration => WriteToFile(sinkConfiguration, loggingOptions),
                    bufferSize: loggingOptions.AsyncBufferSize,
                    blockWhenFull: loggingOptions.BlockWhenFull);
            }
            else
            {
                WriteToFile(configuration.WriteTo, loggingOptions);
            }
        });

        return services;
    }

    /// <summary>
    /// 挂载文件接收器
    /// </summary>
    /// <param name="sinkConfiguration">接收器配置，可能是顶层配置，也可能是异步包装器内部的配置</param>
    /// <param name="loggingOptions">日志选项</param>
    /// <remarks>
    /// 同步与异步两条分支挂的必须是同一个文件 Sink，抽出来是为了避免两处参数列表各自漂移。
    /// </remarks>
    private static void WriteToFile(LoggerSinkConfiguration sinkConfiguration, XiHanLoggingOptions loggingOptions)
    {
        sinkConfiguration.File(
            loggingOptions.FileOutputPath,
            outputTemplate: loggingOptions.FileOutputTemplate,
            rollingInterval: loggingOptions.RollingInterval,
            retainedFileCountLimit: loggingOptions.RetainedFileCountLimit,
            fileSizeLimitBytes: loggingOptions.FileSizeLimitBytes,
            rollOnFileSizeLimit: loggingOptions.RollOnFileSizeLimit);
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
