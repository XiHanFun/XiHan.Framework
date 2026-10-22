// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Observability.OpenTelemetry.Tests;

/// <summary>
/// OpenTelemetry 特性：内核的 Span 与指标被导出、资源属性、信号开关与选项解析。
/// </summary>
public sealed class OpenTelemetryObservabilityTests
{
    [Fact]
    public async Task Feature_ExportsKernelSpansAndMetrics_WithServiceResource()
    {
        var activities = new List<Activity>();
        var metrics = new List<Metric>();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<OpenTelemetryObservabilityFeature>(),
            [new("XiHan:Observability:OpenTelemetry:ServiceName", "orders-test"), new("XiHan:Observability:OpenTelemetry:ServiceVersion", "1.2.3")],
            services =>
            {
                services.ConfigureOpenTelemetryTracerProvider((_, tracing) => tracing.AddInMemoryExporter(activities));
                services.ConfigureOpenTelemetryMeterProvider((_, meters) => meters.AddInMemoryExporter(metrics));
            });
        Assert.Equal(["observability.export"], host.App.Graph["xihan.observability.opentelemetry"].SelectedCapabilities);

        // 提供者是惰性单例：先解析让监听器挂上
        var tracer = host.Services.GetRequiredService<TracerProvider>();
        var meter = host.Services.GetRequiredService<MeterProvider>();
        var attributes = tracer.GetResource().Attributes.ToDictionary(static a => a.Key, static a => a.Value);
        Assert.Equal("orders-test", attributes["service.name"]);
        Assert.Equal("1.2.3", attributes["service.version"]);

        // 结果由处理器写入：没有写结果的调用在指标与 Span 上标为 unset
        var outcome = await host.InvokeAsync(static context =>
        {
            context.SetOutcome(Result.Success());
            return ValueTask.CompletedTask;
        });
        Assert.True(outcome.IsSuccess);
        tracer.ForceFlush();
        var span = Assert.Single(activities, static a => a.OperationName == "xihan.invoke");
        Assert.Equal(FrameworkDiagnostics.SourceName, span.Source.Name);
        Assert.Equal("success", span.GetTagItem("xihan.outcome"));

        meter.ForceFlush();
        Assert.Contains(metrics, static m => m.Name == "xihan.invocations" && m.MeterName == FrameworkDiagnostics.SourceName);
        Assert.Contains(metrics, static m => m.Name == "xihan.invocation.duration");
    }

    [Fact]
    public async Task DisabledSignals_RegisterNoProviders()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<OpenTelemetryObservabilityFeature>(),
            [new("XiHan:Observability:OpenTelemetry:Tracing", "false"), new("XiHan:Observability:OpenTelemetry:Metrics", "false"), new("XiHan:Observability:OpenTelemetry:Logging", "false")]);
        Assert.Null(host.Services.GetService<TracerProvider>());
        Assert.Null(host.Services.GetService<MeterProvider>());
        Assert.Null(host.Services.GetService<LoggerProvider>());
    }

    [Fact]
    public void Options_ParseAndValidate_AndOtlpIsAutoDetected()
    {
        var options = OpenTelemetryOptions.FromConfiguration(Configuration(
            ("ServiceName", "svc"),
            ("SamplingRatio", "0.25"),
            ("Otlp:Endpoint", "http://collector:4318"),
            ("Otlp:Protocol", "httpprotobuf"),
            ("Console", "true"),
            ("Sources", "A, B"),
            ("Meters", "M")));
        Assert.Equal(("svc", 0.25, OtlpProtocol.HttpProtobuf, true), (options.ServiceName, options.SamplingRatio, options.OtlpProtocol, options.Console));
        Assert.Equal(new Uri("http://collector:4318"), options.OtlpEndpoint);
        Assert.Equal(["A", "B"], options.Sources);
        Assert.Equal(["M"], options.Meters);
        Assert.True(options.ExportsOtlp);

        var defaults = OpenTelemetryOptions.FromConfiguration(Configuration());
        Assert.True(defaults.Tracing && defaults.Metrics && defaults.Logging);
        Assert.Equal(1, defaults.SamplingRatio);
        Assert.False(defaults.Console);
        Assert.Null(defaults.Otlp);
        Assert.False(defaults.ExportsOtlp || Environment.GetEnvironmentVariable(OpenTelemetryDefaults.OtlpEndpointVariable) is not null);
        Assert.True(OpenTelemetryOptions.FromConfiguration(Configuration(("Otlp:Enabled", "true"))).ExportsOtlp);
        Assert.False(OpenTelemetryOptions.FromConfiguration(Configuration(("Otlp:Enabled", "false"), ("Otlp:Endpoint", "http://c:4317"))).ExportsOtlp);

        Assert.Throws<ArgumentException>(static () => OpenTelemetryOptions.FromConfiguration(Configuration(("SamplingRatio", "2"))));
        Assert.Throws<ArgumentException>(static () => OpenTelemetryOptions.FromConfiguration(Configuration(("Otlp:Endpoint", "not a uri"))));
        Assert.Throws<ArgumentException>(static () => OpenTelemetryOptions.FromConfiguration(Configuration(("Otlp:Protocol", "udp"))));
        Assert.Throws<ArgumentException>(static () => OpenTelemetryOptions.FromConfiguration(Configuration(("Tracing", "maybe"))));
        Assert.Throws<ArgumentOutOfRangeException>(static () => new OpenTelemetryOptions { SamplingRatio = -0.1 });
        Assert.Throws<ArgumentException>(static () => new OpenTelemetryOptions { ServiceName = " " });
        Assert.Contains(FrameworkDiagnostics.SourceName, OpenTelemetryDefaults.Sources);
        Assert.Contains("System.Runtime", OpenTelemetryDefaults.Meters);
    }

    /// <summary>
    /// 组装 <c>XiHan:Observability:OpenTelemetry</c> 节下的配置。
    /// </summary>
    private static IConfiguration Configuration(params (string Key, string Value)[] settings)
        => new ConfigurationBuilder().AddInMemoryCollection(settings.ToDictionary(static s => $"{OpenTelemetryOptions.ConfigurationSection}:{s.Key}", static s => (string?)s.Value)).Build();
}
