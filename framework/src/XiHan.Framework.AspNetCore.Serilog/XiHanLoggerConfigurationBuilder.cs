#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanLoggerConfigurationBuilder
// Guid:8ae25bc5-7cbc-49e2-8260-5237832ac60a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/21 15:15:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Serilog;
using Serilog.Events;
using System.Text;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.AspNetCore.Serilog;

/// <summary>
/// 曦寒日志配置构建器
/// </summary>
public class XiHanLoggerConfigurationBuilder
{
    private readonly LoggerConfiguration _loggerConfiguration;

    private readonly string _infoTemplate;

    private readonly string _warnTemplate;

    private readonly string _errorTemplate;

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanLoggerConfigurationBuilder()
    {
        _loggerConfiguration = new LoggerConfiguration();
        _infoTemplate =
           @"Date：{Timestamp:yyyy-MM-dd HH:mm:ss.fff}{NewLine}Level：{Level}{NewLine}Message：{Message}{NewLine}================{NewLine}";

        _warnTemplate =
           @"Date：{Timestamp:yyyy-MM-dd HH:mm:ss.fff}{NewLine}Level：{Level}{NewLine}Source：{SourceContext}{NewLine}Message：{Message}{NewLine}================{NewLine}";

        _errorTemplate =
           @"Date：{Timestamp:yyyy-MM-dd HH:mm:ss.fff}{NewLine}Level：{Level}{NewLine}Source：{SourceContext}{NewLine}Message：{Message}{NewLine}Exception：{Exception}{NewLine}Properties：{Properties}{NewLine}================{NewLine}";
    }

    /// <summary>
    /// 设置最小记录级别
    /// </summary>
    /// <param name="level"></param>
    /// <returns></returns>
    public XiHanLoggerConfigurationBuilder MinimumLevel(LogEventLevel level)
    {
        _ = _loggerConfiguration.MinimumLevel.Is(level);
        return this;
    }

    /// <summary>
    /// 设置默认最小记录级别
    /// </summary>
    /// <returns></returns>
    public XiHanLoggerConfigurationBuilder MinimumLevelDefault()
    {
#if DEBUG
        // 最小记录级别
        _ = _loggerConfiguration.MinimumLevel.Debug();
#endif
        _ = _loggerConfiguration.MinimumLevel.Information();
        return this;
    }

    /// <summary>
    /// 重写日志记录级别
    /// </summary>
    /// <param name="source"></param>
    /// <param name="level"></param>
    /// <returns></returns>
    public XiHanLoggerConfigurationBuilder Override(string source, LogEventLevel level)
    {
        _ = _loggerConfiguration.MinimumLevel.Override(source, level);
        return this;
    }

    /// <summary>
    /// 默认重写日志记录级别
    /// </summary>
    /// <returns></returns>
    public XiHanLoggerConfigurationBuilder OverrideDefault()
    {
        _ = Override("Microsoft", LogEventLevel.Warning);
        _ = Override("System", LogEventLevel.Warning);
        return this;
    }

    /// <summary>
    /// 从日志上下文中获取附加信息
    /// </summary>
    /// <param name="propertyName"></param>
    /// <param name="value"></param>
    /// <param name="destructureObjects"></param>
    /// <returns></returns>
    public XiHanLoggerConfigurationBuilder EnrichWithProperty(string propertyName, object? value, bool destructureObjects = false)
    {
        _ = _loggerConfiguration.Enrich.WithProperty(propertyName, value, destructureObjects);
        return this;
    }

    /// <summary>
    /// 默认日志上下文中获取附加信息
    /// </summary>
    /// <returns></returns>
    public XiHanLoggerConfigurationBuilder EnrichWithPropertyDefault()
    {
        _ = EnrichWithProperty("Application", ReflectionHelper.GetEntryAssemblyName());
        _ = EnrichWithProperty("Version", ReflectionHelper.GetEntryAssemblyVersion());
        return this;
    }

    /// <summary>
    /// 从日志上下文中获取附加信息
    /// </summary>
    /// <returns></returns>
    public XiHanLoggerConfigurationBuilder EnrichFromLogContext()
    {
        _ = _loggerConfiguration.Enrich.FromLogContext();
        return this;
    }

    /// <summary>
    /// 默认从日志上下文中获取附加信息
    /// </summary>
    /// <returns></returns>
    public XiHanLoggerConfigurationBuilder EnrichFromLogContextDefault()
    {
        _ = EnrichFromLogContext();
        return this;
    }

    /// <summary>
    /// 添加控制台日志记录器
    /// </summary>
    /// <param name="level"></param>
    /// <param name="outputTemplate"></param>
    /// <returns></returns>
    public XiHanLoggerConfigurationBuilder WriteToConsole(LogEventLevel level, string outputTemplate)
    {
        _ = _loggerConfiguration.WriteTo.Logger(log => log.Filter.ByIncludingOnly(lev => lev.Level == level)
            .WriteTo.Console(outputTemplate: outputTemplate));
        return this;
    }

    /// <summary>
    /// 默认控制台日志记录器配置
    /// </summary>
    /// <returns></returns>
    public XiHanLoggerConfigurationBuilder WriteToConsoleDefault()
    {
#if DEBUG
        _ = WriteToConsole(LogEventLevel.Debug, _infoTemplate);
#endif
        _ = WriteToConsole(LogEventLevel.Information, _infoTemplate);
        _ = WriteToConsole(LogEventLevel.Warning, _warnTemplate);
        _ = WriteToConsole(LogEventLevel.Error, _errorTemplate);
        _ = WriteToConsole(LogEventLevel.Fatal, _errorTemplate);
        return this;
    }

    /// <summary>
    /// 添加文件日志记录器
    /// </summary>
    /// <param name="level"></param>
    /// <param name="path"></param>
    /// <param name="outputTemplate"></param>
    /// <returns></returns>
    public XiHanLoggerConfigurationBuilder WriteToFile(LogEventLevel level, string path, string outputTemplate)
    {
        _ = _loggerConfiguration.WriteTo.Logger(log => log.Filter.ByIncludingOnly(lev => lev.Level == level)
            // 异步输出到文件
            .WriteTo.Async(newConfig => newConfig.File(
                // 配置日志输出到文件，文件输出到当前项目的 logs 目录下，linux 中大写会出错
                path: Path.Combine(AppContext.BaseDirectory, path.ToLowerInvariant()),
                // 生成周期：天
                rollingInterval: RollingInterval.Day,
                // 文件大小：10M，默认1GB
                fileSizeLimitBytes: 1024 * 1024 * 10,
                // 保留最近：60个文件，默认31个，等于null时永远保留文件
                retainedFileCountLimit: 60,
                // 超过大小自动创建新文件
                rollOnFileSizeLimit: true,
                // 最小写入级别
                restrictedToMinimumLevel: level,
                // 写入模板
                outputTemplate: outputTemplate,
                // 编码
                encoding: Encoding.UTF8)));
        return this;
    }

    /// <summary>
    /// 默认文件日志记录器配置
    /// </summary>
    /// <returns></returns>
    public XiHanLoggerConfigurationBuilder WriteToFileDefault()
    {
        const string DebugPath = @"Logs/Debug/.log";
        const string InfoPath = @"Logs/Info/.log";
        const string WaringPath = @"Logs/Waring/.log";
        const string ErrorPath = @"Logs/Error/.log";
        const string FatalPath = @"Logs/Fatal/.log";

#if DEBUG
        _ = WriteToFile(LogEventLevel.Debug, DebugPath, _infoTemplate);
#endif
        _ = WriteToFile(LogEventLevel.Information, InfoPath, _infoTemplate);
        _ = WriteToFile(LogEventLevel.Warning, WaringPath, _warnTemplate);
        _ = WriteToFile(LogEventLevel.Error, ErrorPath, _errorTemplate);
        _ = WriteToFile(LogEventLevel.Fatal, FatalPath, _errorTemplate);

        return this;
    }

    /// <summary>
    /// 构建日志配置
    /// </summary>
    /// <returns></returns>
    public LoggerConfiguration Build(IConfiguration configuration)
    {
        return _loggerConfiguration.ReadFrom.Configuration(configuration);
    }

    /// <summary>
    /// 构建日志配置
    /// </summary>
    /// <returns></returns>
    public LoggerConfiguration Build()
    {
        return _loggerConfiguration;
    }

    /// <summary>
    /// 构建默认日志配置
    /// </summary>
    /// <returns></returns>
    public LoggerConfiguration BuildDefault()
    {
        return MinimumLevelDefault()
            .OverrideDefault()
            .EnrichFromLogContextDefault()
            .EnrichWithPropertyDefault()
            .WriteToConsoleDefault()
            .WriteToFileDefault()
            .Build();
    }
}
