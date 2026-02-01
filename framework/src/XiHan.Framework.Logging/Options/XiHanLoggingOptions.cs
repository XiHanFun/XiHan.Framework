#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanLoggingOptions
// Guid:c9d4e5f6-8a7b-4c9d-a6e3-4f5a8b9c7e1d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 11:05:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Serilog;

namespace XiHan.Framework.Logging.Options;

/// <summary>
/// 曦寒日志配置选项
/// </summary>
public class XiHanLoggingOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Logging";

    /// <summary>
    /// 是否启用日志
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 最小日志级别
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// 控制台输出模板
    /// </summary>
    public string ConsoleOutputTemplate { get; set; } = "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// 文件输出路径
    /// </summary>
    public string FileOutputPath { get; set; } = "logs/xihan-.log";

    /// <summary>
    /// 文件输出模板
    /// </summary>
    public string FileOutputTemplate { get; set; } = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// 日志文件滚动间隔
    /// </summary>
    public RollingInterval RollingInterval { get; set; } = RollingInterval.Day;

    /// <summary>
    /// 保留文件数量限制
    /// </summary>
    public int? RetainedFileCountLimit { get; set; } = 31;

    /// <summary>
    /// 文件大小限制（字节）
    /// </summary>
    public long? FileSizeLimitBytes { get; set; } = 1024 * 1024 * 100; // 100MB

    /// <summary>
    /// 当文件大小达到限制时是否滚动
    /// </summary>
    public bool RollOnFileSizeLimit { get; set; } = true;

    /// <summary>
    /// 是否启用结构化日志
    /// </summary>
    public bool EnableStructuredLogging { get; set; } = true;

    /// <summary>
    /// 是否启用异步日志
    /// </summary>
    public bool EnableAsyncLogging { get; set; } = true;

    /// <summary>
    /// 异步日志缓冲区大小
    /// </summary>
    public int AsyncBufferSize { get; set; } = 10000;

    /// <summary>
    /// 是否在程序关闭时阻塞以确保日志写入完成
    /// </summary>
    public bool BlockWhenFull { get; set; } = false;

    /// <summary>
    /// 日志上下文属性
    /// </summary>
    public Dictionary<string, object> ContextProperties { get; set; } = [];

    /// <summary>
    /// 是否启用性能计数器
    /// </summary>
    public bool EnablePerformanceCounters { get; set; } = false;

    /// <summary>
    /// 是否启用请求日志
    /// </summary>
    public bool EnableRequestLogging { get; set; } = true;

    /// <summary>
    /// 请求日志排除路径
    /// </summary>
    public string[] RequestLoggingExcludePaths { get; set; } =
    [
        "/health",
        "/metrics",
        "/favicon.ico",
        "/swagger"
    ];

    /// <summary>
    /// 日志过滤器配置
    /// </summary>
    public Dictionary<string, LogLevel> Filters { get; set; } = [];
}
