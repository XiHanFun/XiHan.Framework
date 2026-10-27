// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Persistence.Abstractions.Identifiers;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// <see cref="SnowflakeId"/> 的位布局：拼装与解析互逆、各段边界、负数拒绝。
/// </summary>
public sealed class SnowflakeIdTests
{
    [Fact]
    public void Layout_Is41_10_12()
    {
        Assert.Equal(41, SnowflakeId.TimestampBits);
        Assert.Equal(10, SnowflakeId.WorkerIdBits);
        Assert.Equal(12, SnowflakeId.SequenceBits);
        Assert.Equal(1023, SnowflakeId.MaxWorkerId);
        Assert.Equal(4095, SnowflakeId.MaxSequence);
        Assert.Equal((1L << 41) - 1, SnowflakeId.MaxTimestamp);
        // 最大值仍是正数：符号位恒为 0
        Assert.True(SnowflakeId.Compose(SnowflakeId.MaxTimestamp, SnowflakeId.MaxWorkerId, SnowflakeId.MaxSequence) > 0);
        Assert.Equal(long.MaxValue, SnowflakeId.Compose(SnowflakeId.MaxTimestamp, SnowflakeId.MaxWorkerId, SnowflakeId.MaxSequence));
    }

    [Theory]
    [InlineData(0L, 0, 0)]
    [InlineData(1L, 0, 1)]
    [InlineData(123_456_789L, 17, 4095)]
    [InlineData((1L << 41) - 1, 1023, 0)]
    public void Compose_ThenParse_RoundTrips(long timestamp, int workerId, int sequence)
    {
        var epoch = SnowflakeOptions.DefaultEpoch;

        var parsed = SnowflakeId.Parse(SnowflakeId.Compose(timestamp, workerId, sequence), epoch);

        Assert.Equal(epoch.AddMilliseconds(timestamp), parsed.Timestamp);
        Assert.Equal(workerId, parsed.WorkerId);
        Assert.Equal(sequence, parsed.Sequence);
        Assert.Equal(SnowflakeId.Compose(timestamp, workerId, sequence), parsed.Value);
    }

    [Fact]
    public void Compose_RejectsOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SnowflakeId.Compose(-1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SnowflakeId.Compose(SnowflakeId.MaxTimestamp + 1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SnowflakeId.Compose(0, 1024, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SnowflakeId.Compose(0, 0, 4096));
        Assert.Throws<ArgumentOutOfRangeException>(() => SnowflakeId.Parse(-1, SnowflakeOptions.DefaultEpoch));
    }

    [Fact]
    public void Parse_UsesEpochInUtc()
    {
        var epoch = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.FromHours(8));

        var parsed = SnowflakeId.Parse(SnowflakeId.Compose(1_000, 1, 1), epoch);

        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 1, TimeSpan.Zero), parsed.Timestamp);
        Assert.Equal(TimeSpan.Zero, parsed.Timestamp.Offset);
    }
}
