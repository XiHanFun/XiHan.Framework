// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DistributedIds.SnowflakeIds;

namespace XiHan.Framework.DistributedIds.Tests.SnowflakeIds;

/// <summary>
/// 雪花唯一标识生成器选项的测试
/// </summary>
/// <remarks>
/// 选项的每个属性都带自定义 setter 校验，这些校验就是「配置错了要立刻炸」的第一道闸门，
/// 因此默认值、边界值与越界抛异常都必须锁死；位长之间还存在「机器码位长 + 序列号位长 ≤ 22」的交叉约束。
/// </remarks>
public class SnowflakeIdOptionsTests
{
    /// <summary>
    /// 配置节名称被 appsettings 直接引用，不允许漂移
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:DistributedIds:SnowflakeId", SnowflakeIdOptions.SectionName);
    }

    /// <summary>
    /// 新建选项时各字段落在文档描述的默认值上
    /// </summary>
    [Fact]
    public void Defaults_MatchDocumentedValues()
    {
        var options = new SnowflakeIdOptions();

        Assert.Equal(0, options.WorkerId);
        Assert.Equal(6, options.WorkerIdBitLength);
        Assert.Equal(6, options.SeqBitLength);
        Assert.Equal(63, options.MaxSeqNumber);
        Assert.Equal(5, options.MinSeqNumber);
        Assert.Equal(2000, options.TopOverCostCount);
        Assert.Equal(TimestampTypes.Milliseconds, options.TimestampType);
        Assert.Equal(SnowflakeIdTypes.SnowFlakeMethod, options.SnowflakeIdType);
        Assert.Equal(0, options.DataCenterId);
        Assert.Equal(5, options.DataCenterIdBitLength);
        Assert.Equal(0, options.IdLength);
        Assert.Equal(string.Empty, options.IdPrefix);
        Assert.False(options.LoopedSequence);
        Assert.Equal(10000, options.MaxBackwardToleranceMs);
        Assert.True(options.UseCustomEpoch);
    }

    /// <summary>
    /// 默认纪元时间固定为 2026-01-01 UTC
    /// </summary>
    [Fact]
    public void Defaults_BaseTimeIsUtcEpoch2026()
    {
        var options = new SnowflakeIdOptions();

        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), options.BaseTime);
        Assert.Equal(DateTimeKind.Utc, options.BaseTime.Kind);
    }

    /// <summary>
    /// 生成器标识默认是 32 位无连字符 Guid，且实例之间互不相同
    /// </summary>
    [Fact]
    public void Defaults_GeneratorIdIsPerInstanceGuid()
    {
        var first = new SnowflakeIdOptions();
        var second = new SnowflakeIdOptions();

        Assert.Equal(32, first.GeneratorId.Length);
        Assert.True(Guid.TryParseExact(first.GeneratorId, "N", out _));
        Assert.NotEqual(first.GeneratorId, second.GeneratorId);
    }

    /// <summary>
    /// 机器码超出当前机器码位长可表达的范围时拒绝赋值
    /// </summary>
    [Fact]
    public void WorkerId_WhenExceedsBitLengthCapacity_Throws()
    {
        var options = new SnowflakeIdOptions();

        // 默认机器码位长 6，可表达 0-63
        Assert.Throws<ArgumentException>(() => { options.WorkerId = 64; });
        Assert.Equal(0, options.WorkerId);
    }

    /// <summary>
    /// 机器码取到位长上边界时可以正常赋值
    /// </summary>
    [Fact]
    public void WorkerId_AtBitLengthCapacity_IsAccepted()
    {
        var options = new SnowflakeIdOptions
        {
            WorkerId = 63
        };

        Assert.Equal(63, options.WorkerId);
    }

    /// <summary>
    /// 机器码位长必须落在 1-15
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(255)]
    public void WorkerIdBitLength_OutOfRange_Throws(int bitLength)
    {
        var options = new SnowflakeIdOptions();

        Assert.Throws<ArgumentException>(() => { options.WorkerIdBitLength = (byte)bitLength; });
    }

    /// <summary>
    /// 序列号位长必须落在 3-21
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(22)]
    public void SeqBitLength_OutOfRange_Throws(int bitLength)
    {
        var options = new SnowflakeIdOptions();

        Assert.Throws<ArgumentException>(() => { options.SeqBitLength = (byte)bitLength; });
    }

    /// <summary>
    /// 机器码位长与序列号位长之和不能超过 22
    /// </summary>
    [Fact]
    public void SeqBitLength_WhenSumWithWorkerIdBitLengthExceeds22_Throws()
    {
        var options = new SnowflakeIdOptions
        {
            // 15 + 默认序列号位长 6 = 21，仍在允许范围内
            WorkerIdBitLength = 15
        };

        Assert.Throws<ArgumentException>(() => { options.SeqBitLength = 8; });
        Assert.Equal(6, options.SeqBitLength);
    }

    /// <summary>
    /// 位长之和刚好等于 22 时被接受
    /// </summary>
    [Fact]
    public void SeqBitLength_WhenSumWithWorkerIdBitLengthEquals22_IsAccepted()
    {
        var options = new SnowflakeIdOptions
        {
            WorkerIdBitLength = 1,
            SeqBitLength = 21
        };

        Assert.Equal(1, options.WorkerIdBitLength);
        Assert.Equal(21, options.SeqBitLength);
    }

    /// <summary>
    /// 最大序列数不能超过当前序列号位长能表达的最大值，也不能为负
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(64)]
    public void MaxSeqNumber_OutOfSeqBitCapacity_Throws(int maxSeqNumber)
    {
        // 默认序列号位长 6，可表达 0-63
        var options = new SnowflakeIdOptions();

        Assert.Throws<ArgumentException>(() => { options.MaxSeqNumber = maxSeqNumber; });
    }

    /// <summary>
    /// 放大序列号位长后，最大序列数的上限随之抬高
    /// </summary>
    [Fact]
    public void MaxSeqNumber_FollowsSeqBitLength()
    {
        var options = new SnowflakeIdOptions
        {
            SeqBitLength = 12,
            MaxSeqNumber = 4095
        };

        Assert.Equal(4095, options.MaxSeqNumber);
    }

    /// <summary>
    /// 最小序列数必须落在 0-127
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(128)]
    public void MinSeqNumber_OutOfRange_Throws(int minSeqNumber)
    {
        var options = new SnowflakeIdOptions();

        Assert.Throws<ArgumentException>(() => { options.MinSeqNumber = minSeqNumber; });
    }

    /// <summary>
    /// 最大漂移次数必须落在 0-10000
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(10001)]
    public void TopOverCostCount_OutOfRange_Throws(int count)
    {
        var options = new SnowflakeIdOptions();

        Assert.Throws<ArgumentException>(() => { options.TopOverCostCount = count; });
    }

    /// <summary>
    /// Id 长度只接受 0 或 10-20
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    [InlineData(21)]
    public void IdLength_OutOfRange_Throws(int idLength)
    {
        var options = new SnowflakeIdOptions();

        Assert.Throws<ArgumentException>(() => { options.IdLength = (byte)idLength; });
    }

    /// <summary>
    /// Id 长度的合法取值被接受
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(20)]
    public void IdLength_WithinRange_IsAccepted(int idLength)
    {
        var options = new SnowflakeIdOptions
        {
            IdLength = (byte)idLength
        };

        Assert.Equal(idLength, options.IdLength);
    }

    /// <summary>
    /// Id 前缀为 null 时回落到空字符串，避免下游拼接出 "null"
    /// </summary>
    [Fact]
    public void IdPrefix_WhenNull_FallsBackToEmpty()
    {
        var options = new SnowflakeIdOptions
        {
            IdPrefix = "ORD-"
        };

        options.IdPrefix = null!;

        Assert.Equal(string.Empty, options.IdPrefix);
    }

    /// <summary>
    /// 最大时钟回拨容忍时间必须落在 0-60000 毫秒
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(60001)]
    public void MaxBackwardToleranceMs_OutOfRange_Throws(int tolerance)
    {
        var options = new SnowflakeIdOptions();

        Assert.Throws<ArgumentException>(() => { options.MaxBackwardToleranceMs = tolerance; });
    }

    /// <summary>
    /// 数据中心唯一标识不能超过 5 位可表达的 0-31
    /// </summary>
    [Fact]
    public void DataCenterId_WhenExceeds31_Throws()
    {
        var options = new SnowflakeIdOptions();

        Assert.Throws<ArgumentException>(() => { options.DataCenterId = 32; });
    }

    /// <summary>
    /// 远超当前时间的基准时间被拒绝
    /// </summary>
    [Fact]
    public void BaseTime_WhenFarBeyondNow_Throws()
    {
        var options = new SnowflakeIdOptions();

        var exception = Record.Exception(() => { options.BaseTime = new DateTime(2200, 1, 1, 0, 0, 0, DateTimeKind.Utc); });

        Assert.NotNull(exception);
        Assert.Contains("基准时间", exception.Message);
    }

    /// <summary>
    /// 低负载预设给出 6 位机器码 + 6 位序列号
    /// </summary>
    [Fact]
    public void LowWorkload_UsesSmallSequenceSpace()
    {
        var options = SnowflakeIdOptions.LowWorkload(7);

        Assert.Equal(7, options.WorkerId);
        Assert.Equal(6, options.SeqBitLength);
        Assert.Equal(6, options.WorkerIdBitLength);
    }

    /// <summary>
    /// 中负载预设给出 10 位序列号
    /// </summary>
    [Fact]
    public void MediumWorkload_UsesTenBitSequence()
    {
        var options = SnowflakeIdOptions.MediumWorkload(2);

        Assert.Equal(2, options.WorkerId);
        Assert.Equal(10, options.SeqBitLength);
        Assert.Equal(6, options.WorkerIdBitLength);
    }

    /// <summary>
    /// 高负载预设给出 12 位序列号
    /// </summary>
    [Fact]
    public void HighWorkload_UsesTwelveBitSequence()
    {
        var options = SnowflakeIdOptions.HighWorkload(3);

        Assert.Equal(3, options.WorkerId);
        Assert.Equal(12, options.SeqBitLength);
        Assert.Equal(6, options.WorkerIdBitLength);
    }

    /// <summary>
    /// 短唯一标识预设固定输出 10 位字符串
    /// </summary>
    [Fact]
    public void ShortId_FixesIdLengthToTen()
    {
        var options = SnowflakeIdOptions.ShortId(4);

        Assert.Equal(4, options.WorkerId);
        Assert.Equal(8, options.SeqBitLength);
        Assert.Equal(4, options.WorkerIdBitLength);
        Assert.Equal(10, options.IdLength);
    }

    /// <summary>
    /// 带前缀预设保留调用方给的前缀且不截断长度
    /// </summary>
    [Fact]
    public void PrefixedId_KeepsPrefixAndDoesNotTruncate()
    {
        var options = SnowflakeIdOptions.PrefixedId("ORD-", 5);

        Assert.Equal("ORD-", options.IdPrefix);
        Assert.Equal(5, options.WorkerId);
        Assert.Equal(8, options.SeqBitLength);
        Assert.Equal(6, options.WorkerIdBitLength);
        Assert.Equal(0, options.IdLength);
    }

    /// <summary>
    /// 经典预设切换到传统雪花算法并启用数据中心位
    /// </summary>
    [Fact]
    public void Classic_SwitchesAlgorithmAndEnablesDataCenter()
    {
        var options = SnowflakeIdOptions.Classic(6, 3);

        Assert.Equal(SnowflakeIdTypes.ClassicSnowFlakeMethod, options.SnowflakeIdType);
        Assert.Equal(6, options.WorkerId);
        Assert.Equal(3, options.DataCenterId);
        Assert.Equal(12, options.SeqBitLength);
        Assert.Equal(5, options.WorkerIdBitLength);
        Assert.Equal(5, options.DataCenterIdBitLength);
    }

    /// <summary>
    /// 序列化再反序列化后关键配置保持一致
    /// </summary>
    [Fact]
    public void ToJson_ThenFromJson_PreservesConfiguration()
    {
        var options = new SnowflakeIdOptions
        {
            WorkerId = 9,
            MinSeqNumber = 1,
            TopOverCostCount = 500,
            IdPrefix = "T-",
            LoopedSequence = true,
            MaxBackwardToleranceMs = 3000,
            UseCustomEpoch = false,
            GeneratorId = "unit-test-generator"
        };

        var restored = SnowflakeIdOptions.FromJson(options.ToJson());

        Assert.Equal(9, restored.WorkerId);
        Assert.Equal(6, restored.WorkerIdBitLength);
        Assert.Equal(6, restored.SeqBitLength);
        Assert.Equal(1, restored.MinSeqNumber);
        Assert.Equal(500, restored.TopOverCostCount);
        Assert.Equal("T-", restored.IdPrefix);
        Assert.True(restored.LoopedSequence);
        Assert.Equal(3000, restored.MaxBackwardToleranceMs);
        Assert.False(restored.UseCustomEpoch);
        Assert.Equal("unit-test-generator", restored.GeneratorId);
        Assert.Equal(TimestampTypes.Milliseconds, restored.TimestampType);
        Assert.Equal(SnowflakeIdTypes.SnowFlakeMethod, restored.SnowflakeIdType);
    }

    /// <summary>
    /// JSON 字符串为空时按参数错误处理
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void FromJson_WhenNullOrEmpty_Throws(string? json)
    {
        Assert.Throws<ArgumentException>(() => { _ = SnowflakeIdOptions.FromJson(json!); });
    }

    /// <summary>
    /// JSON 格式非法时包装成 InvalidOperationException 抛出
    /// </summary>
    [Fact]
    public void FromJson_WhenMalformed_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => { _ = SnowflakeIdOptions.FromJson("{ this-is-not-json"); });

        Assert.Contains("从JSON加载配置失败", exception.Message);
    }

    /// <summary>
    /// 克隆应当产出独立实例，改克隆不能反噬原对象
    /// </summary>
    /// <remarks>
    /// 当前实现直接 <c>return this</c>，本用例按「克隆」的正确语义断言，失败即为源码缺陷。
    /// </remarks>
    [Fact]
    public void Clone_ReturnsIndependentInstance()
    {
        var options = SnowflakeIdOptions.HighWorkload(1);

        var clone = options.Clone();
        clone.WorkerId = 33;

        Assert.NotSame(options, clone);
        Assert.Equal(1, options.WorkerId);
    }
}
