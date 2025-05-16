using JetBrains.Annotations;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace XiHan.Framework.DistributedIds.Test;

/// <summary>
/// 雪花ID生成器测试
/// </summary>
[TestSubject(typeof(SnowflakeIdGenerator))]
public class SnowflakeIdGeneratorTest
{
    private readonly SnowflakeIdGenerator _generator;
    private readonly IdGeneratorOptions _options;

    public SnowflakeIdGeneratorTest()
    {
        _options = new IdGeneratorOptions
        {
            WorkerId = 1,
            DataCenterId = 1
        };
        _generator = new SnowflakeIdGenerator(_options);
    }

    [Fact(DisplayName = "生成唯一ID测试")]
    public async Task NextId_ShouldGenerateUniqueIds()
    {
        // Arrange
        var ids = new ConcurrentDictionary<long, bool>();
        var count = 10000;
        var tasks = new Task[count];

        // Act
        for (var i = 0; i < count; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                var id = _generator.NextId();
                ids.TryAdd(id, true);
            });
        }
        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(count, ids.Count);
    }

    [Fact(DisplayName = "ID递增测试")]
    public void NextId_ShouldGenerateIncreasingIds()
    {
        // Arrange
        var count = 1000;
        var ids = new long[count];

        // Act
        for (var i = 0; i < count; i++)
        {
            ids[i] = _generator.NextId();
        }

        // Assert
        for (var i = 1; i < count; i++)
        {
            Assert.True(ids[i] > ids[i - 1], $"ID {ids[i]} 应该大于 {ids[i - 1]}");
        }
    }

    [Fact(DisplayName = "ID格式测试")]
    public void NextId_ShouldGenerateValidFormat()
    {
        // Arrange
        var id = _generator.NextId();

        // Act
        var timestamp = _generator.ExtractTime(id);
        var workerId = _generator.ExtractWorkerId(id);
        var sequence = _generator.ExtractSequence(id);

        // Assert
        Assert.True(timestamp > DateTime.MinValue, "时间戳应该大于最小值");
        Assert.Equal(_options.WorkerId, workerId);
        Assert.True(sequence >= 0 && sequence <= (1 << _options.SeqBitLength) - 1, $"序列号应该在0-{(1 << _options.SeqBitLength) - 1}之间");
    }

    [Fact(DisplayName = "高并发测试")]
    public async Task NextId_UnderHighConcurrency_ShouldGenerateUniqueIds()
    {
        // Arrange
        var ids = new ConcurrentDictionary<long, bool>();
        var count = 100000;
        var tasks = new Task[count];
        var stopwatch = new Stopwatch();

        // Act
        stopwatch.Start();
        for (var i = 0; i < count; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                var id = _generator.NextId();
                ids.TryAdd(id, true);
            });
        }
        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        Assert.Equal(count, ids.Count);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, "生成100000个ID应该不超过5秒");
    }

    [Fact(DisplayName = "不同WorkerId生成ID测试")]
    public void NextId_WithDifferentWorkerIds_ShouldGenerateDifferentIds()
    {
        // Arrange
        var options1 = new IdGeneratorOptions { WorkerId = 1, DataCenterId = 1 };
        var options2 = new IdGeneratorOptions { WorkerId = 2, DataCenterId = 1 };
        var generator1 = new SnowflakeIdGenerator(options1);
        var generator2 = new SnowflakeIdGenerator(options2);

        // Act
        var id1 = generator1.NextId();
        var id2 = generator2.NextId();

        // Assert
        Assert.NotEqual(id1, id2);
    }

    [Fact(DisplayName = "不同DataCenterId生成ID测试")]
    public void NextId_WithDifferentDataCenterIds_ShouldGenerateDifferentIds()
    {
        // Arrange
        var options1 = new IdGeneratorOptions { WorkerId = 1, DataCenterId = 1 };
        var options2 = new IdGeneratorOptions { WorkerId = 1, DataCenterId = 2 };
        var generator1 = new SnowflakeIdGenerator(options1);
        var generator2 = new SnowflakeIdGenerator(options2);

        // Act
        var id1 = generator1.NextId();
        var id2 = generator2.NextId();

        // Assert
        Assert.NotEqual(id1, id2);
    }

    [Fact(DisplayName = "解析ID测试")]
    public void ParseId_ShouldReturnCorrectComponents()
    {
        // Arrange
        var id = _generator.NextId();

        // Act
        var timestamp = _generator.ExtractTime(id);
        var workerId = _generator.ExtractWorkerId(id);
        var sequence = _generator.ExtractSequence(id);

        // Assert
        Assert.True(timestamp > DateTime.MinValue);
        Assert.Equal(_options.WorkerId, workerId);
        Assert.True(sequence >= 0 && sequence <= (1 << _options.SeqBitLength) - 1);
    }
}
