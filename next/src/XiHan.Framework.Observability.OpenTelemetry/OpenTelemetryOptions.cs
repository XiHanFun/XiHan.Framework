// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Observability.OpenTelemetry;

/// <summary>
/// OpenTelemetry 选项。从配置节 <c>XiHan:Observability:OpenTelemetry</c> 显式读取：<c>ServiceName</c>、<c>ServiceVersion</c>、<c>Tracing</c>、<c>Metrics</c>、<c>Logging</c>、<c>SamplingRatio</c>、
/// <c>Otlp:Enabled</c>、<c>Otlp:Endpoint</c>、<c>Otlp:Protocol</c>、<c>Console</c>、<c>Sources</c>（逗号分隔）、<c>Meters</c>（逗号分隔）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record OpenTelemetryOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Observability:OpenTelemetry";

    /// <summary>
    /// 资源属性 <c>service.name</c>；默认入口程序集名。
    /// </summary>
    public string ServiceName
    {
        get => field;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    } = Assembly.GetEntryAssembly()?.GetName().Name ?? "xihan";

    /// <summary>
    /// 资源属性 <c>service.version</c>；默认入口程序集版本。
    /// </summary>
    public string? ServiceVersion { get; init; } = Assembly.GetEntryAssembly()?.GetName().Version?.ToString();

    /// <summary>
    /// 是否导出追踪；默认开。
    /// </summary>
    public bool Tracing { get; init; } = true;

    /// <summary>
    /// 是否导出指标；默认开。
    /// </summary>
    public bool Metrics { get; init; } = true;

    /// <summary>
    /// 是否把 <c>Microsoft.Extensions.Logging</c> 的日志经 OpenTelemetry 导出；默认开。
    /// </summary>
    public bool Logging { get; init; } = true;

    /// <summary>
    /// 根 Span 的采样比例，0 到 1；有上游决定的沿用上游。默认 1。
    /// </summary>
    public double SamplingRatio
    {
        get => field;
        init => field = value is >= 0 and <= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The sampling ratio must be between 0 and 1.");
    } = 1;

    /// <summary>
    /// 是否启用 OTLP 导出器；<see langword="null"/>（默认）表示自动：配置了 <see cref="OtlpEndpoint"/> 或环境变量 <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> 时启用。
    /// </summary>
    public bool? Otlp { get; init; }

    /// <summary>
    /// OTLP 端点；为空时导出器按标准环境变量与默认端口决定。
    /// </summary>
    public Uri? OtlpEndpoint { get; init; }

    /// <summary>
    /// OTLP 协议；默认 gRPC。
    /// </summary>
    public OtlpProtocol OtlpProtocol { get; init; }

    /// <summary>
    /// 是否同时写到控制台导出器（开发用）；默认关。
    /// </summary>
    public bool Console { get; init; }

    /// <summary>
    /// 在默认之外额外订阅的 Activity 源。
    /// </summary>
    public IReadOnlyList<string> Sources { get; init; } = [];

    /// <summary>
    /// 在默认之外额外订阅的 Meter。
    /// </summary>
    public IReadOnlyList<string> Meters { get; init; } = [];

    /// <summary>
    /// 是否应启用 OTLP 导出器：显式设置优先，否则看端点配置与标准环境变量。
    /// </summary>
    public bool ExportsOtlp => Otlp ?? (OtlpEndpoint is not null || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OpenTelemetryDefaults.OtlpEndpointVariable)));

    /// <summary>
    /// 从配置读取。
    /// </summary>
    public static OpenTelemetryOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var options = new OpenTelemetryOptions();
        if (section["ServiceName"] is { } name)
        {
            options = options with { ServiceName = name };
        }

        if (section["ServiceVersion"] is { } version)
        {
            options = options with { ServiceVersion = version };
        }

        options = options with
        {
            Tracing = Flag(section, "Tracing", options.Tracing),
            Metrics = Flag(section, "Metrics", options.Metrics),
            Logging = Flag(section, "Logging", options.Logging),
            Console = Flag(section, "Console", options.Console),
        };
        if (section["SamplingRatio"] is { } ratio)
        {
            options = double.TryParse(ratio, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 0 and <= 1
                ? options with { SamplingRatio = parsed }
                : throw new ArgumentException($"{ConfigurationSection}:SamplingRatio must be a number between 0 and 1, but was '{ratio}'.");
        }

        if (section["Otlp:Enabled"] is { } enabled)
        {
            options = bool.TryParse(enabled, out var parsed)
                ? options with { Otlp = parsed }
                : throw new ArgumentException($"{ConfigurationSection}:Otlp:Enabled must be 'true' or 'false', but was '{enabled}'.");
        }

        if (section["Otlp:Endpoint"] is { } endpoint && !string.IsNullOrWhiteSpace(endpoint))
        {
            options = Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
                ? options with { OtlpEndpoint = uri }
                : throw new ArgumentException($"{ConfigurationSection}:Otlp:Endpoint must be an absolute URI, but was '{endpoint}'.");
        }

        if (section["Otlp:Protocol"] is { } protocol)
        {
            options = Enum.TryParse<OtlpProtocol>(protocol, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                ? options with { OtlpProtocol = parsed }
                : throw new ArgumentException($"{ConfigurationSection}:Otlp:Protocol must be 'Grpc' or 'HttpProtobuf', but was '{protocol}'.");
        }

        return options with { Sources = List(section, "Sources"), Meters = List(section, "Meters") };
    }

    /// <summary>
    /// 读布尔开关；缺失用默认值。
    /// </summary>
    private static bool Flag(IConfiguration section, string key, bool fallback)
    {
        var text = section[key];
        if (text is null)
        {
            return fallback;
        }

        return bool.TryParse(text, out var parsed) ? parsed : throw new ArgumentException($"{ConfigurationSection}:{key} must be 'true' or 'false', but was '{text}'.");
    }

    /// <summary>
    /// 读逗号分隔的名字列表。
    /// </summary>
    private static string[] List(IConfiguration section, string key)
        => section[key] is { } text ? text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [];
}
