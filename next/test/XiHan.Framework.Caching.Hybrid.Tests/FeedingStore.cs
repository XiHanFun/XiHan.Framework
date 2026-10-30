// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Hybrid.Tests;

/// <summary>
/// 会发变更通知的字节存储替身。
/// </summary>
/// <remarks>
/// 读写交给内存存储；<see cref="Raise"/> 模拟别的实例经后端转发来的变更。
/// </remarks>
internal sealed class FeedingStore(TimeProvider clock) : ICacheStore, ICacheChangeFeed
{
    /// <summary>
    /// 真正保存数据的内存存储。
    /// </summary>
    private readonly InMemoryCacheStore _inner = new(new InMemoryCachingOptions(), clock);

    /// <summary>
    /// 当前订阅者。
    /// </summary>
    private readonly List<Action<CacheChange>> _handlers = [];

    /// <summary>
    /// 订阅者数。
    /// </summary>
    public int SubscriberCount => _handlers.Count;

    /// <summary>
    /// 把一次变更交给全部订阅者。
    /// </summary>
    public void Raise(CacheChange change)
    {
        foreach (var handler in _handlers.ToList())
        {
            handler(change);
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(Action<CacheChange> handler)
    {
        _handlers.Add(handler);
        return new Unsubscriber(() => _handlers.Remove(handler));
    }

    /// <inheritdoc />
    public ValueTask<Maybe<ReadOnlyMemory<byte>>> GetAsync(CacheKey key, CancellationToken cancellationToken) => _inner.GetAsync(key, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Maybe<ReadOnlyMemory<byte>>>> GetManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken) => _inner.GetManyAsync(keys, cancellationToken);

    /// <inheritdoc />
    public ValueTask SetAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken) => _inner.SetAsync(key, value, options, cancellationToken);

    /// <inheritdoc />
    public ValueTask SetManyAsync(IReadOnlyList<CacheEntry> entries, CancellationToken cancellationToken) => _inner.SetManyAsync(entries, cancellationToken);

    /// <inheritdoc />
    public ValueTask<bool> TryAddAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken) => _inner.TryAddAsync(key, value, options, cancellationToken);

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken) => _inner.RemoveAsync(key, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<bool>> RemoveManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken) => _inner.RemoveManyAsync(keys, cancellationToken);

    /// <inheritdoc />
    public ValueTask<bool> TryRemoveAsync(CacheKey key, ReadOnlyMemory<byte> expectedValue, CancellationToken cancellationToken) => _inner.TryRemoveAsync(key, expectedValue, cancellationToken);
}
