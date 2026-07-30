// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace XiHan.Framework.Http.Configuration;

/// <summary>
/// HTTP 客户端配置
/// </summary>
public class HttpClientConfiguration
{
    /// <summary>
    /// 基础地址
    /// </summary>
    public string? BaseAddress { get; set; }

    /// <summary>
    /// 超时时间(秒)
    /// </summary>
    [Range(1, 300)]
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// 请求头
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = [];

    /// <summary>
    /// 是否启用重试
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    /// 是否启用熔断器
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>
    /// 是否忽略SSL证书错误
    /// </summary>
    public bool? IgnoreSslErrors { get; set; }
}
