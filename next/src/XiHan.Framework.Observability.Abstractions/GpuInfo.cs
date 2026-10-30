// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Observability.Abstractions;

/// <summary>
/// 一块显卡。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record GpuInfo
{
    /// <summary>
    /// 名称。
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 厂商名。
    /// </summary>
    public string? Vendor { get; init; }

    /// <summary>
    /// PCI 厂商 ID。
    /// </summary>
    /// <remarks>
    /// 四位小写十六进制，如 <c>10de</c>。
    /// </remarks>
    public string? VendorId { get; init; }

    /// <summary>
    /// PCI 设备 ID。
    /// </summary>
    /// <remarks>
    /// 四位小写十六进制。
    /// </remarks>
    public string? DeviceId { get; init; }

    /// <summary>
    /// 驱动名。
    /// </summary>
    public string? Driver { get; init; }

    /// <summary>
    /// 驱动版本。
    /// </summary>
    public string? DriverVersion { get; init; }

    /// <summary>
    /// 显存字节数。
    /// </summary>
    public long? MemoryBytes { get; init; }

    /// <summary>
    /// 温度（℃）。
    /// </summary>
    public double? TemperatureCelsius { get; init; }
}
