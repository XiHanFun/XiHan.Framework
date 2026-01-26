#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ProxyPoolOptions
// Guid:a6f8h0c2-bd3e-7f9g-ch5i-4d6e7f8g9h0b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/20 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Http.Configuration;
using XiHan.Framework.Http.Enums;

namespace XiHan.Framework.Http.Options;

/// <summary>
/// 代理池配置选项
/// </summary>
public class XiHanProxyPoolOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Http:ProxyPool";

    /// <summary>
    /// 是否启用代理池
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 代理选择策略
    /// </summary>
    public ProxySelectionStrategy SelectionStrategy { get; set; } = ProxySelectionStrategy.RoundRobin;

    /// <summary>
    /// 代理列表
    /// </summary>
    public List<ProxyConfiguration> Proxies { get; set; } = [];

    /// <summary>
    /// 是否启用代理健康检查
    /// </summary>
    public bool EnableHealthCheck { get; set; } = true;

    /// <summary>
    /// 健康检查间隔(秒)
    /// </summary>
    [Range(10, 3600)]
    public int HealthCheckIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// 健康检查超时(秒)
    /// </summary>
    [Range(1, 60)]
    public int HealthCheckTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// 健康检查URL
    /// </summary>
    public string HealthCheckUrl { get; set; } = "https://www.google.com";

    /// <summary>
    /// 失败重试次数
    /// </summary>
    [Range(0, 10)]
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// 失败阈值(达到此数值后将代理标记为不可用)
    /// </summary>
    [Range(1, 100)]
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// 恢复时间(秒，不可用的代理在此时间后会重新尝试)
    /// </summary>
    [Range(60, 7200)]
    public int RecoveryTimeSeconds { get; set; } = 300;

    /// <summary>
    /// 是否在启动时验证所有代理
    /// </summary>
    public bool ValidateOnStartup { get; set; } = true;

    /// <summary>
    /// 是否自动移除失败的代理
    /// </summary>
    public bool AutoRemoveFailedProxy { get; set; } = false;

    /// <summary>
    /// 代理统计信息保留时间(秒)
    /// </summary>
    [Range(3600, 604800)]
    public int StatisticsRetentionSeconds { get; set; } = 86400; // 24小时
}
