// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions;

/// <summary>
/// 类型化缓存。<see cref="Store"/> 暴露底层存储给需要原子语义的组件（锁、幂等）。
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
    /// 写入或覆盖；值不能为 <see langword="null"/>。
    /// </summary>
    ValueTask SetAsync<T>(CacheKey key, T value, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull;

    /// <summary>
    /// 删除；存在返回 <see langword="true"/>。
    /// </summary>
    ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken);

    /// <summary>
    /// 命中返回，未命中则调用 <paramref name="factory"/> 并写入。不做并发合并：多个未命中调用者各自执行工厂。
    /// </summary>
    ValueTask<T> GetOrCreateAsync<T>(CacheKey key, Func<CancellationToken, ValueTask<T>> factory, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull;
}
