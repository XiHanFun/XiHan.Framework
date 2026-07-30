// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
