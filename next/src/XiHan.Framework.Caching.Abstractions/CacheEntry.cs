// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions;

/// <summary>
/// 批量写入（<see cref="ICacheStore.SetManyAsync"/>）的一项：键、值与该项自己的过期选项。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record CacheEntry
{
    /// <summary>
    /// 创建条目；不给选项时用 Provider 的默认存活时间。
    /// </summary>
    public CacheEntry(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions? options = null)
    {
        Key = key;
        Value = value;
        Options = options ?? CacheEntryOptions.Default;
    }

    /// <summary>
    /// 键。
    /// </summary>
    public CacheKey Key { get; }

    /// <summary>
    /// 值；存储在写入时复制，之后调用方修改自己的缓冲区不影响缓存。
    /// </summary>
    public ReadOnlyMemory<byte> Value { get; }

    /// <summary>
    /// 过期选项。
    /// </summary>
    public CacheEntryOptions Options { get; }
}
