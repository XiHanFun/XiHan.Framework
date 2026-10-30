// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Observability.Abstractions;

/// <summary>
/// 物理内存。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record MemoryInfo
{
    /// <summary>
    /// 总字节数。
    /// </summary>
    public long? TotalBytes { get; init; }

    /// <summary>
    /// 可用字节数。
    /// </summary>
    /// <remarks>
    /// 不换出即可分配的量，含可回收的缓存。
    /// </remarks>
    public long? AvailableBytes { get; init; }

    /// <summary>
    /// 空闲字节数。
    /// </summary>
    /// <remarks>
    /// 完全未用的量，不含缓存。
    /// </remarks>
    public long? FreeBytes { get; init; }

    /// <summary>
    /// 文件缓存字节数。
    /// </summary>
    public long? CachedBytes { get; init; }

    /// <summary>
    /// 已用字节数。
    /// </summary>
    /// <remarks>
    /// 总量减可用。
    /// </remarks>
    public long? UsedBytes => TotalBytes - AvailableBytes;

    /// <summary>
    /// 使用率（0–100）。
    /// </summary>
    public double? UsagePercent => TotalBytes is > 0 && UsedBytes is { } used ? used * 100d / TotalBytes.Value : null;
}
