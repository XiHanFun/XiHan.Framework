#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanGatewayOptions
// Guid:9a0b1c2d-3e4f-5a6b-7c8d-9e0f1a2b3c4d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/22 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Gateway.Options;

/// <summary>
/// 网关配置选项
/// </summary>
public class XiHanGatewayOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Web:Gateway";

    /// <summary>
    /// 是否启用灰度路由
    /// </summary>
    public bool EnableGrayRouting { get; set; } = true;

    /// <summary>
    /// 是否启用请求追踪
    /// </summary>
    public bool EnableRequestTracing { get; set; } = true;

    /// <summary>
    /// 是否启用限流
    /// </summary>
    public bool EnableRateLimiting { get; set; } = false;

    /// <summary>
    /// 是否启用熔断
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = false;

    /// <summary>
    /// 请求超时时间(秒)
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 允许的域名列表(CORS)
    /// </summary>
    public List<string> AllowedOrigins { get; set; } = [];

    /// <summary>
    /// 全局 Header
    /// </summary>
    public Dictionary<string, string> GlobalHeaders { get; set; } = new();
}
