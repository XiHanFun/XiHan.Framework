// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Identifiers;

/// <summary>
/// 雪花 <see cref="long"/> 标识生成器（RFC-0005「标识生成」）。
/// </summary>
/// <remarks>
/// 41 位自 epoch 起的毫秒、10 位机器位、12 位序列。线程安全；同一毫秒序列用尽时等待下一毫秒；时钟回拨不超过 <see cref="SnowflakeOptions.ClockDriftTolerance"/> 时等待追平，超过时抛出携带 <see cref="PersistenceErrors.ClockMovedBackwards"/> 的 <see cref="FrameworkException"/>；没有机器位时抛出携带 <see cref="PersistenceErrors.WorkerIdUnavailable"/> 的异常。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class SnowflakeIdGenerator : IIdGenerator<long>
{
    /// <summary>
    /// 机器位来源。
    /// </summary>
    private readonly IWorkerIdSource _workerIds;

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// epoch 的 UTC 刻度。
    /// </summary>
    private readonly long _epochTicks;

    /// <summary>
    /// 时钟回拨的容忍上限（毫秒）。
    /// </summary>
    private readonly long _tolerance;

    /// <summary>
    /// 保护 <see cref="_last"/> 与 <see cref="_sequence"/>。
    /// </summary>
    /// <remarks>
    /// 等待时钟的线程也持有它，其余线程排队而不是各自等。
    /// </remarks>
    private readonly object _gate = new();

    /// <summary>
    /// 上一个标识的时间戳（自 epoch 起的毫秒）。
    /// </summary>
    /// <remarks>
    /// 还没生成过为 -1。
    /// </remarks>
    private long _last = -1;

    /// <summary>
    /// 上一个标识在其毫秒内的序列。
    /// </summary>
    private int _sequence;

    /// <summary>
    /// 创建。
    /// </summary>
    /// <remarks>
    /// 时钟早于 epoch 是配置错误，抛出 <see cref="ArgumentException"/>。
    /// </remarks>
    /// <param name="options">选项。</param>
    /// <param name="workerIds">机器位来源。</param>
    /// <param name="clock">时钟。</param>
    public SnowflakeIdGenerator(SnowflakeOptions options, IWorkerIdSource workerIds, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(workerIds);
        ArgumentNullException.ThrowIfNull(clock);
        if (clock.GetUtcNow() < options.Epoch)
        {
            throw new ArgumentException($"The clock ({clock.GetUtcNow():O}) is before the snowflake epoch ({options.Epoch:O}); '{SnowflakeOptions.ConfigurationSection}:Epoch' must not be in the future.", nameof(options));
        }

        Epoch = options.Epoch;
        _epochTicks = options.Epoch.UtcTicks;
        _tolerance = (long)options.ClockDriftTolerance.TotalMilliseconds;
        _workerIds = workerIds;
        _clock = clock;
    }

    /// <summary>
    /// 时间戳的起点。
    /// </summary>
    public DateTimeOffset Epoch { get; }

    /// <summary>
    /// 当前机器位。
    /// </summary>
    /// <remarks>
    /// 无值表示此刻生成会失败。
    /// </remarks>
    public Maybe<int> WorkerId => _workerIds.Current;

    /// <inheritdoc />
    public long NewId()
    {
        lock (_gate)
        {
            // 机器位在锁内读：租约丢失后不再生成，而不是沿用旧机器位
            if (!_workerIds.Current.TryGetValue(out var workerId))
            {
                throw new FrameworkException(PersistenceErrors.WorkerIdUnavailable, "No snowflake worker ID is available: the lease has not been acquired or has been lost.");
            }

            var now = Now();
            if (now < _last)
            {
                // 回拨：小于容忍上限就在锁内等时钟追上上一个时间戳，否则宁可失败也不发出可能重复的标识
                var drift = _last - now;
                if (drift > _tolerance)
                {
                    throw new FrameworkException(PersistenceErrors.ClockMovedBackwards, $"The clock moved backwards by {drift} ms, which exceeds the tolerance of {_tolerance} ms; refusing to generate snowflake IDs until the clock catches up.");
                }

                now = WaitUntil(_last);
            }

            if (now == _last)
            {
                _sequence = (_sequence + 1) & SnowflakeId.MaxSequence;
                if (_sequence == 0)
                {
                    // 本毫秒的 4096 个序列用尽：等下一毫秒
                    now = WaitUntil(_last + 1);
                }
            }
            else
            {
                _sequence = 0;
            }

            if (now > SnowflakeId.MaxTimestamp)
            {
                throw new InvalidOperationException($"The 41-bit timestamp space since the snowflake epoch ({Epoch:O}) is exhausted.");
            }

            _last = now;
            return SnowflakeId.Compose(now, workerId, _sequence);
        }
    }

    /// <summary>
    /// 按本生成器的 epoch 还原标识的时间戳、机器位与序列。
    /// </summary>
    public SnowflakeId Parse(long value) => SnowflakeId.Parse(value, Epoch);

    /// <summary>
    /// 自 epoch 起的毫秒数。
    /// </summary>
    /// <remarks>
    /// 早于 epoch 时为负。
    /// </remarks>
    private long Now() => (_clock.GetUtcNow().UtcTicks - _epochTicks) / TimeSpan.TicksPerMillisecond;

    /// <summary>
    /// 自旋等待时钟到达 <paramref name="target"/>，返回到达时的时间戳。
    /// </summary>
    /// <remarks>
    /// 等待期间时钟再次回拨超过容忍上限时放弃。
    /// </remarks>
    private long WaitUntil(long target)
    {
        var spin = new SpinWait();
        long now;
        while ((now = Now()) < target)
        {
            if (target - now > _tolerance)
            {
                throw new FrameworkException(PersistenceErrors.ClockMovedBackwards, $"The clock moved backwards by {target - now} ms while waiting, which exceeds the tolerance of {_tolerance} ms; refusing to generate snowflake IDs until the clock catches up.");
            }

            // SpinOnce 先自旋再让出时间片：等下一毫秒通常只需几次自旋，等回拨追平时则让出 CPU
            spin.SpinOnce();
        }

        return now;
    }
}
