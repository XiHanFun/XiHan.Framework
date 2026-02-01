#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheItem
// Guid:4b7fab57-8fd4-4747-b1f3-c66adc681a64
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/08/22 02:24:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Caching;

/// <summary>
/// 泛型缓存项
/// 封装缓存的值和过期信息，支持绝对过期和滑动过期两种模式
/// </summary>
/// <typeparam name="T">缓存值的类型</typeparam>
public class CacheItem<T> : ICacheItem
{
    /// <summary>
    /// 创建时间
    /// </summary>
    private readonly DateTimeOffset _createdAt;

    /// <summary>
    /// 使用 UTC ticks 存储最后访问时间，原子读写
    /// </summary>
    private long _lastAccessedUtcTicks;

    /// <summary>
    /// 访问次数（用于 LFU 策略）
    /// </summary>
    private long _accessCount;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="value">缓存的值</param>
    public CacheItem(T value)
    {
        Value = value;
        _createdAt = DateTimeOffset.UtcNow;
        _lastAccessedUtcTicks = DateTime.UtcNow.Ticks;
        _accessCount = 0;
    }

    /// <summary>
    /// 缓存的值
    /// </summary>
    public T Value { get; set; } = default!;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt => _createdAt;

    /// <summary>
    /// 访问次数
    /// </summary>
    public long AccessCount => Interlocked.Read(ref _accessCount);

    /// <summary>
    /// 绝对过期时间
    /// 当达到此时间时，缓存项将被视为过期
    /// </summary>
    public DateTimeOffset? AbsoluteExpiration { get; set; }

    /// <summary>
    /// 滑动过期时间间隔
    /// 如果在此时间间隔内未访问缓存项，则缓存项将被视为过期
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// 最后访问时间（UTC）
    /// 用于计算滑动过期时间
    /// </summary>
    public DateTimeOffset LastAccessed
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastAccessedUtcTicks);
            var utc = DateTime.SpecifyKind(new DateTime(ticks), DateTimeKind.Utc);
            return new DateTimeOffset(utc);
        }
    }

    /// <summary>
    /// 检查缓存项是否已过期
    /// </summary>
    /// <returns>如果缓存项已过期返回 true，否则返回 false</returns>
    public bool IsExpired
    {
        get
        {
            var now = DateTimeOffset.UtcNow;

            // 检查绝对过期时间
            if (AbsoluteExpiration.HasValue && now > AbsoluteExpiration.Value)
            {
                return true;
            }

            // 检查滑动过期时间
            if (SlidingExpiration.HasValue && now - LastAccessed > SlidingExpiration.Value)
            {
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// 下一个过期时间（绝对或滑动）
    /// </summary>
    public DateTimeOffset? NextExpiration
    {
        get
        {
            var next = AbsoluteExpiration;
            if (SlidingExpiration.HasValue)
            {
                var slidingExpiration = LastAccessed.Add(SlidingExpiration.Value);
                if (!next.HasValue || slidingExpiration < next.Value)
                {
                    next = slidingExpiration;
                }
            }

            return next;
        }
    }

    /// <summary>
    /// 更新最后访问时间为当前时间
    /// 用于滑动过期时间的计算
    /// </summary>
    public void UpdateLastAccessed()
    {
        Interlocked.Exchange(ref _lastAccessedUtcTicks, DateTime.UtcNow.Ticks);
        Interlocked.Increment(ref _accessCount);
    }
}
