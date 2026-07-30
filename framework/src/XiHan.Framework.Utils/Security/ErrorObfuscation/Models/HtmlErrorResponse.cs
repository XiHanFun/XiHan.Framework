// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Models;

/// <summary>
/// HTML 错误响应对象
/// </summary>
public class HtmlErrorResponse
{
    /// <summary>
    /// 错误类型
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>
    /// HTTP 状态码
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 异常类型
    /// </summary>
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>
    /// 编程语言
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// 时间戳
    /// </summary>
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// 追踪ID
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// 服务器信息
    /// </summary>
    public string Server { get; set; } = string.Empty;

    /// <summary>
    /// 主机名
    /// </summary>
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    /// 堆栈跟踪
    /// </summary>
    public string StackTrace { get; set; } = string.Empty;
}
