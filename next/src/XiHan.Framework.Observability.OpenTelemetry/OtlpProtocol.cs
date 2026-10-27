// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Observability.OpenTelemetry;

/// <summary>
/// OTLP 的传输协议。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum OtlpProtocol
{
    /// <summary>
    /// gRPC（默认端口 4317）。
    /// </summary>
    Grpc = 0,

    /// <summary>
    /// HTTP + protobuf（默认端口 4318）。
    /// </summary>
    HttpProtobuf = 1,
}
