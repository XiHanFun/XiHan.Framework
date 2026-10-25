// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Leadership;

namespace XiHan.Framework.Scheduling.InMemory;

/// <summary>
/// 进程内租约式领导选举：每个资源一条记录（持有者、存活时间、到期时刻），过期视为空闲。单实例下正确；多实例换数据库或 Kubernetes Lease 的 Provider。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryLeaderElection : ILeaderElection
{
    /// <summary>
    /// 一条租约记录。
    /// </summary>
    /// <param name="Holder">持有者。</param>
    /// <param name="TimeToLive">每次续约延长的时间。</param>
    /// <param name="ExpiresAt">到期时刻。</param>
    private sealed record LeaseRecord(string Holder, TimeSpan TimeToLive, DateTimeOffset ExpiresAt);

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 保护记录表的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 资源 → 记录。
    /// </summary>
    private readonly Dictionary<string, LeaseRecord> _leases = new(StringComparer.Ordinal);

    /// <summary>
    /// 创建。
    /// </summary>
    public InMemoryLeaderElection(TimeProvider clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public ValueTask<Result<LeaderLease>> TryAcquireAsync(JobName resource, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        if (timeToLive <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), timeToLive, "The time to live must be positive.");
        }

        lock (_lock)
        {
            var now = _clock.GetUtcNow();
            // 未过期的租约由他人持有即拒绝；过期的记录视为空闲，直接覆盖
            if (_leases.TryGetValue(resource.Value, out var existing) && existing.ExpiresAt > now)
            {
                return ValueTask.FromResult<Result<LeaderLease>>(Error.Conflict(SchedulingErrors.LeaseHeld, SchedulingMessages.LeaseHeld, MessageArguments.Of(("resource", resource.Value))));
            }

            var holder = Guid.NewGuid().ToString("N");
            _leases[resource.Value] = new LeaseRecord(holder, timeToLive, now + timeToLive);
            return ValueTask.FromResult(Result.Success<LeaderLease>(new InMemoryLeaderLease(this, resource, holder)));
        }
    }

    /// <summary>
    /// 资源当前是否由 <paramref name="holder"/> 持有且未过期。
    /// </summary>
    internal bool IsHeldBy(JobName resource, string holder)
    {
        lock (_lock)
        {
            return _leases.TryGetValue(resource.Value, out var record) && record.Holder == holder && record.ExpiresAt > _clock.GetUtcNow();
        }
    }

    /// <summary>
    /// 持有者的到期时刻；不是持有者时为 <see cref="DateTimeOffset.MinValue"/>。
    /// </summary>
    internal DateTimeOffset ExpiresAt(JobName resource, string holder)
    {
        lock (_lock)
        {
            return _leases.TryGetValue(resource.Value, out var record) && record.Holder == holder ? record.ExpiresAt : DateTimeOffset.MinValue;
        }
    }

    /// <summary>
    /// 续约：仍是未过期的持有者才延长。
    /// </summary>
    internal Result Renew(JobName resource, string holder)
    {
        lock (_lock)
        {
            var now = _clock.GetUtcNow();
            if (!_leases.TryGetValue(resource.Value, out var record) || record.Holder != holder || record.ExpiresAt <= now)
            {
                return Error.Conflict(SchedulingErrors.LeaseHeld, SchedulingMessages.LeaseHeld, MessageArguments.Of(("resource", resource.Value)));
            }

            _leases[resource.Value] = record with { ExpiresAt = now + record.TimeToLive };
            return Result.Success();
        }
    }

    /// <summary>
    /// 释放：仍是持有者才删除记录；幂等。
    /// </summary>
    internal void Release(JobName resource, string holder)
    {
        lock (_lock)
        {
            if (_leases.TryGetValue(resource.Value, out var record) && record.Holder == holder)
            {
                _leases.Remove(resource.Value);
            }
        }
    }
}
