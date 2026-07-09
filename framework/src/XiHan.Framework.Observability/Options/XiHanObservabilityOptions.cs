#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanObservabilityOptions
// Guid:197b81d2-2434-4a97-97b6-6fc307bce5c3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/09 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Observability.Options;

/// <summary>
/// 曦寒可观测性配置项（绑定配置节 <c>XiHan:Observability</c>）
/// </summary>
/// <remarks>
/// 默认 <see cref="Enabled"/>=false，保持模块「装配即孤儿、零运行时行为」；应用侧按需在 appsettings 打开并配置导出。
/// </remarks>
public class XiHanObservabilityOptions
{
    /// <summary>
    /// 配置节名
    /// </summary>
    public const string SectionName = "XiHan:Observability";

    /// <summary>
    /// OpenTelemetry 总开关；关闭则不装配 SDK，仅保留自研诊断/指标/性能与健康检查
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 服务名（写入 OTel Resource service.name）
    /// </summary>
    public string ServiceName { get; set; } = "XiHan.App";

    /// <summary>
    /// 服务版本（写入 OTel Resource service.version）
    /// </summary>
    public string? ServiceVersion { get; set; }

    /// <summary>
    /// 是否启用链路追踪（Tracing）
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// 是否启用指标（Metrics）
    /// </summary>
    public bool EnableMetrics { get; set; } = false;

    /// <summary>
    /// 是否启用日志导出（OTLP Logs）
    /// </summary>
    public bool EnableLogging { get; set; } = false;

    /// <summary>
    /// 采样率（0~1）；ParentBased(TraceIdRatioBased)。dev 建议 1，prod 按需下调
    /// </summary>
    public double SamplingRatio { get; set; } = 1.0;

    /// <summary>
    /// OTLP 导出端点（如 http://localhost:4317）；为空则不启用 OTLP 导出
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// 是否输出到控制台导出器（dev 友好；prod 关闭）
    /// </summary>
    public bool ConsoleExporter { get; set; } = false;

    /// <summary>
    /// 额外要监听的 ActivitySource 名（应用自定义源）；框架内置源由 XiHanActivitySources.All 自动纳入
    /// </summary>
    public List<string> AdditionalSources { get; set; } = [];
}
