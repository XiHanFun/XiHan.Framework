// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Gateway.Models;

/// <summary>
/// 网关错误响应
/// </summary>
public class GatewayErrorResponse
{
    /// <summary>
    /// 追踪ID
    /// </summary>
    public string TraceId { get; set; } = null!;

    /// <summary>
    /// 错误代码
    /// </summary>
    public string ErrorCode { get; set; } = null!;

    /// <summary>
    /// 错误消息
    /// </summary>
    public string ErrorMessage { get; set; } = null!;

    /// <summary>
    /// 请求路径
    /// </summary>
    public string Path { get; set; } = null!;

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 详细信息
    /// </summary>
    public Dictionary<string, object>? Details { get; set; }
}
