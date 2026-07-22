// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Logging;

/// <summary>
/// 日志配置选项
/// </summary>
public class LogOptions
{
    /// <summary>
    /// 最小日志级别，默认 Info
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    /// <summary>
    /// 是否启用控制台输出，默认启用
    /// </summary>
    public bool EnableConsoleOutput { get; set; } = true;

    /// <summary>
    /// 是否启用文件输出，默认不启用
    /// </summary>
    public bool EnableFileOutput { get; set; } = false;

    /// <summary>
    /// 是否显示日志头部信息，默认显示
    /// </summary>
    public bool DisplayHeader { get; set; } = true;

    /// <summary>
    /// 日志文件目录，默认为 logs 文件夹
    /// </summary>
    public string LogDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "logs");

    /// <summary>
    /// 最大文件大小（字节），默认 10MB
    /// </summary>
    public long MaxFileSize { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// 队列容量，默认 10000
    /// </summary>
    public int QueueCapacity { get; set; } = 10000;

    /// <summary>
    /// 批处理大小，默认 50
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// 是否启用异步写入，默认启用
    /// </summary>
    public bool EnableAsyncWrite { get; set; } = true;

    /// <summary>
    /// 是否启用统计功能，默认不启用
    /// </summary>
    public bool EnableStatistics { get; set; } = false;

    /// <summary>
    /// 日志轮转策略，默认按大小
    /// </summary>
    public LogRotationPolicy RotationPolicy { get; set; } = LogRotationPolicy.Size;

    /// <summary>
    /// 日志保留天数，默认 30 天（0 表示永久保留）
    /// </summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>
    /// 队列溢出策略，默认丢弃低级别日志
    /// </summary>
    public QueueOverflowPolicy OverflowPolicy { get; set; } = QueueOverflowPolicy.DropLowPriority;

    /// <summary>
    /// 日志格式，默认文本格式
    /// </summary>
    public LogFormat LogFormat { get; set; } = LogFormat.Text;
}
