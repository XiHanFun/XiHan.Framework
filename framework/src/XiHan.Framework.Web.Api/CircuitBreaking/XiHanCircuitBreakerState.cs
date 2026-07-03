#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanCircuitBreakerState
// Guid:83464355-32a5-4e7e-ab64-4a25fbb4a325
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;

namespace XiHan.Framework.Web.Api.CircuitBreaking;

/// <summary>
/// 入站熔断器状态（进程内单例）
/// </summary>
/// <remarks>
/// 三态状态机：Closed（闭合，正常放行并统计）→ Open（熔断，直接拒绝）→ HalfOpen（半开，放行少量探测）→ Closed。
/// 滑动窗口采用分桶环形数组 + Interlocked 计数的无锁实现（10 桶，每桶覆盖 WindowSeconds/10），
/// 桶轮换与计数在高并发下存在纳秒级竞态，属可接受的近似统计，换取热路径零锁开销。
/// 多实例部署时各实例独立熔断，无分布式协调（本熔断器保护的是单实例自身的过载，无需全局一致）。
/// </remarks>
public class XiHanCircuitBreakerState
{
    /// <summary>
    /// 闭合态（正常放行并统计失败率）
    /// </summary>
    private const int StateClosed = 0;

    /// <summary>
    /// 熔断态（未到期请求直接拒绝）
    /// </summary>
    private const int StateOpen = 1;

    /// <summary>
    /// 半开态（放行至多 HalfOpenMaxProbes 个探测请求）
    /// </summary>
    private const int StateHalfOpen = 2;

    /// <summary>
    /// 滑动窗口分桶数
    /// </summary>
    private const int BucketCount = 10;

    private readonly Bucket[] _buckets;
    private readonly long _bucketDurationMs;
    private readonly int _minimumRequests;
    private readonly double _failureRateThreshold;
    private readonly long _breakMs;
    private readonly int _halfOpenMaxProbes;

    private int _state = StateClosed;
    private long _openedAtMs;
    private int _halfOpenProbesIssued;
    private int _halfOpenSuccessCount;

    /// <summary>
    /// 构造函数（对配置做下限钳制，避免非法配置导致除零或永不评估）
    /// </summary>
    /// <param name="options">熔断选项</param>
    public XiHanCircuitBreakerState(IOptions<XiHanCircuitBreakingOptions> options)
    {
        var value = options.Value;
        var windowSeconds = Math.Max(1, value.WindowSeconds);
        _bucketDurationMs = Math.Max(1, windowSeconds * 1000L / BucketCount);
        _minimumRequests = Math.Max(1, value.MinimumRequests);
        _failureRateThreshold = Math.Clamp(value.FailureRateThreshold, 0.01, 1.0);
        _breakMs = Math.Max(1, value.BreakSeconds) * 1000L;
        _halfOpenMaxProbes = Math.Max(1, value.HalfOpenMaxProbes);

        _buckets = new Bucket[BucketCount];
        for (var i = 0; i < BucketCount; i++)
        {
            _buckets[i] = new Bucket();
        }
    }

    /// <summary>
    /// 请求入口判定：是否放行当前请求
    /// </summary>
    /// <param name="isProbe">是否为半开态探测请求（探测结果驱动状态迁移）</param>
    /// <param name="retryAfterSeconds">拒绝时建议的重试等待秒数（放行时为 0）</param>
    /// <returns>true 放行；false 拒绝（应返回 503）</returns>
    public bool TryPass(out bool isProbe, out int retryAfterSeconds)
    {
        while (true)
        {
            switch (Volatile.Read(ref _state))
            {
                case StateClosed:
                    isProbe = false;
                    retryAfterSeconds = 0;
                    return true;

                case StateOpen:
                    var elapsedMs = Environment.TickCount64 - Volatile.Read(ref _openedAtMs);
                    if (elapsedMs < _breakMs)
                    {
                        isProbe = false;
                        retryAfterSeconds = (int)Math.Max(1, (_breakMs - elapsedMs + 999) / 1000);
                        return false;
                    }

                    // 熔断到期：竞争进入半开态，胜者重置探测计数（CAS 与重置间的纳秒级竞态可接受）
                    if (Interlocked.CompareExchange(ref _state, StateHalfOpen, StateOpen) == StateOpen)
                    {
                        Interlocked.Exchange(ref _halfOpenProbesIssued, 0);
                        Interlocked.Exchange(ref _halfOpenSuccessCount, 0);
                    }

                    continue;

                case StateHalfOpen:
                default:
                    if (Interlocked.Increment(ref _halfOpenProbesIssued) <= _halfOpenMaxProbes)
                    {
                        isProbe = true;
                        retryAfterSeconds = 0;
                        return true;
                    }

                    // 探测名额已满：拒绝，探测请求通常很快出结果，建议短等待后重试
                    isProbe = false;
                    retryAfterSeconds = 1;
                    return false;
            }
        }
    }

    /// <summary>
    /// 记录请求结果并驱动状态迁移
    /// </summary>
    /// <remarks>
    /// 闭合态：计入滑动窗口，失败时评估是否熔断；
    /// 半开态探测：任一失败重新熔断，全部（HalfOpenMaxProbes 个）成功恢复闭合；
    /// 状态迁移期间在途的非探测请求结果不计（避免污染新状态的统计）。
    /// </remarks>
    /// <param name="isSuccess">是否成功（失败=响应 5xx 或未处理异常）</param>
    /// <param name="isProbe">是否为半开态探测请求</param>
    public void Record(bool isSuccess, bool isProbe)
    {
        if (isProbe)
        {
            // 状态已被其它探测结果迁移（如已重新熔断），迟到的探测结果不再驱动状态
            if (Volatile.Read(ref _state) != StateHalfOpen)
            {
                return;
            }

            if (!isSuccess)
            {
                TripOpen(StateHalfOpen);
                return;
            }

            if (Interlocked.Increment(ref _halfOpenSuccessCount) >= _halfOpenMaxProbes
                && Interlocked.CompareExchange(ref _state, StateClosed, StateHalfOpen) == StateHalfOpen)
            {
                // 恢复闭合：清空滑动窗口，避免熔断前的旧失败立即再次触发熔断
                ResetWindow();
            }

            return;
        }

        // 仅闭合态统计滑动窗口
        if (Volatile.Read(ref _state) != StateClosed)
        {
            return;
        }

        var bucket = GetCurrentBucket();
        if (isSuccess)
        {
            Interlocked.Increment(ref bucket.Success);
            return;
        }

        Interlocked.Increment(ref bucket.Failure);
        EvaluateTrip();
    }

    /// <summary>
    /// 评估滑动窗口失败率，达阈值则熔断（仅在记录失败时调用，成功路径零评估开销）
    /// </summary>
    private void EvaluateTrip()
    {
        var currentBucketId = Environment.TickCount64 / _bucketDurationMs;
        var minBucketId = currentBucketId - BucketCount + 1;
        var successes = 0L;
        var failures = 0L;
        foreach (var bucket in _buckets)
        {
            if (Volatile.Read(ref bucket.BucketId) < minBucketId)
            {
                continue;
            }

            successes += Volatile.Read(ref bucket.Success);
            failures += Volatile.Read(ref bucket.Failure);
        }

        var total = successes + failures;
        if (total < _minimumRequests)
        {
            return;
        }

        if ((double)failures / total >= _failureRateThreshold)
        {
            TripOpen(StateClosed);
        }
    }

    /// <summary>
    /// 从指定状态迁移到熔断态（先写开启时刻再迁移状态，避免其它线程读到过期的旧开启时刻而提前放行）
    /// </summary>
    /// <param name="fromState">期望的当前状态</param>
    private void TripOpen(int fromState)
    {
        Interlocked.Exchange(ref _openedAtMs, Environment.TickCount64);
        Interlocked.CompareExchange(ref _state, StateOpen, fromState);
    }

    /// <summary>
    /// 获取当前时间片对应的桶（环形复用：桶归属的时间片过期时由首个写入者竞争重置）
    /// </summary>
    private Bucket GetCurrentBucket()
    {
        var bucketId = Environment.TickCount64 / _bucketDurationMs;
        var bucket = _buckets[(int)(bucketId % BucketCount)];
        var seenId = Volatile.Read(ref bucket.BucketId);
        if (seenId != bucketId && Interlocked.CompareExchange(ref bucket.BucketId, bucketId, seenId) == seenId)
        {
            Interlocked.Exchange(ref bucket.Success, 0);
            Interlocked.Exchange(ref bucket.Failure, 0);
        }

        return bucket;
    }

    /// <summary>
    /// 清空滑动窗口全部分桶
    /// </summary>
    private void ResetWindow()
    {
        foreach (var bucket in _buckets)
        {
            Interlocked.Exchange(ref bucket.BucketId, -1);
            Interlocked.Exchange(ref bucket.Success, 0);
            Interlocked.Exchange(ref bucket.Failure, 0);
        }
    }

    /// <summary>
    /// 滑动窗口分桶（字段供 Interlocked 直接操作）
    /// </summary>
    private sealed class Bucket
    {
        /// <summary>
        /// 桶当前归属的绝对时间片编号（-1 表示空桶）
        /// </summary>
        public long BucketId = -1;

        /// <summary>
        /// 成功计数
        /// </summary>
        public long Success;

        /// <summary>
        /// 失败计数
        /// </summary>
        public long Failure;
    }
}
