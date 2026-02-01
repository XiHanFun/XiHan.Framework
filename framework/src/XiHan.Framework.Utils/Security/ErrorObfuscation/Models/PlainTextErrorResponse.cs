#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PlainTextErrorResponse
// Guid:9e0f1a2b-4c5d-6e7f-8a9b-0c1d2e3f4a5b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Models;

/// <summary>
/// 纯文本错误响应对象
/// </summary>
public class PlainTextErrorResponse
{
    /// <summary>
    /// 时间
    /// </summary>
    public string Time { get; set; } = string.Empty;

    /// <summary>
    /// HTTP 状态码
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// 错误类型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 编程语言
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// 服务器信息
    /// </summary>
    public string Server { get; set; } = string.Empty;

    /// <summary>
    /// 追踪ID
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// 主机名
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 堆栈跟踪
    /// </summary>
    public string StackTrace { get; set; } = string.Empty;

    /// <summary>
    /// 转换为格式化文本
    /// </summary>
    public override string ToString()
    {
        var lines = new List<string>
        {
            "================================ ERROR REPORT ================================",
            $"Time:        {Time} UTC",
            $"Status:      HTTP {Status}",
            $"Type:        {Type}",
            $"Language:    {Language}",
            $"Server:      {Server}",
            $"TraceID:     {TraceId}",
            $"Host:        {Host}",
            "==============================================================================",
            "",
            "ERROR MESSAGE:",
            Message,
            "",
            "STACK TRACE:",
            StackTrace,
            "",
            "=============================================================================="
        };

        return string.Join(Environment.NewLine, lines);
    }
}
