using JetBrains.Annotations;

namespace XiHan.Framework.DistributedIds.Test;

/// <summary>
/// 雪花算法ID生成器测试
/// </summary>
[TestSubject(typeof(SnowflakeIdGenerator))]
public class SnowflakeIdGeneratorTest
{
    [Fact(DisplayName = "默认配置创建雪花ID生成器测试")]
    public void Constructor_WithDefaultOptions_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var options = new IdGeneratorOptions();
        var generator = new SnowflakeIdGenerator(options);

        // Assert
        Assert.NotNull(generator);
        var stats = generator.GetStats();
        Assert.Equal(options.GeneratorId, stats["GeneratorId"]);
        Assert.Equal((long)options.DataCenterId, stats["DataCenterId"]);
        Assert.Equal((long)options.WorkerId, stats["WorkerId"]);
    }

    [Fact(DisplayName = "自定义配置创建雪花ID生成器测试")]
    public void Constructor_WithCustomOptions_ShouldInitializeCorrectly()
    {
        // Arrange
        var options = new IdGeneratorOptions
        {
            DataCenterId = 3,
            WorkerId = 5,
            Method = IdGeneratorOptions.SnowFlakeMethod
        };

        // Act
        var generator = new SnowflakeIdGenerator(options);

        // Assert
        Assert.NotNull(generator);
        var stats = generator.GetStats();
        Assert.Equal(3L, stats["DataCenterId"]);
        Assert.Equal(5L, stats["WorkerId"]);
        Assert.Equal("雪花算法(漂移)", stats["GeneratorType"]);
    }

    [Fact(DisplayName = "经典雪花算法生成器类型测试")]
    public void GetGeneratorType_WithClassicMethod_ShouldReturnCorrectType()
    {
        // Arrange
        var options = new IdGeneratorOptions
        {
            Method = IdGeneratorOptions.ClassicSnowFlakeMethod
        };
        var generator = new SnowflakeIdGenerator(options);

        // Act
        var type = generator.GetGeneratorType();

        // Assert
        Assert.Equal("雪花算法(经典)", type);
    }

    [Fact(DisplayName = "漂移雪花算法生成器类型测试")]
    public void GetGeneratorType_WithSnowflakeMethod_ShouldReturnCorrectType()
    {
        // Arrange
        var options = new IdGeneratorOptions
        {
            Method = IdGeneratorOptions.SnowFlakeMethod
        };
        var generator = new SnowflakeIdGenerator(options);

        // Act
        var type = generator.GetGeneratorType();

        // Assert
        Assert.Equal("雪花算法(漂移)", type);
    }

    [Fact(DisplayName = "生成唯一ID测试")]
    public void NextId_ShouldGenerateUniqueIds()
    {
        // Arrange
        var options = new IdGeneratorOptions();
        var generator = new SnowflakeIdGenerator(options);
        var count = 1000;
        var ids = new HashSet<long>();

        // Act
        for (var i = 0; i < count; i++)
        {
            ids.Add(generator.NextId());
        }

        // Assert
        Assert.Equal(count, ids.Count);
    }

    [Fact(DisplayName = "生成字符串ID测试")]
    public void NextIdString_ShouldGenerateValidStringId()
    {
        // Arrange
        var options = new IdGeneratorOptions();
        var generator = new SnowflakeIdGenerator(options);

        // Act
        var idString = generator.NextIdString();

        // Assert
        Assert.NotEmpty(idString);
        Assert.True(long.TryParse(idString, out _));
    }

    [Fact(DisplayName = "生成字符串ID带前缀测试")]
    public void NextIdString_WithPrefix_ShouldIncludePrefix()
    {
        // Arrange
        var options = new IdGeneratorOptions
        {
            IdPrefix = "TEST_"
        };
        var generator = new SnowflakeIdGenerator(options);

        // Act
        var idString = generator.NextIdString();

        // Assert
        Assert.StartsWith("TEST_", idString);
    }

    [Fact(DisplayName = "生成固定长度字符串ID测试")]
    public void NextIdString_WithIdLength_ShouldHaveCorrectLength()
    {
        // Arrange
        var options = new IdGeneratorOptions
        {
            IdLength = 10
        };
        var generator = new SnowflakeIdGenerator(options);

        // Act
        var idString = generator.NextIdString();

        // Assert
        Assert.Equal(10, idString.Length);
    }

    [Fact(DisplayName = "批量生成ID测试")]
    public void NextIds_ShouldGenerateSpecifiedNumberOfIds()
    {
        // Arrange
        var options = new IdGeneratorOptions();
        var generator = new SnowflakeIdGenerator(options);
        var count = 5;

        // Act
        var ids = generator.NextIds(count);

        // Assert
        Assert.Equal(count, ids.Length);
        Assert.Equal(count, ids.Distinct().Count());
    }

    [Fact(DisplayName = "批量生成字符串ID测试")]
    public void NextIdStrings_ShouldGenerateSpecifiedNumberOfStringIds()
    {
        // Arrange
        var options = new IdGeneratorOptions();
        var generator = new SnowflakeIdGenerator(options);
        var count = 5;

        // Act
        var idStrings = generator.NextIdStrings(count);

        // Assert
        Assert.Equal(count, idStrings.Length);
        Assert.Equal(count, idStrings.Distinct().Count());
    }

    [Fact(DisplayName = "异步生成ID测试")]
    public async Task NextIdAsync_ShouldGenerateId()
    {
        // Arrange
        var options = new IdGeneratorOptions();
        var generator = new SnowflakeIdGenerator(options);

        // Act
        var id = await generator.NextIdAsync();

        // Assert
        Assert.NotEqual(0, id);
    }

    [Fact(DisplayName = "异步生成字符串ID测试")]
    public async Task NextIdStringAsync_ShouldGenerateStringId()
    {
        // Arrange
        var options = new IdGeneratorOptions();
        var generator = new SnowflakeIdGenerator(options);

        // Act
        var idString = await generator.NextIdStringAsync();

        // Assert
        Assert.NotEmpty(idString);
    }

    [Fact(DisplayName = "异步批量生成ID测试")]
    public async Task NextIdsAsync_ShouldGenerateSpecifiedNumberOfIds()
    {
        // Arrange
        var options = new IdGeneratorOptions();
        var generator = new SnowflakeIdGenerator(options);
        var count = 5;

        // Act
        var ids = await generator.NextIdsAsync(count);

        // Assert
        Assert.Equal(count, ids.Length);
        Assert.Equal(count, ids.Distinct().Count());
    }

    [Fact(DisplayName = "异步批量生成字符串ID测试")]
    public async Task NextIdStringsAsync_ShouldGenerateSpecifiedNumberOfStringIds()
    {
        // Arrange
        var options = new IdGeneratorOptions();
        var generator = new SnowflakeIdGenerator(options);
        var count = 5;

        // Act
        var idStrings = await generator.NextIdStringsAsync(count);

        // Assert
        Assert.Equal(count, idStrings.Length);
        Assert.Equal(count, idStrings.Distinct().Count());
    }

    [Fact(DisplayName = "从ID提取时间测试")]
    public void ExtractTime_ShouldReturnCorrectTime()
    {
        // Arrange
        var options = new IdGeneratorOptions();
        var generator = new SnowflakeIdGenerator(options);
        var beforeGeneration = DateTime.UtcNow;
        var id = generator.NextId();
        var afterGeneration = DateTime.UtcNow;

        // Act
        var extractedTime = generator.ExtractTime(id);

        // Assert
        Assert.True(extractedTime >= beforeGeneration.AddSeconds(-1));
        Assert.True(extractedTime <= afterGeneration.AddSeconds(1));
    }

    [Fact(DisplayName = "从ID提取工作机器ID测试")]
    public void ExtractWorkerId_ShouldReturnCorrectWorkerId()
    {
        // Arrange
        var workerId = 7;
        var options = new IdGeneratorOptions
        {
            WorkerId = (ushort)workerId
        };
        var generator = new SnowflakeIdGenerator(options);
        var id = generator.NextId();

        // Act
        var extractedWorkerId = generator.ExtractWorkerId(id);

        // Assert
        Assert.Equal(workerId, extractedWorkerId);
    }

    [Fact(DisplayName = "从ID提取序列号测试")]
    public void ExtractSequence_ShouldReturnValidSequence()
    {
        // Arrange
        var options = new IdGeneratorOptions();
        var generator = new SnowflakeIdGenerator(options);
        var id = generator.NextId();

        // Act
        var sequence = generator.ExtractSequence(id);

        // Assert
        Assert.True(sequence >= 0);
        Assert.True(sequence < Math.Pow(2, options.SeqBitLength));
    }

    [Fact(DisplayName = "从ID提取数据中心ID测试")]
    public void ExtractDataCenterId_ShouldReturnCorrectDataCenterId()
    {
        // Arrange
        var dataCenterId = 3;
        var options = new IdGeneratorOptions
        {
            DataCenterId = (byte)dataCenterId,
            Method = IdGeneratorOptions.ClassicSnowFlakeMethod // 注意：必须使用经典雪花算法才能正确提取数据中心ID
        };
        var generator = new SnowflakeIdGenerator(options);
        var id = generator.NextId();

        // Act
        var extractedDataCenterId = generator.ExtractDataCenterId(id);

        // Assert
        Assert.Equal(dataCenterId, extractedDataCenterId);
    }

    [Fact(DisplayName = "获取生成器状态测试")]
    public void GetStats_ShouldReturnValidDictionary()
    {
        // Arrange
        var options = new IdGeneratorOptions();
        var generator = new SnowflakeIdGenerator(options);

        // Act
        var stats = generator.GetStats();

        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.ContainsKey("GeneratorId"));
        Assert.True(stats.ContainsKey("GeneratorType"));
        Assert.True(stats.ContainsKey("WorkerId"));
        Assert.True(stats.ContainsKey("DataCenterId"));
        Assert.True(stats.ContainsKey("LastTimestamp"));
        Assert.True(stats.ContainsKey("CurrentSequence"));
        Assert.True(stats.ContainsKey("OverCostCount"));
        Assert.True(stats.ContainsKey("BaseTime"));
        Assert.True(stats.ContainsKey("TimestampType"));
    }

    [Fact(DisplayName = "批量生成ID参数验证测试")]
    public void NextIds_WithInvalidCount_ShouldThrowException()
    {
        // Arrange
        var options = new IdGeneratorOptions();
        var generator = new SnowflakeIdGenerator(options);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => generator.NextIds(0));
        Assert.Throws<ArgumentException>(() => generator.NextIds(-1));
    }

    [Fact(DisplayName = "批量生成字符串ID参数验证测试")]
    public void NextIdStrings_WithInvalidCount_ShouldThrowException()
    {
        // Arrange
        var options = new IdGeneratorOptions();
        var generator = new SnowflakeIdGenerator(options);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => generator.NextIdStrings(0));
        Assert.Throws<ArgumentException>(() => generator.NextIdStrings(-1));
    }

    [Fact(DisplayName = "异步批量生成ID参数验证测试")]
    public async Task NextIdsAsync_WithInvalidCount_ShouldThrowException()
    {
        // Arrange
        var options = new IdGeneratorOptions();
        var generator = new SnowflakeIdGenerator(options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => generator.NextIdsAsync(0));
        await Assert.ThrowsAsync<ArgumentException>(() => generator.NextIdsAsync(-1));
    }

    [Fact(DisplayName = "异步批量生成字符串ID参数验证测试")]
    public async Task NextIdStringsAsync_WithInvalidCount_ShouldThrowException()
    {
        // Arrange
        var options = new IdGeneratorOptions();
        var generator = new SnowflakeIdGenerator(options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => generator.NextIdStringsAsync(0));
        await Assert.ThrowsAsync<ArgumentException>(() => generator.NextIdStringsAsync(-1));
    }
}
