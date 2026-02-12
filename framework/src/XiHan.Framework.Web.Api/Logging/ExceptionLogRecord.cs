#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExceptionLogRecord
// Guid:4d5b5c12-f605-404b-8044-0e19d5f4b17e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 16:22:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Logging;

/// <summary>
/// 异常日志记录模型
/// </summary>
public class ExceptionLogRecord
{
    /// <summary>
    /// 跟踪标识
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// 用户标识
    /// </summary>
    public long? UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 请求路径
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// 请求方法
    /// </summary>
    public string? Method { get; set; }

    /// <summary>
    /// 控制器
    /// </summary>
    public string? ControllerName { get; set; }

    /// <summary>
    /// 动作
    /// </summary>
    public string? ActionName { get; set; }

    /// <summary>
    /// 状态码
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// 异常类型
    /// </summary>
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>
    /// 异常消息
    /// </summary>
    public string ExceptionMessage { get; set; } = string.Empty;

    /// <summary>
    /// 异常堆栈
    /// </summary>
    public string? ExceptionStackTrace { get; set; }

    /// <summary>
    /// 请求头摘要（JSON）
    /// </summary>
    public string? RequestHeaders { get; set; }

    /// <summary>
    /// 请求参数摘要（JSON）
    /// </summary>
    public string? RequestParams { get; set; }

    /// <summary>
    /// 远端 IP
    /// </summary>
    public string? RemoteIp { get; set; }

    /// <summary>
    /// UserAgent
    /// </summary>
    public string? UserAgent { get; set; }
}
