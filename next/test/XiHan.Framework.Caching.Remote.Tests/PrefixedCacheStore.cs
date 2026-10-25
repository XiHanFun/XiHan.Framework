// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Remote.Tests;

/// <summary>
/// 给每个键加随机前缀的存储：各用例共用同一个插件进程，前缀让它们互不干扰。
/// </summary>
internal sealed class PrefixedCacheStore(ICacheStore inner) : ICacheStore
{
    /// <summary>
    /// 本实例的键前缀。
    /// </summary>
    private readonly string _prefix = Guid.NewGuid().ToString("N")[..8] + ":";

    /// <inheritdoc />
    public ValueTask<Maybe<ReadOnlyMemory<byte>>> GetAsync(CacheKey key, CancellationToken cancellationToken) => inner.GetAsync(Prefix(key), cancellationToken);

    /// <inheritdoc />
    public ValueTask SetAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken) => inner.SetAsync(Prefix(key), value, options, cancellationToken);

    /// <inheritdoc />
    public ValueTask<bool> TryAddAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken) => inner.TryAddAsync(Prefix(key), value, options, cancellationToken);

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken) => inner.RemoveAsync(Prefix(key), cancellationToken);

    /// <inheritdoc />
    public ValueTask<bool> TryRemoveAsync(CacheKey key, ReadOnlyMemory<byte> expectedValue, CancellationToken cancellationToken) => inner.TryRemoveAsync(Prefix(key), expectedValue, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Maybe<ReadOnlyMemory<byte>>>> GetManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken) => inner.GetManyAsync(Prefix(keys), cancellationToken);

    /// <inheritdoc />
    public ValueTask SetManyAsync(IReadOnlyList<CacheEntry> entries, CancellationToken cancellationToken)
        => inner.SetManyAsync([.. entries.Select(e => new CacheEntry(Prefix(e.Key), e.Value, e.Options))], cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<bool>> RemoveManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken) => inner.RemoveManyAsync(Prefix(keys), cancellationToken);

    /// <summary>
    /// 加前缀。
    /// </summary>
    private CacheKey Prefix(CacheKey key) => new(_prefix + key.Value);

    /// <summary>
    /// 给每个键加前缀。
    /// </summary>
    private CacheKey[] Prefix(IReadOnlyList<CacheKey> keys) => [.. keys.Select(Prefix)];
}
