#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheStatistics
// Guid:8b7c6d5e-4f3d-2c1b-0a9f-8e7d6c5b4a3b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Caching;

/// <summary>
/// 缓存统计信息
/// 使用原子操作确保线程安全
/// </summary>
public class CacheStatistics
{
    private long _hitCount;
    private long _missCount;
    private long _evictionCount;
    private long _expiredCount;

    /// <summary>
    /// 缓存命中次数
    /// </summary>
    public long HitCount => Interlocked.Read(ref _hitCount);

    /// <summary>
    /// 缓存未命中次数
    /// </summary>
    public long MissCount => Interlocked.Read(ref _missCount);

    /// <summary>
    /// 缓存淘汰次数
    /// </summary>
    public long EvictionCount => Interlocked.Read(ref _evictionCount);

    /// <summary>
    /// 缓存过期次数
    /// </summary>
    public long ExpiredCount => Interlocked.Read(ref _expiredCount);

    /// <summary>
    /// 总请求次数
    /// </summary>
    public long TotalRequests => HitCount + MissCount;

    /// <summary>
    /// 缓存命中率（百分比）
    /// </summary>
    public double HitRate
    {
        get
        {
            var total = TotalRequests;
            return total == 0 ? 0 : (double)HitCount / total * 100;
        }
    }

    /// <summary>
    /// 重置统计信息
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _hitCount, 0);
        Interlocked.Exchange(ref _missCount, 0);
        Interlocked.Exchange(ref _evictionCount, 0);
        Interlocked.Exchange(ref _expiredCount, 0);
    }

    /// <summary>
    /// 获取统计信息摘要
    /// </summary>
    public string GetSummary()
    {
        return $"命中: {HitCount}, 未命中: {MissCount}, 命中率: {HitRate:F2}%, " +
               $"淘汰: {EvictionCount}, 过期: {ExpiredCount}";
    }

    /// <summary>
    /// 记录命中
    /// </summary>
    internal void RecordHit()
    {
        Interlocked.Increment(ref _hitCount);
    }

    /// <summary>
    /// 记录未命中
    /// </summary>
    internal void RecordMiss()
    {
        Interlocked.Increment(ref _missCount);
    }

    /// <summary>
    /// 记录淘汰
    /// </summary>
    internal void RecordEviction()
    {
        Interlocked.Increment(ref _evictionCount);
    }

    /// <summary>
    /// 记录过期
    /// </summary>
    internal void RecordExpired()
    {
        Interlocked.Increment(ref _expiredCount);
    }
}
