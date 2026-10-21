// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions;

/// <summary>
/// 条目过期：绝对存活时间或滑动过期，二选一；都不设时用 Provider 的默认存活时间。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record CacheEntryOptions
{
    /// <summary>
    /// 使用 Provider 默认存活时间。
    /// </summary>
    public static CacheEntryOptions Default { get; } = new();

    /// <summary>
    /// 写入后固定存活的时间。
    /// </summary>
    public TimeSpan? TimeToLive
    {
        get => field;
        init => field = ValidatePositive(value, nameof(TimeToLive));
    }

    /// <summary>
    /// 每次读取后延长的时间。
    /// </summary>
    public TimeSpan? SlidingExpiration
    {
        get => field;
        init => field = ValidatePositive(value, nameof(SlidingExpiration));
    }

    /// <summary>
    /// 固定存活。
    /// </summary>
    public static CacheEntryOptions Expires(TimeSpan timeToLive) => new() { TimeToLive = timeToLive };

    /// <summary>
    /// 滑动过期。
    /// </summary>
    public static CacheEntryOptions Sliding(TimeSpan slidingExpiration) => new() { SlidingExpiration = slidingExpiration };

    /// <summary>
    /// 校验组合约束：绝对与滑动不能同时设置。
    /// </summary>
    public CacheEntryOptions Validated()
        => TimeToLive is not null && SlidingExpiration is not null
            ? throw new ArgumentException("TimeToLive and SlidingExpiration cannot both be set.")
            : this;

    /// <summary>
    /// 校验过期时间：<see langword="null"/> 表示不设置，否则必须为正。
    /// </summary>
    private static TimeSpan? ValidatePositive(TimeSpan? value, string name)
        => value is null || value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(name, value, "The expiration must be positive.");
}
