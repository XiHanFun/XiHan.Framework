using JetBrains.Annotations;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

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
        var count = 500000;

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

    [Fact(DisplayName = "验证雪花算法生成重复ID所需时间测试", Skip = "耗时测试，仅在需要验证时运行")]
    public void GenerateDuplicateId_ShouldTakeLongTime()
    {
        // Arrange
        var options = new IdGeneratorOptions
        {
            // 使用漂移算法，提高生成速度
            Method = IdGeneratorOptions.SnowFlakeMethod,
            // 使用较小的序列位长度，增加重复可能性
            SeqBitLength = 8
        };
        var generator = new SnowflakeIdGenerator(options);

        var ids = new HashSet<long>(10_000_000); // 预分配一个较大的容量
        var duplicateFound = false;
        var duplicateId = 0L;
        var totalGeneratedCount = 0;

        var stopwatch = Stopwatch.StartNew();
        var maxRunTime = TimeSpan.FromMinutes(5); // 最多运行5分钟

        // Act
        try
        {
            while (!duplicateFound && stopwatch.Elapsed < maxRunTime)
            {
                var id = generator.NextId();
                totalGeneratedCount++;

                if (!ids.Add(id))
                {
                    // 找到重复ID
                    duplicateFound = true;
                    duplicateId = id;
                    break;
                }

                // 每生成100万个ID打印一次进度
                if (totalGeneratedCount % 1_000_000 == 0)
                {
                    Console.WriteLine($"已生成 {totalGeneratedCount:N0} 个ID，耗时: {stopwatch.Elapsed}");
                }
            }
        }
        finally
        {
            stopwatch.Stop();
        }

        // Assert
        if (duplicateFound)
        {
            Console.WriteLine($"在生成 {totalGeneratedCount:N0} 个ID后，发现重复ID: {duplicateId}，耗时: {stopwatch.Elapsed}");
            // 这里我们期望足够长的时间后才会出现重复
            Assert.True(totalGeneratedCount > 1_000_000, $"ID重复出现得太快: 仅生成 {totalGeneratedCount:N0} 个ID");
        }
        else
        {
            Console.WriteLine($"在最大运行时间 {maxRunTime} 内，生成了 {totalGeneratedCount:N0} 个ID，未发现重复");
            // 如果在最大运行时间内没有找到重复，测试也算通过
            Assert.True(totalGeneratedCount > 0);
        }
    }

    [Fact(DisplayName = "并发环境下验证雪花算法重复ID生成测试", Skip = "耗时测试，仅在需要验证时运行")]
    public void GenerateDuplicateIdInParallel_ShouldTakeLongTime()
    {
        // Arrange
        var options = new IdGeneratorOptions
        {
            Method = IdGeneratorOptions.SnowFlakeMethod
        };
        var generator = new SnowflakeIdGenerator(options);

        var ids = new ConcurrentDictionary<long, byte>();
        var duplicateFound = false;
        var duplicateId = 0L;
        var totalGeneratedCount = 0;

        var stopwatch = Stopwatch.StartNew();
        var maxRunTime = TimeSpan.FromMinutes(3); // 最多运行3分钟
        var concurrentTasks = 8; // 并发任务数

        // Act
        try
        {
            var tasks = new Task[concurrentTasks];
            var counters = new int[concurrentTasks];
            var duplicates = new long[concurrentTasks];
            var foundDuplicates = new bool[concurrentTasks];

            for (var i = 0; i < concurrentTasks; i++)
            {
                var taskId = i;
                tasks[i] = Task.Run(() =>
                {
                    while (!duplicateFound && stopwatch.Elapsed < maxRunTime)
                    {
                        var id = generator.NextId();
                        Interlocked.Increment(ref counters[taskId]);

                        if (!ids.TryAdd(id, 0))
                        {
                            duplicates[taskId] = id;
                            foundDuplicates[taskId] = true;
                            Interlocked.Exchange(ref duplicateFound, true);
                            break;
                        }
                    }
                });
            }

            // 等待所有任务完成或任一任务发现重复
            Task.WaitAll(tasks);

            // 计算总生成数量
            totalGeneratedCount = counters.Sum();

            // 查找第一个发现重复的任务
            for (var i = 0; i < concurrentTasks; i++)
            {
                if (foundDuplicates[i])
                {
                    duplicateId = duplicates[i];
                    break;
                }
            }
        }
        finally
        {
            stopwatch.Stop();
        }

        // Assert
        if (duplicateFound)
        {
            Console.WriteLine($"在并发({concurrentTasks}个线程)环境下，生成 {totalGeneratedCount:N0} 个ID后，发现重复ID: {duplicateId}，耗时: {stopwatch.Elapsed}");
            // 这里我们期望足够长的时间后才会出现重复
            Assert.True(totalGeneratedCount > 1_000_000, $"ID重复出现得太快: 仅生成 {totalGeneratedCount:N0} 个ID");
        }
        else
        {
            Console.WriteLine($"在最大运行时间 {maxRunTime} 内，并发({concurrentTasks}个线程)生成了 {totalGeneratedCount:N0} 个ID，未发现重复");
            // 如果在最大运行时间内没有找到重复，测试也算通过
            Assert.True(totalGeneratedCount > 0);
        }
    }

    [Fact(DisplayName = "1秒内生成10万个ID检查重复测试")]
    public void Generate100000IdsInOneSecond_ShouldNotHaveDuplicates()
    {
        // Arrange
        var options = new IdGeneratorOptions
        {
            // 使用漂移算法，提高生成速度
            Method = IdGeneratorOptions.SnowFlakeMethod
        };
        var generator = new SnowflakeIdGenerator(options);

        var ids = new ConcurrentBag<long>();
        var stopwatch = Stopwatch.StartNew();
        var targetDuration = TimeSpan.FromSeconds(1);
        var targetCount = 100000;

        // Act - 使用多线程并行生成ID
        Parallel.For(0, targetCount, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, _ =>
        {
            // 如果已经超过1秒，则不再生成
            if (stopwatch.Elapsed <= targetDuration)
            {
                ids.Add(generator.NextId());
            }
        });

        stopwatch.Stop();

        // 将并发集合转换为列表以便分析
        var idList = ids.ToList();
        var distinctIds = new HashSet<long>(idList);
        var actualCount = idList.Count;
        var elapsedMs = stopwatch.ElapsedMilliseconds;

        // 计算每秒生成速率
        var idsPerSecond = actualCount / (elapsedMs / 1000.0);

        // Assert
        Console.WriteLine($"在 {elapsedMs} 毫秒内生成了 {actualCount:N0} 个ID");
        Console.WriteLine($"生成速率: {idsPerSecond:N0} IDs/秒");

        if (actualCount < targetCount)
        {
            Console.WriteLine($"警告: 未能在1秒内生成目标数量 {targetCount:N0} 个ID");
        }

        // 验证没有重复
        Assert.Equal(distinctIds.Count, actualCount);

        // 验证生成的ID数量应该符合预期
        Assert.True(actualCount > 0, "至少应生成一些ID");

        // 理想情况下应该接近目标数量
        if (elapsedMs >= 1000)
        {
            // 如果用了超过1秒，期望至少达到目标的80%
            Assert.True(actualCount >= targetCount * 0.8,
                $"应至少生成目标数量的80%，实际生成: {actualCount:N0}/{targetCount:N0} ({actualCount * 100.0 / targetCount:F2}%)");
        }
    }

    [Fact(DisplayName = "1秒内高性能批量生成10万个ID检查重复测试")]
    public void Generate100000IdsInOneSecondOptimized_ShouldNotHaveDuplicates()
    {
        // Arrange
        var options = new IdGeneratorOptions
        {
            Method = IdGeneratorOptions.SnowFlakeMethod
        };
        var generator = new SnowflakeIdGenerator(options);

        var maxIds = 150000; // 增加目标数以确保能达到10万
        var batchSize = 10000; // 每次批量生成的数量
        var allIds = new List<long>(maxIds);

        var stopwatch = Stopwatch.StartNew();
        var targetDuration = TimeSpan.FromSeconds(1);

        // Act - 使用批量生成方法
        while (stopwatch.Elapsed < targetDuration && allIds.Count < maxIds)
        {
            // 计算本批次应生成的ID数量
            var currentBatchSize = Math.Min(batchSize, maxIds - allIds.Count);

            // 批量生成ID
            var batchIds = generator.NextIds(currentBatchSize);
            allIds.AddRange(batchIds);
        }

        stopwatch.Stop();

        // 截取1秒内生成的数量（但不超过目标10万）
        var actualIds = allIds.Take(Math.Min(allIds.Count, 100000)).ToList();
        var distinctIds = new HashSet<long>(actualIds);
        var actualCount = actualIds.Count;
        var elapsedMs = stopwatch.ElapsedMilliseconds;

        // 计算每秒生成速率
        var idsPerSecond = actualCount / (elapsedMs / 1000.0);

        // Assert
        Console.WriteLine($"在 {elapsedMs} 毫秒内生成了 {actualCount:N0} 个ID");
        Console.WriteLine($"生成速率: {idsPerSecond:N0} IDs/秒");

        // 验证没有重复
        Assert.Equal(distinctIds.Count, actualCount);
        Assert.True(actualCount > 0, "至少应生成一些ID");

        // 如果用了超过1秒，期望至少达到目标的80%
        Assert.True(actualCount >= 100000,
            $"警告: 未能在1秒内生成10万个ID，实际生成: {actualCount:N0}");
    }
}
