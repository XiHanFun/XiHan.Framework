#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GatewayErrorResponse
// Guid:6d7e8f9a-0b1c-2d3e-4f5a-6b7c8d9e0f1a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/22 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
