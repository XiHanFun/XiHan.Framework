#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanLoggingModule
// Guid:b8f3e4d2-9c71-4a8b-b5d6-3e2f1a8c9d4e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 11:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Logging.Extensions;
using XiHan.Framework.Logging.Options;

namespace XiHan.Framework.Logging;

/// <summary>
/// 曦寒框架日志模块
/// </summary>
public class XiHanLoggingModule : XiHanModule
{
    /// <summary>
    /// 服务配置前
    /// </summary>
    /// <param name="context"></param>
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // 配置日志选项
        services.Configure<XiHanLoggingOptions>(options =>
        {
            // 设置默认配置
            options.IsEnabled = true;
            options.MinimumLevel = LogLevel.Information;
        });
    }

    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // 添加 XiHan 日志服务
        services.AddXiHanLogging();

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
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var logger = context.ServiceProvider.GetRequiredService<ILogger<XiHanLoggingModule>>();
        logger.LogInformation("XiHan.Framework.Logging module initialized successfully");
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
