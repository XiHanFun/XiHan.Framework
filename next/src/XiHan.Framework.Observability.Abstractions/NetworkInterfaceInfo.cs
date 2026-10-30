// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net.NetworkInformation;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Observability.Abstractions;

/// <summary>
/// 一块网卡、它的地址与流量。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record NetworkInterfaceInfo
{
    /// <summary>
    /// 网卡 ID。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 名称。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 描述。
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 类型。
    /// </summary>
    public required NetworkInterfaceType Type { get; init; }

    /// <summary>
    /// 状态。
    /// </summary>
    public required OperationalStatus Status { get; init; }

    /// <summary>
    /// 速率（bit/s）。
    /// </summary>
    public long? SpeedBitsPerSecond { get; init; }

    /// <summary>
    /// 物理地址。
    /// </summary>
    /// <remarks>
    /// 冒号分隔的大写十六进制，如 <c>00:1A:2B:3C:4D:5E</c>。
    /// </remarks>
    public string? PhysicalAddress { get; init; }

    /// <summary>
    /// IPv4 地址。
    /// </summary>
    /// <remarks>
    /// 读得到前缀长度时带上，如 <c>192.168.1.5/24</c>。
    /// </remarks>
    public IReadOnlyList<string> IPv4Addresses { get; init; } = [];

    /// <summary>
    /// IPv6 地址。
    /// </summary>
    /// <remarks>
    /// 读得到前缀长度时带上。
    /// </remarks>
    public IReadOnlyList<string> IPv6Addresses { get; init; } = [];

    /// <summary>
    /// 网关地址。
    /// </summary>
    public IReadOnlyList<string> Gateways { get; init; } = [];

    /// <summary>
    /// DNS 服务器地址。
    /// </summary>
    public IReadOnlyList<string> DnsServers { get; init; } = [];

    /// <summary>
    /// 累计接收字节数。
    /// </summary>
    public long? BytesReceived { get; init; }

    /// <summary>
    /// 累计发送字节数。
    /// </summary>
    public long? BytesSent { get; init; }

    /// <summary>
    /// 累计接收包数。
    /// </summary>
    public long? PacketsReceived { get; init; }

    /// <summary>
    /// 累计发送包数。
    /// </summary>
    public long? PacketsSent { get; init; }

    /// <summary>
    /// 接收速率（字节/秒）。
    /// </summary>
    /// <remarks>
    /// 启动后第一个采样间隔内为空。
    /// </remarks>
    public double? ReceiveBytesPerSecond { get; init; }

    /// <summary>
    /// 发送速率（字节/秒）。
    /// </summary>
    /// <remarks>
    /// 启动后第一个采样间隔内为空。
    /// </remarks>
    public double? SendBytesPerSecond { get; init; }
}
