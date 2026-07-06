#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AiPipelineOptions
// Guid:a1b2c3d4-e5f6-4a23-9c23-0a0b0c0d0e23
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Abstractions.Configuration;

/// <summary>
/// AI 会话管道横切开关（护栏/遥测/缓存；绑定配置节 <c>XiHan:AI:Pipeline</c>）
/// </summary>
/// <remarks>
/// 全部默认关：护栏是有意开启的安全策略;缓存对高温创造性调用会重放同答(语义有风险);
/// 遥测在未接 OTel 导出器前为静默空操作。开启由部署方按需在 appsettings 打开。
/// </remarks>
public sealed class AiPipelineOptions
{
    /// <summary>
    /// 是否启用内容护栏（管道最外层，fail-closed 拦截）
    /// </summary>
    public bool EnableGuardrail { get; set; } = false;

    /// <summary>
    /// 是否启用 OpenTelemetry 遥测（须另接 TracerProvider/MeterProvider + 导出器方可收集）
    /// </summary>
    public bool EnableTelemetry { get; set; } = false;

    /// <summary>
    /// 遥测是否记录敏感数据（prompt/响应原文；默认关）
    /// </summary>
    public bool EnableSensitiveTelemetry { get; set; } = false;

    /// <summary>
    /// 遥测 ActivitySource/Meter 名
    /// </summary>
    public string TelemetrySourceName { get; set; } = "XiHan.AI";

    /// <summary>
    /// 是否启用响应分布式缓存（相同请求命中同答，慎用于创造性/高温调用）
    /// </summary>
    public bool EnableResponseCache { get; set; } = false;
}
