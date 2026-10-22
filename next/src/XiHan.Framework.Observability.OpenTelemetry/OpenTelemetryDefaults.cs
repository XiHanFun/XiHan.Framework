// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Observability.OpenTelemetry;

/// <summary>
/// 默认订阅的 Activity 源与 Meter：内核自己的，以及 .NET 运行时与 ASP.NET Core 内置的（无需检测包）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class OpenTelemetryDefaults
{
    /// <summary>
    /// 标准环境变量：设置了它就视为已配置 OTLP 端点。
    /// </summary>
    public const string OtlpEndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>
    /// 默认订阅的 Activity 源：内核、ASP.NET Core、HttpClient。
    /// </summary>
    public static IReadOnlyList<string> Sources { get; } = [FrameworkDiagnostics.SourceName, "Microsoft.AspNetCore", "System.Net.Http"];

    /// <summary>
    /// 默认订阅的 Meter：内核、ASP.NET Core 宿主与 Kestrel、HttpClient、运行时。
    /// </summary>
    public static IReadOnlyList<string> Meters { get; } = [FrameworkDiagnostics.SourceName, "Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel", "System.Net.Http", "System.Runtime"];
}
