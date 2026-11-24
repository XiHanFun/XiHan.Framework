// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Caches;

/// <summary>
/// 类型化缓存
/// </summary>
/// <remarks>
/// <see cref="Store"/> 暴露底层存储给需要原子语义的组件（锁、幂等）。批量方法与 <see cref="ICacheStore"/> 的批量方法语义一致：一次往返、结果与入参顺序一一对应、空集合不触后端
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface ICache
{
    /// <summary>
    /// 底层存储
    /// </summary>
    ICacheStore Store { get; }

    /// <summary>
    /// 读取
    /// </summary>
    ValueTask<Maybe<T>> GetAsync<T>(CacheKey key, CancellationToken cancellationToken);

    /// <summary>
    /// 批量读取
    /// </summary>
    /// <remarks>
    /// 结果与 <paramref name="keys"/> 顺序一一对应
    /// </remarks>
    ValueTask<IReadOnlyList<Maybe<T>>> GetManyAsync<T>(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken);

    /// <summary>
    /// 写入或覆盖
    /// </summary>
    /// <remarks>
    /// 值不能为 <see langword="null"/>
    /// </remarks>
    ValueTask SetAsync<T>(CacheKey key, T value, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull;

    /// <summary>
    /// 批量写入或覆盖，全部条目用同一过期选项
    /// </summary>
    /// <remarks>
    /// 值不能为 <see langword="null"/>，同一键出现多次后者覆盖
    /// </remarks>
    ValueTask SetManyAsync<T>(IReadOnlyList<KeyValuePair<CacheKey, T>> entries, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull;

    /// <summary>
    /// 删除
    /// </summary>
    /// <remarks>
    /// 存在返回 <see langword="true"/>
    /// </remarks>
    ValueTask<bool> RemoveAsync(CacheKey key, CancellationToken cancellationToken);

    /// <summary>
    /// 批量删除
    /// </summary>
    /// <remarks>
    /// 结果与 <paramref name="keys"/> 顺序一一对应
    /// </remarks>
    ValueTask<IReadOnlyList<bool>> RemoveManyAsync(IReadOnlyList<CacheKey> keys, CancellationToken cancellationToken);

    /// <summary>
    /// 命中返回，未命中则调用 <paramref name="factory"/> 并写入
    /// </summary>
    /// <remarks>
    /// 同一个缓存实例上同一个键、同一种结果类型的并发未命中由先到的调用回源，其余的等它的结果（ADR-0271）；跨实例、跨进程不合并，要跨实例只算一次用 <see cref="Locking.IDistributedLock"/>。
    /// <list type="bullet">
    /// <item>工厂在先到的那次调用里执行，收到的是它的 <paramref name="cancellationToken"/>。等待的调用取消只结束自己的等待；先到的调用取消时工厂随它取消，等待的调用里先到的一个接手、执行它自己的工厂</item>
    /// <item>工厂抛出（先到的调用没有取消）时，当时在等的调用都收到同一个异常；失败不缓存，下一次调用重新回源</item>
    /// <item>回源期间经本实例写入或删除这个键，这次回源就与它脱钩：已经在等的调用照常拿到结果，结果不再写回缓存，之后的调用重新读缓存</item>
    /// <item>被合并的调用拿到的是先到那次工厂的结果，过期选项也以它为准：同一个键必须对应同一份数据</item>
    /// </list>
    /// 缓存后端读写失败照常抛出，不改为直接回源
    /// </remarks>
    ValueTask<T> GetOrCreateAsync<T>(CacheKey key, Func<CancellationToken, ValueTask<T>> factory, CacheEntryOptions options, CancellationToken cancellationToken)
        where T : notnull;
}
