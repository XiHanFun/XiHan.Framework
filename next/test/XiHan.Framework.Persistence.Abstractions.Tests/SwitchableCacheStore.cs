// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// 包一层的缓存存储。
/// </summary>
/// <remarks>
/// <see cref="Refuse"/> 时"不存在才写"一律失败（模拟机器位全被占），<see cref="Fail"/> 时每个操作都抛存储不可用（模拟锁存储不可达）。
/// </remarks>
/// <param name="inner">真正的存储。</param>
internal sealed class SwitchableCacheStore(ICacheStore inner) : ICacheStore
{
    /// <summary>
    /// 为真时 <see cref="TryAddAsync"/> 总是返回 <see langword="false"/>。
    /// </summary>
    public bool Refuse { get; set; }

    /// <summary>
    /// 为真时每个操作都抛出携带 <see cref="CachingErrors.StoreUnavailable"/> 的异常。
    /// </summary>
    public bool Fail { get; set; }

    /// <inheritdoc />
    public ValueTask<Maybe<ReadOnlyMemory<byte>>> GetAsync(CacheKey key, CancellationToken cancellationToken)
    {
        Guard();
        return inner.GetAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Maybe<ReadOnlyMemory<byte>>>> GetManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        Guard();
        return inner.GetManyAsync(keys, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask SetAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken)
    {
        Guard();
        return inner.SetAsync(key, value, options, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask SetManyAsync(IReadOnlyList<CacheEntry> entries, CancellationToken cancellationToken)
    {
        Guard();
        return inner.SetManyAsync(entries, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<bool> TryAddAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken)
    {
        Guard();
        return Refuse ? ValueTask.FromResult(false) : inner.TryAddAsync(key, value, options, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken)
    {
        Guard();
        return inner.RemoveAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<bool>> RemoveManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken)
    {
        Guard();
        return inner.RemoveManyAsync(keys, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<bool> TryRemoveAsync(CacheKey key, ReadOnlyMemory<byte> expectedValue, CancellationToken cancellationToken)
    {
        Guard();
        return inner.TryRemoveAsync(key, expectedValue, cancellationToken);
    }

    /// <summary>
    /// <see cref="Fail"/> 时抛出存储不可用。
    /// </summary>
    private void Guard()
    {
        if (Fail)
        {
            throw new FrameworkException(CachingErrors.StoreUnavailable, "The lock store is unreachable.");
        }
    }
}
