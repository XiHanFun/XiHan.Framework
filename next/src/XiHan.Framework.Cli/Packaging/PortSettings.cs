// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 协议端口。
/// </summary>
internal sealed record PortSettings
{
    /// <summary>
    /// HTTP 端口。
    /// </summary>
    public int Http { get; set; } = 8080;

    /// <summary>
    /// gRPC（h2c）端口；没有时不暴露。
    /// </summary>
    public int? Grpc { get; set; }
}
