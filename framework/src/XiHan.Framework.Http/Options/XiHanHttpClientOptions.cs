#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HttpClientOptions
// Guid:1c570b50-bc30-4159-a133-2acb1155f4fb
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/31 20:17:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Http.Configuration;

namespace XiHan.Framework.Http.Options;

/// <summary>
/// HTTP 客户端配置选项
/// </summary>
public class XiHanHttpClientOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Http";

    /// <summary>
    /// 默认超时时间(秒)
    /// </summary>
    [Range(1, 300)]
    public int DefaultTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 重试次数
    /// </summary>
    [Range(0, 10)]
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// 重试间隔(秒)
    /// </summary>
    public int[] RetryDelaySeconds { get; set; } = [1, 5, 10];

    /// <summary>
    /// 熔断器失败阈值
    /// </summary>
    [Range(1, 100)]
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// 熔断器采样持续时间(秒)
    /// </summary>
    [Range(10, 300)]
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 60;

    /// <summary>
    /// 熔断器最小吞吐量
    /// </summary>
    [Range(1, 1000)]
    public int CircuitBreakerMinimumThroughput { get; set; } = 10;

    /// <summary>
    /// 熔断器断开持续时间(秒)
    /// </summary>
    [Range(10, 600)]
    public int CircuitBreakerDurationOfBreakSeconds { get; set; } = 30;

    /// <summary>
    /// 是否启用请求日志
    /// </summary>
    public bool EnableRequestLogging { get; set; } = true;

    /// <summary>
    /// 是否启用响应日志
    /// </summary>
    public bool EnableResponseLogging { get; set; } = true;

    /// <summary>
    /// 是否记录敏感数据
    /// </summary>
    public bool LogSensitiveData { get; set; } = false;

    /// <summary>
    /// 最大响应内容长度(用于日志记录)
    /// </summary>
    [Range(1024, 1048576)]
    public int MaxResponseContentLength { get; set; } = 4096;

    /// <summary>
    /// 客户端生存期(分钟)
    /// </summary>
    [Range(1, 1440)]
    public int ClientLifetimeMinutes { get; set; } = 5;

    /// <summary>
    /// 是否忽略SSL证书错误
    /// </summary>
    public bool IgnoreSslErrors { get; set; } = false;

    /// <summary>
    /// 默认请求头
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; set; } = new()
    {
        ["Accept"] = "application/json",
        ["User-Agent"] = $"XiHan.Framework.Http/{XiHan.Version}"
    };

    /// <summary>
    /// 预定义的HTTP客户端配置
    /// </summary>
    public Dictionary<string, HttpClientConfiguration> Clients { get; set; } = [];
}
