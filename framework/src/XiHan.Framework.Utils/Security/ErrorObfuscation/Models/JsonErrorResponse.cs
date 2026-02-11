#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonErrorResponse
// Guid:7c8d9e0f-2a3b-4c5d-6e7f-8a9b0c1d2e3f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Models;

/// <summary>
/// JSON 错误响应对象
/// </summary>
public class JsonErrorResponse
{
    /// <summary>
    /// HTTP 状态码
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// 错误类型
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 异常类型
    /// </summary>
    public string Exception { get; set; } = string.Empty;

    /// <summary>
    /// 时间戳（Unix 毫秒）
    /// </summary>
    public long Timestamp { get; set; }

    /// <summary>
    /// ISO 格式时间戳
    /// </summary>
    public string TimestampISO { get; set; } = string.Empty;

    /// <summary>
    /// 追踪ID
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// 请求ID
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// 请求路径
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// HTTP 方法
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// 编程语言
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// 服务器信息
    /// </summary>
    public string Server { get; set; } = string.Empty;

    /// <summary>
    /// 数据库信息
    /// </summary>
    public string Database { get; set; } = string.Empty;

    /// <summary>
    /// 堆栈跟踪
    /// </summary>
    public string StackTrace { get; set; } = string.Empty;

    /// <summary>
    /// 元数据
    /// </summary>
    public ErrorMetadata? Metadata { get; set; }
}

/// <summary>
/// 错误元数据
/// </summary>
public class ErrorMetadata
{
    /// <summary>
    /// 主机名
    /// </summary>
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    /// 进程ID
    /// </summary>
    public int Pid { get; set; }

    /// <summary>
    /// 线程ID
    /// </summary>
    public int ThreadId { get; set; }

    /// <summary>
    /// 内存使用
    /// </summary>
    public string MemoryUsage { get; set; } = string.Empty;
}
