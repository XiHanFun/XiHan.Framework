// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Stores;

/// <summary>
/// 存储的一次变更。
/// </summary>
/// <remarks>
/// 写入或删除了这些键，或按前缀删除了一批键（此时 <see cref="Keys"/> 为空）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record CacheChange
{
    /// <summary>
    /// 创建。
    /// </summary>
    private CacheChange(IReadOnlyList<CacheKey> keys, string? prefix)
    {
        Keys = keys;
        Prefix = prefix;
    }

    /// <summary>
    /// 写入或删除的键。
    /// </summary>
    public IReadOnlyList<CacheKey> Keys { get; }

    /// <summary>
    /// 按前缀删除时的前缀。
    /// </summary>
    public string? Prefix { get; }

    /// <summary>
    /// 这些键变了。
    /// </summary>
    public static CacheChange ForKeys(IReadOnlyList<CacheKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return new CacheChange([.. keys], null);
    }

    /// <summary>
    /// 前缀下的键被删除了。
    /// </summary>
    public static CacheChange ForPrefix(string prefix)
        => !string.IsNullOrEmpty(prefix) && CacheKey.IsValidPrefix(prefix) ? new CacheChange([], prefix) : throw new ArgumentException($"Cache key prefix '{prefix}' is invalid or empty.", nameof(prefix));
}
