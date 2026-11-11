// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions;

/// <summary>
/// 缓存错误码
/// </summary>
/// <remarks>
/// 登记在 <c>spec/conventions/error-codes.md</c>
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class CachingErrors
{
    /// <summary>
    /// 键不合法
    /// </summary>
    public const string InvalidKey = "CACHING_VALIDATION_001";

    /// <summary>
    /// 条目选项不合法
    /// </summary>
    /// <remarks>
    /// 远程缓存插件按操作集校验过期选项时返回，适配器原样成为异常；.NET 侧构造 <see cref="Stores.CacheEntryOptions"/> 时直接抛 <see cref="ArgumentException"/>
    /// </remarks>
    public const string InvalidOptions = "CACHING_VALIDATION_002";

    /// <summary>
    /// 键列表的每页条数越界
    /// </summary>
    /// <remarks>
    /// <see cref="Stores.CacheKeyQuery.PageSize"/> 不在 1 到 <see cref="Stores.CacheKeyQuery.MaxPageSize"/> 之间：不在构造时抛出，<c>ListKeysAsync</c> 以结果返回（HTTP 400）
    /// </remarks>
    public const string ListPageSizeInvalid = "CACHING_VALIDATION_003";

    /// <summary>
    /// 键前缀不合法
    /// </summary>
    /// <remarks>
    /// 列键时前缀不合键的文法，或按前缀删除时前缀为空或不合文法：不在构造时抛出，以结果返回（HTTP 400）
    /// </remarks>
    public const string PrefixInvalid = "CACHING_VALIDATION_004";

    /// <summary>
    /// 存储不可达或操作失败
    /// </summary>
    public const string StoreUnavailable = "CACHING_UNAVAILABLE_001";

    /// <summary>
    /// 锁被其他持有者占用
    /// </summary>
    /// <remarks>
    /// <see cref="Locking.DistributedLockExtensions.AcquireAsync"/> 返回；<see cref="Locking.IDistributedLock.TryAcquireAsync"/> 被占用时只是无值
    /// </remarks>
    public const string LockHeld = "CACHING_CONFLICT_001";

    /// <summary>
    /// 同一幂等键的调用仍在处理中
    /// </summary>
    public const string IdempotentInProgress = "CACHING_CONFLICT_002";

    /// <summary>
    /// 幂等记录不能回放给本次调用
    /// </summary>
    /// <remarks>
    /// 同一幂等键已被另一个调用（另一个操作，或操作与非操作调用之间）使用，或记录里的响应无法还原
    /// </remarks>
    public const string IdempotentMismatch = "CACHING_CONFLICT_003";

    /// <summary>
    /// 内存缓存已达容量上限
    /// </summary>
    /// <remarks>
    /// 健康项 Degraded；LRU 继续淘汰最久未访问的条目
    /// </remarks>
    public const string CapacityReached = "CACHING_UNAVAILABLE_002";
}
