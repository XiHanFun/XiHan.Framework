// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Caches;

/// <summary>
/// 类型化缓存。<see cref="Store"/> 暴露底层存储给需要原子语义的组件（锁、幂等）。
/// 批量方法与 <see cref="ICacheStore"/> 的批量方法语义一致：一次往返、结果与入参顺序一一对应、空集合不触后端。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface ICache
{
    /// <summary>
    /// 底层存储。
    /// </summary>
    ICacheStore Store { get; }

    /// <summary>
    /// 读取。
    /// </summary>
    ValueTask<Maybe<T>> GetAsync<T>(CacheKey key, CancellationToken cancellationToken);

    /// <summary>
    /// 批量读取；结果与 <paramref name="keys"/> 顺序一一对应。
    /// </summary>
    ValueTask<IReadOnlyList<Maybe<T>>> GetManyAsync<T>(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken);

    /// <summary>
    /// 写入或覆盖；值不能为 <see langword="null"/>。
    /// </summary>
    ValueTask SetAsync<T>(CacheKey key, T value, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull;

    /// <summary>
    /// 批量写入或覆盖，全部条目用同一过期选项；值不能为 <see langword="null"/>，同一键出现多次后者覆盖。
    /// </summary>
    ValueTask SetManyAsync<T>(IReadOnlyList<KeyValuePair<CacheKey, T>> entries, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull;

    /// <summary>
    /// 删除；存在返回 <see langword="true"/>。
    /// </summary>
    ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken);

    /// <summary>
    /// 批量删除；结果与 <paramref name="keys"/> 顺序一一对应。
    /// </summary>
    ValueTask<IReadOnlyList<bool>> RemoveManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken);

    /// <summary>
    /// 命中返回，未命中则调用 <paramref name="factory"/> 并写入。不做并发合并：多个未命中调用者各自执行工厂。
    /// </summary>
    ValueTask<T> GetOrCreateAsync<T>(CacheKey key, Func<CancellationToken, ValueTask<T>> factory, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull;
}
