// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Xml.Serialization;

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Models;

/// <summary>
/// XML 错误响应对象
/// </summary>
[XmlRoot("Error")]
public class XmlErrorResponse
{
    /// <summary>
    /// HTTP 状态码
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// 错误类型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 异常类型
    /// </summary>
    public string Exception { get; set; } = string.Empty;

    /// <summary>
    /// ISO 格式时间戳
    /// </summary>
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// 追踪ID
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// 编程语言
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// 服务器信息
    /// </summary>
    public string Server { get; set; } = string.Empty;

    /// <summary>
    /// 堆栈跟踪
    /// </summary>
    public string StackTrace { get; set; } = string.Empty;

    /// <summary>
    /// 元数据
    /// </summary>
    public XmlErrorMetadata? Metadata { get; set; }
}

/// <summary>
/// XML 错误元数据
/// </summary>
public class XmlErrorMetadata
{
    /// <summary>
    /// 主机名
    /// </summary>
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    /// 进程ID
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// 线程ID
    /// </summary>
    public int ThreadId { get; set; }
}
