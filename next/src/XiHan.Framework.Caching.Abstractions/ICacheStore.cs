// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions;

/// <summary>
/// 字节级缓存存储，Provider 实现它。基础设施失败抛出携带 <see cref="CachingErrors.StoreUnavailable"/> 的 <see cref="FrameworkException"/>。
/// 返回的字节不可修改。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface ICacheStore
{
    /// <summary>
    /// 读取；不存在或已过期为无值。滑动过期的条目在读取时延长。
    /// </summary>
    ValueTask<Maybe<ReadOnlyMemory<byte>>> GetAsync(CacheKey key, CancellationToken cancellationToken);

    /// <summary>
    /// 写入或覆盖。
    /// </summary>
    ValueTask SetAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// 不存在（或已过期）才写入；写入返回 <see langword="true"/>。原子。
    /// </summary>
    ValueTask<bool> TryAddAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// 删除；存在返回 <see langword="true"/>。
    /// </summary>
    ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken);

    /// <summary>
    /// 当前值等于 <paramref name="expectedValue"/> 时才删除；删除返回 <see langword="true"/>。原子。
    /// </summary>
    ValueTask<bool> TryRemoveAsync(CacheKey key, ReadOnlyMemory<byte> expectedValue, CancellationToken cancellationToken);
}
