// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Identifiers;

/// <summary>
/// 雪花标识的位布局与解析结果（RFC-0005「标识生成」）。
/// </summary>
/// <remarks>
/// 符号位恒为 0，其后 41 位是自 epoch 起的毫秒数、10 位机器位、12 位同一毫秒内的序列。
/// </remarks>
/// <param name="Value">标识本身。</param>
/// <param name="Timestamp">生成时刻（毫秒精度，UTC）。</param>
/// <param name="WorkerId">机器位。</param>
/// <param name="Sequence">同一毫秒内的序列。</param>
[ApiLevel(Stability.Preview, "5.0")]
public readonly record struct SnowflakeId(long Value, DateTimeOffset Timestamp, int WorkerId, int Sequence)
{
    /// <summary>
    /// 时间戳位数。
    /// </summary>
    public const int TimestampBits = 41;

    /// <summary>
    /// 机器位位数。
    /// </summary>
    public const int WorkerIdBits = 10;

    /// <summary>
    /// 序列位数。
    /// </summary>
    public const int SequenceBits = 12;

    /// <summary>
    /// 机器位的最大值（1023）。
    /// </summary>
    public const int MaxWorkerId = (1 << WorkerIdBits) - 1;

    /// <summary>
    /// 序列的最大值（4095）：单个生成器每毫秒最多生成 4096 个标识。
    /// </summary>
    public const int MaxSequence = (1 << SequenceBits) - 1;

    /// <summary>
    /// 时间戳的最大值（毫秒）：约 69.7 年。
    /// </summary>
    public const long MaxTimestamp = (1L << TimestampBits) - 1;

    /// <summary>
    /// 机器位的起始位。
    /// </summary>
    public const int WorkerIdShift = SequenceBits;

    /// <summary>
    /// 时间戳的起始位。
    /// </summary>
    public const int TimestampShift = SequenceBits + WorkerIdBits;

    /// <summary>
    /// 按布局拼出标识。
    /// </summary>
    /// <remarks>
    /// 任一段越界抛出 <see cref="ArgumentOutOfRangeException"/>。
    /// </remarks>
    /// <param name="timestamp">自 epoch 起的毫秒数。</param>
    /// <param name="workerId">机器位。</param>
    /// <param name="sequence">序列。</param>
    public static long Compose(long timestamp, int workerId, int sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(timestamp);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(timestamp, MaxTimestamp);
        ArgumentOutOfRangeException.ThrowIfNegative(workerId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(workerId, MaxWorkerId);
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sequence, MaxSequence);
        return (timestamp << TimestampShift) | ((long)workerId << WorkerIdShift) | (long)sequence;
    }

    /// <summary>
    /// 按 <paramref name="epoch"/> 还原标识的时间戳、机器位与序列。
    /// </summary>
    /// <remarks>
    /// 负数不是雪花标识，抛出 <see cref="ArgumentOutOfRangeException"/>。
    /// </remarks>
    /// <param name="value">标识。</param>
    /// <param name="epoch">生成它的生成器所用的 epoch。</param>
    public static SnowflakeId Parse(long value, DateTimeOffset epoch)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        var timestamp = value >> TimestampShift;
        var workerId = (int)((value >> WorkerIdShift) & MaxWorkerId);
        var sequence = (int)(value & MaxSequence);
        return new SnowflakeId(value, epoch.ToUniversalTime().AddMilliseconds(timestamp), workerId, sequence);
    }
}
