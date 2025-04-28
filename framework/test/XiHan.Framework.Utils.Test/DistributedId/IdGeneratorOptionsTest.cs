using JetBrains.Annotations;
using XiHan.Framework.Utils.DistributedId;

namespace XiHan.Framework.Utils.Test.DistributedId;

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
        Assert.Equal(0, options.MaxSeqNumber);
        Assert.Equal(5, options.MinSeqNumber);
        Assert.Equal(2000, options.TopOverCostCount);
        Assert.Equal(2, options.TimestampType);
        Assert.Equal(IdGeneratorOptions.SnowFlakeMethod, options.Method);
        Assert.Equal(0, options.DataCenterId);
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
            DataCenterId = 2
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
}
