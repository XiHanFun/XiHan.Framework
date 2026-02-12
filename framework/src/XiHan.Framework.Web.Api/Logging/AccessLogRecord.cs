#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AccessLogRecord
// Guid:9a824816-bcbf-4f59-8d0f-6f42be450ecb
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 16:20:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.Logging;

/// <summary>
/// 访问日志记录模型
/// </summary>
public class AccessLogRecord
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
    /// 会话标识
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// 请求方法
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// 请求路径
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 状态码
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// 远端 IP
    /// </summary>
    public string? RemoteIp { get; set; }

    /// <summary>
    /// UserAgent
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// 来源地址
    /// </summary>
    public string? Referer { get; set; }

    /// <summary>
    /// 耗时毫秒
    /// </summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}
