using JetBrains.Annotations;

namespace XiHan.Framework.DistributedIds.Test;

/// <summary>
/// ID生成器选项测试
/// </summary>
[TestSubject(typeof(IdGeneratorOptions))]
public class IdGeneratorOptionsTest
{
    [Fact(DisplayName = "默认选项测试")]
    public void DefaultOptions_ShouldHaveValidValues()
    {
        // Arrange
        var options = new IdGeneratorOptions();

        // Assert
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), options.BaseTime);
        Assert.Equal(0, options.WorkerId);
        Assert.Equal(6, options.WorkerIdBitLength);
        Assert.Equal(6, options.SeqBitLength);
        Assert.Equal(63, options.MaxSeqNumber);
        Assert.Equal(5, options.MinSeqNumber);
        Assert.Equal(2000, options.TopOverCostCount);
        Assert.Equal(2, options.TimestampType);
        Assert.Equal(IdGeneratorOptions.SnowFlakeMethod, options.Method);
        Assert.Equal(0, options.DataCenterId);
        Assert.Equal(5, options.DataCenterIdBitLength);
        Assert.Equal(0, options.IdLength);
        Assert.Empty(options.IdPrefix);
        Assert.False(options.LoopedSequence);
        Assert.Equal(10000, options.MaxBackwardToleranceMs);
        Assert.True(options.UseCustomEpoch);
        Assert.NotNull(options.GeneratorId);
        Assert.NotEmpty(options.GeneratorId);
    }

    [Fact(DisplayName = "自定义选项测试")]
    public void CustomOptions_ShouldSetValuesCorrectly()
    {
        // Arrange
        var options = new IdGeneratorOptions
        {
            BaseTime = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            WorkerId = 1,
            WorkerIdBitLength = 8,
            SeqBitLength = 8,
            MaxSeqNumber = 100,
            MinSeqNumber = 10,
            TopOverCostCount = 1000,
            TimestampType = 1,
            Method = IdGeneratorOptions.ClassicSnowFlakeMethod,
            DataCenterId = 2,
            DataCenterIdBitLength = 6
        };

        // Assert
        Assert.Equal(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc), options.BaseTime);
        Assert.Equal(1, options.WorkerId);
        Assert.Equal(8, options.WorkerIdBitLength);
        Assert.Equal(8, options.SeqBitLength);
        Assert.Equal(100, options.MaxSeqNumber);
        Assert.Equal(10, options.MinSeqNumber);
        Assert.Equal(1000, options.TopOverCostCount);
        Assert.Equal(1, options.TimestampType);
        Assert.Equal(IdGeneratorOptions.ClassicSnowFlakeMethod, options.Method);
        Assert.Equal(2, options.DataCenterId);
        Assert.Equal(6, options.DataCenterIdBitLength);
    }

    [Fact(DisplayName = "WorkerId范围测试")]
    public void WorkerId_ShouldBeWithinValidRange()
    {
        // Arrange
        var options = new IdGeneratorOptions
        {
            // Act & Assert
            WorkerId = 0
        };
        options.WorkerId = ushort.MaxValue;
    }

    [Fact(DisplayName = "WorkerIdBitLength范围测试")]
    public void WorkerIdBitLength_ShouldBeWithinValidRange()
    {
        // Arrange
        var options = new IdGeneratorOptions();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.WorkerIdBitLength = 0);
        Assert.Throws<ArgumentException>(() => options.WorkerIdBitLength = 16);
        options.WorkerIdBitLength = 1;
        options.WorkerIdBitLength = 15;
    }

    [Fact(DisplayName = "SeqBitLength范围测试")]
    public void SeqBitLength_ShouldBeWithinValidRange()
    {
        // Arrange
        var options = new IdGeneratorOptions();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.SeqBitLength = 2);
        Assert.Throws<ArgumentException>(() => options.SeqBitLength = 22);
        options.SeqBitLength = 3;
        options.SeqBitLength = 21;
    }

    [Fact(DisplayName = "MaxSeqNumber范围测试")]
    public void MaxSeqNumber_ShouldBeWithinValidRange()
    {
        // Arrange
        var options = new IdGeneratorOptions
        {
            SeqBitLength = 6
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.MaxSeqNumber = -1);
        Assert.Throws<ArgumentException>(() => options.MaxSeqNumber = 64);
        options.MaxSeqNumber = 0;
        options.MaxSeqNumber = 63;
    }

    [Fact(DisplayName = "MinSeqNumber范围测试")]
    public void MinSeqNumber_ShouldBeWithinValidRange()
    {
        // Arrange
        var options = new IdGeneratorOptions();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.MinSeqNumber = -1);
        Assert.Throws<ArgumentException>(() => options.MinSeqNumber = 128);
        options.MinSeqNumber = 0;
        options.MinSeqNumber = 127;
    }

    [Fact(DisplayName = "TopOverCostCount范围测试")]
    public void TopOverCostCount_ShouldBeWithinValidRange()
    {
        // Arrange
        var options = new IdGeneratorOptions();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.TopOverCostCount = -1);
        Assert.Throws<ArgumentException>(() => options.TopOverCostCount = 10001);
        options.TopOverCostCount = 0;
        options.TopOverCostCount = 10000;
    }

    [Fact(DisplayName = "TimestampType范围测试")]
    public void TimestampType_ShouldBeWithinValidRange()
    {
        // Arrange
        var options = new IdGeneratorOptions();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.TimestampType = 0);
        Assert.Throws<ArgumentException>(() => options.TimestampType = 3);
        options.TimestampType = 1;
        options.TimestampType = 2;
    }

    [Fact(DisplayName = "Method范围测试")]
    public void Method_ShouldBeWithinValidRange()
    {
        // Arrange
        var options = new IdGeneratorOptions();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Method = 0);
        Assert.Throws<ArgumentException>(() => options.Method = 3);
        options.Method = IdGeneratorOptions.SnowFlakeMethod;
        options.Method = IdGeneratorOptions.ClassicSnowFlakeMethod;
    }

    [Fact(DisplayName = "DataCenterId范围测试")]
    public void DataCenterId_ShouldBeWithinValidRange()
    {
        // Arrange
        var options = new IdGeneratorOptions
        {
            // Act & Assert
            DataCenterId = 0
        };
        options.DataCenterId = 31;
    }

    [Fact(DisplayName = "WorkerIdBitLength和SeqBitLength总和测试")]
    public void WorkerIdBitLengthAndSeqBitLengthSum_ShouldNotExceed22()
    {
        // Arrange
        var options = new IdGeneratorOptions
        {
            // Act & Assert
            WorkerIdBitLength = 6,
            SeqBitLength = 16
        };
        Assert.Throws<ArgumentException>(() => options.SeqBitLength = 17);
    }

    [Fact(DisplayName = "DataCenterIdBitLength范围测试")]
    public void DataCenterIdBitLength_ShouldBeWithinValidRange()
    {
        // Arrange
        var options = new IdGeneratorOptions();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.DataCenterIdBitLength = 0);
        Assert.Throws<ArgumentException>(() => options.DataCenterIdBitLength = 11);
        options.DataCenterIdBitLength = 1;
        options.DataCenterIdBitLength = 10;
    }

    [Fact(DisplayName = "IdLength范围测试")]
    public void IdLength_ShouldBeWithinValidRange()
    {
        // Arrange
        var options = new IdGeneratorOptions();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.IdLength = 1);
        Assert.Throws<ArgumentException>(() => options.IdLength = 9);
        Assert.Throws<ArgumentException>(() => options.IdLength = 21);
        options.IdLength = 0;
        options.IdLength = 10;
        options.IdLength = 15;
        options.IdLength = 20;
    }

    [Fact(DisplayName = "MaxBackwardToleranceMs范围测试")]
    public void MaxBackwardToleranceMs_ShouldBeWithinValidRange()
    {
        // Arrange
        var options = new IdGeneratorOptions();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.MaxBackwardToleranceMs = -1);
        Assert.Throws<ArgumentException>(() => options.MaxBackwardToleranceMs = 60001);
        options.MaxBackwardToleranceMs = 0;
        options.MaxBackwardToleranceMs = 60000;
    }

    [Fact(DisplayName = "LoopedSequence测试")]
    public void LoopedSequence_ShouldSetCorrectly()
    {
        // Arrange
        var options = new IdGeneratorOptions();

        // Act & Assert
        Assert.False(options.LoopedSequence); // 默认值应为false
        options.LoopedSequence = true;
        Assert.True(options.LoopedSequence);
    }

    [Fact(DisplayName = "UseCustomEpoch测试")]
    public void UseCustomEpoch_ShouldSetCorrectly()
    {
        // Arrange
        var options = new IdGeneratorOptions();

        // Act & Assert
        Assert.True(options.UseCustomEpoch); // 默认值应为true
        options.UseCustomEpoch = false;
        Assert.False(options.UseCustomEpoch);
    }

    [Fact(DisplayName = "GeneratorId测试")]
    public void GeneratorId_ShouldSetCorrectly()
    {
        // Arrange
        var options = new IdGeneratorOptions();
        var testId = "test-generator-id";

        // Act & Assert
        Assert.NotNull(options.GeneratorId); // 默认值不应为null
        Assert.NotEmpty(options.GeneratorId); // 默认值不应为空
        options.GeneratorId = testId;
        Assert.Equal(testId, options.GeneratorId);
        options.GeneratorId = null;
        Assert.NotNull(options.GeneratorId); // 设置null应转为默认值而不是null
    }

    [Fact(DisplayName = "IdPrefix测试")]
    public void IdPrefix_ShouldSetCorrectly()
    {
        // Arrange
        var options = new IdGeneratorOptions();
        var testPrefix = "TEST_";

        // Act & Assert
        Assert.Empty(options.IdPrefix); // 默认值应为空字符串
        options.IdPrefix = testPrefix;
        Assert.Equal(testPrefix, options.IdPrefix);
        options.IdPrefix = null;
        Assert.Empty(options.IdPrefix); // 设置null应转为空字符串
    }
}
