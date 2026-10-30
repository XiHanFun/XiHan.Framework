// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Observability.Abstractions;

/// <summary>
/// 主板、整机与 BIOS。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record BoardInfo
{
    /// <summary>
    /// 主板厂商。
    /// </summary>
    public string? Manufacturer { get; init; }

    /// <summary>
    /// 主板型号。
    /// </summary>
    public string? Product { get; init; }

    /// <summary>
    /// 主板版本。
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// 主板序列号。
    /// </summary>
    /// <remarks>
    /// Linux 只有 root 读得到，Windows 不提供（RFC-0023 的已知差异）。
    /// </remarks>
    public string? SerialNumber { get; init; }

    /// <summary>
    /// 整机厂商。
    /// </summary>
    public string? SystemManufacturer { get; init; }

    /// <summary>
    /// 整机型号。
    /// </summary>
    public string? SystemProduct { get; init; }

    /// <summary>
    /// BIOS 厂商。
    /// </summary>
    public string? BiosVendor { get; init; }

    /// <summary>
    /// BIOS 版本。
    /// </summary>
    public string? BiosVersion { get; init; }

    /// <summary>
    /// BIOS 日期。
    /// </summary>
    /// <remarks>
    /// 固件给出的原文，如 <c>08/13/2024</c>。
    /// </remarks>
    public string? BiosDate { get; init; }
}
