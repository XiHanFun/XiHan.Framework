#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanConsoleLoggerOptions
// Guid:23b71156-4cbf-4155-8b48-be493f9e1ee4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 11:50:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Logging.Options;

/// <summary>
/// XiHan 控制台日志提供器选项
/// </summary>
public class XiHanConsoleLoggerOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Logging:Console";

    /// <summary>
    /// 最小日志级别
    /// </summary>
    public LogLevel MinLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// 是否包含作用域
    /// </summary>
    public bool IncludeScopes { get; set; } = true;

    /// <summary>
    /// 是否启用彩色输出
    /// </summary>
    public bool EnableColors { get; set; } = true;

    /// <summary>
    /// 是否启用彩虹渐变输出
    /// </summary>
    public bool EnableRainbow { get; set; } = false;

    /// <summary>
    /// 日志格式
    /// </summary>
    public string LogFormat { get; set; } = "[{Timestamp:HH:mm:ss}] [{Level}] {Category}: {Message}{Exception}";

    /// <summary>
    /// 时间戳格式
    /// </summary>
    public string TimestampFormat { get; set; } = "HH:mm:ss";

    /// <summary>
    /// 是否显示类别名称
    /// </summary>
    public bool ShowCategoryName { get; set; } = true;

    /// <summary>
    /// 是否显示时间戳
    /// </summary>
    public bool ShowTimestamp { get; set; } = true;

    /// <summary>
    /// 是否显示日志级别
    /// </summary>
    public bool ShowLogLevel { get; set; } = true;

    /// <summary>
    /// 日志级别颜色映射
    /// </summary>
    public Dictionary<LogLevel, ConsoleColor> LogLevelColors { get; set; } = new()
    {
        [LogLevel.Trace] = ConsoleColor.Gray,
        [LogLevel.Debug] = ConsoleColor.DarkGray,
        [LogLevel.Information] = ConsoleColor.Green,
        [LogLevel.Warning] = ConsoleColor.Yellow,
        [LogLevel.Error] = ConsoleColor.Red,
        [LogLevel.Critical] = ConsoleColor.DarkRed
    };

    /// <summary>
    /// 单行输出模式
    /// </summary>
    public bool SingleLine { get; set; } = false;

    /// <summary>
    /// 是否输出到标准错误流（仅错误级别）
    /// </summary>
    public bool UseStdErrorForErrors { get; set; } = false;
}
