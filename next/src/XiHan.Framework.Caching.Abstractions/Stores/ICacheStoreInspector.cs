// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Stores;

/// <summary>
/// 缓存存储的键管理。
/// </summary>
/// <remarks>
/// 可选能力，给管理界面用：存储能做就实现（内存与 Redis 实现）。管理界面从容器取 <see cref="ICacheStore"/>，转成本接口即可用；读写值仍走 <see cref="ICacheStore"/>（ADR-0055）。
/// 基础设施失败抛出 <see cref="CachingErrors.StoreUnavailable"/>，与 <see cref="ICacheStore"/> 一致。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface ICacheStoreInspector
{
    /// <summary>
    /// 按前缀分页列出键。
    /// </summary>
    /// <remarks>
    /// 已过期的键不列出；分页的顺序与续页标记的含义由实现决定（见各实现）。
    /// </remarks>
    ValueTask<CacheKeyPage> ListKeysAsync(CacheKeyQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// 读一个条目的大小与剩余存活时间。
    /// </summary>
    /// <remarks>
    /// 不存在或已过期为无值；读取不延长滑动过期。
    /// </remarks>
    ValueTask<Maybe<CacheEntryInfo>> InspectAsync(CacheKey key, CancellationToken cancellationToken);

    /// <summary>
    /// 删除前缀下的全部键，返回删除数。
    /// </summary>
    /// <remarks>
    /// 前缀不能为空（防误删全部），不合法时抛出 <see cref="ArgumentException"/>。
    /// </remarks>
    ValueTask<long> RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken);
}
