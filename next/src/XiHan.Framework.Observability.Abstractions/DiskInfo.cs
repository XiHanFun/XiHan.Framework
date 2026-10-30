// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Observability.Abstractions;

/// <summary>
/// 一个磁盘或挂载点。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record DiskInfo
{
    /// <summary>
    /// 挂载点或盘符。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 设备。
    /// </summary>
    /// <remarks>
    /// 如 Linux 的 <c>/dev/sda1</c>。
    /// </remarks>
    public string? Device { get; init; }

    /// <summary>
    /// 卷标。
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// 文件系统。
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// 类型。
    /// </summary>
    public required DriveType Type { get; init; }

    /// <summary>
    /// 总字节数。
    /// </summary>
    public required long TotalBytes { get; init; }

    /// <summary>
    /// 空闲字节数。
    /// </summary>
    public required long FreeBytes { get; init; }

    /// <summary>
    /// 当前用户可用的字节数。
    /// </summary>
    /// <remarks>
    /// 受配额与保留空间影响，可能小于空闲字节数。
    /// </remarks>
    public required long AvailableBytes { get; init; }

    /// <summary>
    /// 已用字节数。
    /// </summary>
    public long UsedBytes => TotalBytes - FreeBytes;

    /// <summary>
    /// 使用率（0–100）。
    /// </summary>
    public double? UsagePercent => TotalBytes > 0 ? UsedBytes * 100d / TotalBytes : null;
}
