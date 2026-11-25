// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using XiHan.Framework.Persistence.Abstractions.Identifiers;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// 回放 <c>snowflake-ids.json</c>：雪花标识的位布局
/// </summary>
/// <remarks>
/// 规范 RFC-0005「雪花标识」：符号位恒为 0，其后 41 位是自 epoch 起的毫秒数、10 位机器位、12 位同一毫秒内的序列；由三段合成标识、由标识还原三段与时刻，两个方向都要对（ADR-0277）
/// </remarks>
public sealed class SnowflakeIdVectorTests
{
    /// <summary>
    /// 向量里时刻的写法：UTC，秒以下可以没有
    /// </summary>
    private const string InstantFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly SnowflakeVectors _vectors = SpecVectors.Read<SnowflakeVectors>("persistence", "snowflake-ids.json");

    [Fact]
    public void TheLayout_IsTheOneInTheVectors()
    {
        Assert.Equal(SnowflakeId.TimestampBits, _vectors.TimestampBits);
        Assert.Equal(SnowflakeId.WorkerIdBits, _vectors.WorkerBits);
        Assert.Equal(SnowflakeId.SequenceBits, _vectors.SequenceBits);
        Assert.Equal(SnowflakeId.MaxWorkerId, _vectors.MaxWorker);
        Assert.Equal(SnowflakeId.MaxSequence, _vectors.MaxSequence);
    }

    [Fact]
    public void TheDefaultEpochAndTheWorkerLockPrefix_AreTheOnesInTheVectors()
    {
        Assert.Equal(SnowflakeOptions.DefaultEpoch, Utc(_vectors.DefaultEpoch));
        Assert.Equal(Utc(_vectors.DefaultEpoch), new SnowflakeOptions().Epoch);
        Assert.Equal(LeasedWorkerIdSource.ResourcePrefix, _vectors.WorkerLockPrefix);
    }

    [Fact]
    public void ComposeAndParse_AgreeWithEveryCase()
    {
        Assert.NotEmpty(_vectors.Cases);
        var failures = new List<string>();
        foreach (var vector in _vectors.Cases)
        {
            var value = Integer(vector.Value);
            var milliseconds = Integer(vector.Milliseconds);
            var epoch = Utc(vector.Epoch);

            // 合成：三段到标识
            var composed = SnowflakeId.Compose(milliseconds, vector.Worker, vector.Sequence);
            if (composed != value)
            {
                failures.Add($"{vector.Name}: composed {composed}, the vector has {value}");
            }

            // 还原：标识到三段与时刻；毫秒数按整数的刻度比，不经浮点
            var parsed = SnowflakeId.Parse(value, epoch);
            var elapsed = (parsed.Timestamp - epoch).Ticks;
            if (parsed.Value != value || parsed.WorkerId != vector.Worker || parsed.Sequence != vector.Sequence || elapsed != milliseconds * TimeSpan.TicksPerMillisecond || parsed.Timestamp != Utc(vector.Time))
            {
                failures.Add($"{vector.Name}: parsed {elapsed / TimeSpan.TicksPerMillisecond} ms, worker {parsed.WorkerId}, sequence {parsed.Sequence} at '{parsed.Timestamp.ToString(InstantFormat, CultureInfo.InvariantCulture)}', the vector has {milliseconds} ms, worker {vector.Worker}, sequence {vector.Sequence} at '{vector.Time}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Compose_RejectsThePartsOutOfRange()
    {
        Assert.NotEmpty(_vectors.OutOfRange);
        foreach (var vector in _vectors.OutOfRange)
        {
            var error = Record.Exception(() => SnowflakeId.Compose(Integer(vector.Milliseconds), vector.Worker, vector.Sequence));

            Assert.True(error is ArgumentOutOfRangeException, vector.Name);
        }
    }

    [Fact]
    public void TheVectors_CoverTheEndsOfEveryPart_AndValuesBeyondDoublePrecision()
    {
        // 防止向量换了写法后边界一条都没核对还照样通过
        // 三段各自到上限的、63 位全 1 的、超过 2^53（JSON 数字在有的语言里读不准）的、epoch 不是缺省值的
        Assert.Contains(_vectors.Cases, vector => vector.Worker == _vectors.MaxWorker && vector.Sequence == _vectors.MaxSequence);
        Assert.Contains(_vectors.Cases, vector => Integer(vector.Milliseconds) == (1L << _vectors.TimestampBits) - 1);
        Assert.Contains(_vectors.Cases, static vector => Integer(vector.Value) == long.MaxValue);
        Assert.Contains(_vectors.Cases, static vector => Integer(vector.Value) > (1L << 53));
        Assert.Contains(_vectors.Cases, vector => Utc(vector.Epoch) != Utc(_vectors.DefaultEpoch));

        // 越界：三段各自多一个的与为负的
        Assert.Contains(_vectors.OutOfRange, vector => vector.Worker == _vectors.MaxWorker + 1);
        Assert.Contains(_vectors.OutOfRange, static vector => vector.Worker < 0);
        Assert.Contains(_vectors.OutOfRange, vector => vector.Sequence == _vectors.MaxSequence + 1);
        Assert.Contains(_vectors.OutOfRange, static vector => vector.Sequence < 0);
        Assert.Contains(_vectors.OutOfRange, vector => Integer(vector.Milliseconds) == 1L << _vectors.TimestampBits);
        Assert.Contains(_vectors.OutOfRange, static vector => Integer(vector.Milliseconds) < 0);
    }

    /// <summary>
    /// 向量里写成十进制字符串的 64 位整数
    /// </summary>
    /// <remarks>
    /// 标识与毫秒数会超过 2^53，向量不用 JSON 数字写它们
    /// </remarks>
    private static long Integer(string text)
        => long.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

    /// <summary>
    /// 向量里的 UTC 时刻
    /// </summary>
    private static DateTimeOffset Utc(string text)
        => DateTimeOffset.ParseExact(text, InstantFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="DefaultEpoch">epoch 的缺省值</param>
    /// <param name="TimestampBits">毫秒数的位数</param>
    /// <param name="WorkerBits">机器位的位数</param>
    /// <param name="SequenceBits">序列的位数</param>
    /// <param name="MaxWorker">机器位的最大值</param>
    /// <param name="MaxSequence">序列的最大值</param>
    /// <param name="WorkerLockPrefix">租约分配机器位时锁的资源名前缀，后接机器位的十进制写法</param>
    /// <param name="Cases">合成与还原的用例</param>
    /// <param name="OutOfRange">合成时必须拒绝的三段</param>
    private sealed record SnowflakeVectors(string DefaultEpoch, int TimestampBits, int WorkerBits, int SequenceBits, int MaxWorker, int MaxSequence, string WorkerLockPrefix, SnowflakeCase[] Cases, OutOfRangeCase[] OutOfRange);

    /// <summary>
    /// 一个标识与它的三段
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Epoch">生成它的 epoch（UTC）</param>
    /// <param name="Value">标识，十进制字符串</param>
    /// <param name="Milliseconds">自 epoch 起的毫秒数，十进制字符串</param>
    /// <param name="Worker">机器位</param>
    /// <param name="Sequence">同一毫秒内的序列</param>
    /// <param name="Time">epoch 加毫秒数的时刻（UTC）</param>
    private sealed record SnowflakeCase(string Name, string Epoch, string Value, string Milliseconds, int Worker, int Sequence, string Time);

    /// <summary>
    /// 合成时必须拒绝的三段
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Milliseconds">自 epoch 起的毫秒数，十进制字符串</param>
    /// <param name="Worker">机器位</param>
    /// <param name="Sequence">同一毫秒内的序列</param>
    private sealed record OutOfRangeCase(string Name, string Milliseconds, int Worker, int Sequence);
}
