// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Observability.OpenTelemetry;

/// <summary>
/// OpenTelemetry 的能力包入口：提供 <c>observability.export@1</c>。内核的 Activity 与 Meter 加上 .NET 与 ASP.NET Core 内置的源与仪表按选项接到追踪、指标与日志提供者；
/// 配置了端点或标准环境变量时启用 OTLP 导出器，<c>Console</c> 为真时同时写到控制台。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class OpenTelemetryObservabilityFeature : IFeature
{
    /// <inheritdoc />
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.observability.opentelemetry",
        Package = "XiHan.Framework.Observability.OpenTelemetry",
        Version = FrameworkInfo.Version,
        Primitives = 1,
        Stability = Stability.Preview,
        Kind = ManifestKind.Provider,
        Role = ManifestRole.Default,
        Provides = [new CapabilityDeclaration(ObservabilityCapabilities.Export, ObservabilityCapabilities.Version)],
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        Contributes = new Contributions { Config = new ConfigContribution("XiHan:Observability") },
        Entry = typeof(OpenTelemetryObservabilityFeature).FullName,
    };

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var options = OpenTelemetryOptions.FromConfiguration(context.Configuration);
        context.Services.AddSingleton(options);
        if (!context.Node.IsSelectedFor(ObservabilityCapabilities.Export))
        {
            return;
        }

        var builder = context.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(options.ServiceName, serviceVersion: options.ServiceVersion, serviceInstanceId: Environment.MachineName));
        if (options.Tracing)
        {
            builder.WithTracing(tracing =>
            {
                tracing.AddSource([.. OpenTelemetryDefaults.Sources, .. options.Sources]);
                // 有上游决定的沿用上游，根 Span 按比例采样
                tracing.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(options.SamplingRatio)));
                if (options.Console)
                {
                    tracing.AddConsoleExporter();
                }
            });
        }

        if (options.Metrics)
        {
            builder.WithMetrics(metrics =>
            {
                metrics.AddMeter([.. OpenTelemetryDefaults.Meters, .. options.Meters]);
                if (options.Console)
                {
                    metrics.AddConsoleExporter();
                }
            });
        }

        if (options.Logging)
        {
            builder.WithLogging(logging =>
            {
                if (options.Console)
                {
                    logging.AddConsoleExporter();
                }
            });
        }

        if (options.ExportsOtlp)
        {
            // 跨信号的 OTLP 导出器：端点为空时按标准环境变量与默认端口决定
            var protocol = options.OtlpProtocol == OtlpProtocol.HttpProtobuf ? OtlpExportProtocol.HttpProtobuf : OtlpExportProtocol.Grpc;
            if (options.OtlpEndpoint is { } endpoint)
            {
                builder.UseOtlpExporter(protocol, endpoint);
            }
            else
            {
                builder.UseOtlpExporter();
            }
        }
    }
}
