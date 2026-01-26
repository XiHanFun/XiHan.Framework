#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanFileLoggerOptions
// Guid:e7f2a3b4-6c5d-4e7f-c4a1-2b3c4d5e7f9m
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 11:45:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Logging.Options;

/// <summary>
/// XiHan 文件日志提供器选项
/// </summary>
public class XiHanFileLoggerOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Logging:File";

    /// <summary>
    /// 文件路径
    /// </summary>
    public string FilePath { get; set; } = "logs/xihan-.log";

    /// <summary>
    /// 文件大小限制（字节）
    /// </summary>
    public long FileSizeLimit { get; set; } = 10 * 1024 * 1024; // 10MB

    /// <summary>
    /// 保留文件数量
    /// </summary>
    public int RetainedFileCountLimit { get; set; } = 31;

    /// <summary>
    /// 缓冲区大小
    /// </summary>
    public int BufferSize { get; set; } = 1024;

    /// <summary>
    /// 刷新间隔
    /// </summary>
    public TimeSpan FlushPeriod { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 最小日志级别
    /// </summary>
    public LogLevel MinLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// 是否包含作用域
    /// </summary>
    public bool IncludeScopes { get; set; } = true;

    /// <summary>
    /// 日志格式
    /// </summary>
    public string LogFormat { get; set; } = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] {Category}: {Message}{NewLine}{Exception}";

    /// <summary>
    /// 是否启用异步写入
    /// </summary>
    public bool EnableAsyncWrite { get; set; } = true;

    /// <summary>
    /// 编码格式
    /// </summary>
    public string Encoding { get; set; } = "UTF-8";
}
