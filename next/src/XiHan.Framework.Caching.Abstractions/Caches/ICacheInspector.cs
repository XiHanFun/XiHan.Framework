// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Caches;

/// <summary>
/// 类型化缓存里对象条目的键管理。
/// </summary>
/// <remarks>
/// 可选能力（ADR-0058）：进程内的对象缓存把对象引用存在自己的表里，不经 <see cref="ICache.Store"/> 的字节存储，<see cref="ICacheStoreInspector"/> 看不到。管理界面把缓存转成本接口得到对象条目，再把 <see cref="ICache.Store"/> 转成 <see cref="ICacheStoreInspector"/> 得到字节条目，两份合起来是全部条目。删单个条目用 <see cref="ICache.RemoveAsync"/>，读值用 <see cref="ICache.GetAsync{T}"/>（<c>T</c> 取 <see cref="object"/>）。序列化缓存的条目都在字节存储里，不实现本接口。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface ICacheInspector
{
    /// <summary>
    /// 按前缀分页列出对象条目的键。
    /// </summary>
    /// <remarks>
    /// 前缀、上限与续页同 <see cref="ICacheStoreInspector.ListKeysAsync"/>。
    /// </remarks>
    ValueTask<CacheKeyPage> ListKeysAsync(CacheKeyQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 一个对象条目的类型与存活时间。
    /// </summary>
    /// <remarks>
    /// 不存在或已过期为无值；不读出值、不延长滑动过期。
    /// </remarks>
    ValueTask<Maybe<CachedObjectInfo>> InspectAsync(CacheKey key, CancellationToken cancellationToken);

    /// <summary>
    /// 删除前缀下的全部对象条目，返回删除数。
    /// </summary>
    /// <remarks>
    /// 前缀为空或不合法抛出 <see cref="ArgumentException"/>。
    /// </remarks>
    ValueTask<long> RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken);
}
