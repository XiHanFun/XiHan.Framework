// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Stores;

/// <summary>
/// 字节级缓存存储，Provider 实现它。基础设施失败抛出携带 <see cref="CachingErrors.StoreUnavailable"/> 的 <see cref="FrameworkException"/>。
/// 返回的字节不可修改。批量方法按 <c>spec/conventions/batch-first.md</c>：一次往返、结果与入参顺序一一对应、空集合不触后端；
/// <see cref="TryAddAsync"/> 与 <see cref="TryRemoveAsync"/> 是锁与幂等的原子原语，保持单键。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface ICacheStore
{
    /// <summary>
    /// 读取；不存在或已过期为无值。滑动过期的条目在读取时延长。
    /// </summary>
    ValueTask<Maybe<ReadOnlyMemory<byte>>> GetAsync(CacheKey key, CancellationToken cancellationToken);

    /// <summary>
    /// 批量读取：逐键同 <see cref="GetAsync"/>，一次往返；结果与 <paramref name="keys"/> 顺序一一对应，命中的滑动过期条目逐个延长。
    /// </summary>
    ValueTask<IReadOnlyList<Maybe<ReadOnlyMemory<byte>>>> GetManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken);

    /// <summary>
    /// 写入或覆盖。
    /// </summary>
    ValueTask SetAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// 批量写入或覆盖：逐项同 <see cref="SetAsync"/>（每项自带过期选项），一次往返；同一键出现多次后者覆盖，任一项选项不合法时整批不写。
    /// </summary>
    ValueTask SetManyAsync(IReadOnlyList<CacheEntry> entries, CancellationToken cancellationToken);

    /// <summary>
    /// 不存在（或已过期）才写入；写入返回 <see langword="true"/>。原子。
    /// </summary>
    ValueTask<bool> TryAddAsync(CacheKey key, ReadOnlyMemory<byte> value, CacheEntryOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// 删除；存在返回 <see langword="true"/>。
    /// </summary>
    ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken);

    /// <summary>
    /// 批量删除：逐键同 <see cref="RemoveAsync"/>，一次往返；结果与 <paramref name="keys"/> 顺序一一对应，同一键出现多次第二次为 <see langword="false"/>。
    /// </summary>
    ValueTask<IReadOnlyList<bool>> RemoveManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken);

    /// <summary>
    /// 当前值等于 <paramref name="expectedValue"/> 时才删除；删除返回 <see langword="true"/>。原子。
    /// </summary>
    ValueTask<bool> TryRemoveAsync(CacheKey key, ReadOnlyMemory<byte> expectedValue, CancellationToken cancellationToken);
}
