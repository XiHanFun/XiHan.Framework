// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    public string FilePath { get; set; } = "Logs/xihan-.log";

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
    /// <remarks>
    /// 当前 XiHanFileLogger 并不读取该字段：每条日志都同步 File.AppendAllText，既无缓冲也无定时刷新。
    /// 这是 docs/guide/logging.md「声明了但当前不生效的选项」已明确写出的既定限制，不是实现遗漏；
    /// 该提供器定位为不走 Serilog 管道的宿主里的轻量输出，高吞吐场景请改用本模块的 Serilog 文件 Sink。
    /// </remarks>
    public int BufferSize { get; set; } = 1024;

    /// <summary>
    /// 刷新间隔
    /// </summary>
    /// <remarks>
    /// 同 <see cref="BufferSize"/>：当前实现每条日志直接落盘，没有需要定时刷新的缓冲区，该字段不生效。
    /// </remarks>
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
    /// <remarks>
    /// 同 <see cref="BufferSize"/>：当前实现恒为同步写入，该字段不生效，配 false 与 true 行为一致。
    /// </remarks>
    public bool EnableAsyncWrite { get; set; } = true;

    /// <summary>
    /// 编码格式
    /// </summary>
    public string Encoding { get; set; } = "UTF-8";
}
