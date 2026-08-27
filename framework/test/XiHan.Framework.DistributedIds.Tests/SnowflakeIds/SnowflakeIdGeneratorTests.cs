// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DistributedIds.SnowflakeIds;

namespace XiHan.Framework.DistributedIds.Tests.SnowflakeIds;

/// <summary>
/// 雪花漂移算法唯一标识生成器的测试
/// </summary>
/// <remarks>
/// 只断言对外承诺的契约：单调递增、并发唯一、字段可反解、批量与异步入口一致，
/// 不锁死具体位布局——位布局由选项决定，属实现细节。
/// 时钟回拨分支依赖真实系统时钟，无法在不改源码的前提下稳定触发，未覆盖。
/// </remarks>
public class SnowflakeIdGeneratorTests
{
    /// <summary>
    /// 同一实例连续生成的 ID 严格递增
    /// </summary>
    [Fact]
    public void NextId_Sequentially_IsStrictlyIncreasing()
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_HighWorkload(1);

        var previous = generator.NextId();
        for (var index = 0; index < 2000; index++)
        {
            var current = generator.NextId();

            Assert.True(current > previous, $"第 {index} 次生成未严格递增：{previous} -> {current}");
            previous = current;
        }
    }

    /// <summary>
    /// 传统雪花算法下同样保持严格递增
    /// </summary>
    [Fact]
    public void NextId_ClassicAlgorithm_IsStrictlyIncreasing()
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_Classic(1, 1);

        var previous = generator.NextId();
        for (var index = 0; index < 1000; index++)
        {
            var current = generator.NextId();

            Assert.True(current > previous, $"第 {index} 次生成未严格递增：{previous} -> {current}");
            previous = current;
        }
    }

    /// <summary>
    /// 多线程并发生成时不出现重复 ID
    /// </summary>
    /// <remarks>
    /// 生成器对外宣称线程安全（内部 Lock 串行化），这里用真并发压出重复才有意义。
    /// </remarks>
    [Fact]
    public async Task NextId_UnderConcurrency_ProducesUniqueIds()
    {
        const int workerCount = 8;
        const int perWorker = 250;
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_HighWorkload(1);

        var tasks = Enumerable.Range(0, workerCount)
            .Select(_ => Task.Run(() =>
            {
                var ids = new long[perWorker];
                for (var index = 0; index < perWorker; index++)
                {
                    ids[index] = generator.NextId();
                }

                return ids;
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        var all = results.SelectMany(ids => ids).ToArray();

        Assert.Equal(workerCount * perWorker, all.Length);
        Assert.Equal(all.Length, all.Distinct().Count());
    }

    /// <summary>
    /// 不同机器码的生成器产出的 ID 互不相交
    /// </summary>
    [Fact]
    public void NextId_DifferentWorkerIds_ProduceDisjointIds()
    {
        var first = IdGeneratorFactory.CreateSnowflakeIdGenerator_HighWorkload(11);
        var second = IdGeneratorFactory.CreateSnowflakeIdGenerator_HighWorkload(22);

        var firstIds = first.NextIds(200);
        var secondIds = second.NextIds(200);

        Assert.Empty(firstIds.Intersect(secondIds));
    }

    /// <summary>
    /// 批量生成返回请求数量且内部不重复
    /// </summary>
    [Fact]
    public void NextIds_ReturnsRequestedCountWithoutDuplicates()
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_MediumWorkload(1);

        var ids = generator.NextIds(300);

        Assert.Equal(300, ids.Length);
        Assert.Equal(300, ids.Distinct().Count());
    }

    /// <summary>
    /// 批量数量不是正数时拒绝生成
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NextIds_WhenCountNotPositive_Throws(int count)
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload(1);

        Assert.Throws<ArgumentException>(() => { _ = generator.NextIds(count); });
    }

    /// <summary>
    /// 字符串批量数量不是正数时拒绝生成
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NextIdStrings_WhenCountNotPositive_Throws(int count)
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload(1);

        Assert.Throws<ArgumentException>(() => { _ = generator.NextIdStrings(count); });
    }

    /// <summary>
    /// 字符串批量生成返回请求数量
    /// </summary>
    [Fact]
    public void NextIdStrings_ReturnsRequestedCount()
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload(1);

        var ids = generator.NextIdStrings(20);

        Assert.Equal(20, ids.Length);
        Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));
    }

    /// <summary>
    /// 从 ID 中反解出的时间落在生成时刻附近
    /// </summary>
    [Fact]
    public void ExtractTime_ReturnsGenerationMoment()
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_HighWorkload(1);
        var before = DateTime.UtcNow;

        var extracted = generator.ExtractTime(generator.NextId());

        Assert.InRange(extracted, before.AddSeconds(-10), DateTime.UtcNow.AddSeconds(10));
    }

    /// <summary>
    /// 从 ID 中反解出配置的机器码
    /// </summary>
    [Fact]
    public void ExtractWorkerId_ReturnsConfiguredWorkerId()
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_HighWorkload(37);

        Assert.Equal(37, generator.ExtractWorkerId(generator.NextId()));
    }

    /// <summary>
    /// 反解出的序列号落在配置的最小/最大序列数之间
    /// </summary>
    [Fact]
    public void ExtractSequence_StaysWithinConfiguredRange()
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_HighWorkload(1);

        foreach (var id in generator.NextIds(200))
        {
            // 默认最小序列数 5、最大序列数 63
            Assert.InRange(generator.ExtractSequence(id), 5, 63);
        }
    }

    /// <summary>
    /// 传统雪花算法下机器码与数据中心标识都能反解
    /// </summary>
    [Fact]
    public void ExtractIds_ClassicAlgorithm_ReturnsWorkerAndDataCenter()
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_Classic(9, 3);

        var id = generator.NextId();

        Assert.Equal(9, generator.ExtractWorkerId(id));
        Assert.Equal(3, generator.ExtractDataCenterId(id));
    }

    /// <summary>
    /// 生成器类型字符串带出当前算法
    /// </summary>
    [Fact]
    public void GetGeneratorType_ReflectsAlgorithm()
    {
        var drift = IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload(1);
        var classic = IdGeneratorFactory.CreateSnowflakeIdGenerator_Classic(1, 1);

        Assert.Equal("SnowflakeId (SnowFlakeMethod)", drift.GetGeneratorType());
        Assert.Equal("SnowflakeId (ClassicSnowFlakeMethod)", classic.GetGeneratorType());
    }

    /// <summary>
    /// 状态字典给出运维排障需要的键
    /// </summary>
    [Fact]
    public void GetStats_ExposesRuntimeState()
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_HighWorkload(12);
        _ = generator.NextId();

        var stats = generator.GetStats();

        Assert.Contains("GeneratorId", stats.Keys);
        Assert.Contains("LastTimestamp", stats.Keys);
        Assert.Contains("CurrentSequence", stats.Keys);
        Assert.Contains("OverCostCount", stats.Keys);
        Assert.Contains("BaseTime", stats.Keys);
        Assert.Equal("SnowflakeId (SnowFlakeMethod)", (string)stats["GeneratorType"]);
        Assert.Equal(12L, (long)stats["WorkerId"]);
        Assert.Equal(0L, (long)stats["DataCenterId"]);
        Assert.Equal("毫秒级", (string)stats["TimestampType"]);
        Assert.True((long)stats["LastTimestamp"] > 0);
    }

    /// <summary>
    /// 配置了前缀时字符串形式带上前缀
    /// </summary>
    [Fact]
    public void NextIdString_WithPrefix_KeepsPrefix()
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_PrefixedId("ORD-", 1);

        var id = generator.NextIdString();

        Assert.StartsWith("ORD-", id);
        Assert.True(id.Length > "ORD-".Length);
    }

    /// <summary>
    /// 配置了固定长度时字符串形式长度被对齐
    /// </summary>
    [Fact]
    public void NextIdString_WithFixedLength_HasExactLength()
    {
        // 短唯一标识预设把 IdLength 固定为 10
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_ShortId(1);

        Assert.All(generator.NextIdStrings(10), id => Assert.Equal(10, id.Length));
    }

    /// <summary>
    /// 未配置长度与前缀时字符串形式就是长整型的十进制文本
    /// </summary>
    [Fact]
    public void NextIdString_WithoutPrefixAndLength_IsPlainNumber()
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload(1);

        var id = generator.NextIdString();

        Assert.True(long.TryParse(id, out var parsed));
        Assert.True(parsed > 0);
    }

    /// <summary>
    /// 异步入口与同步入口给出同样的契约
    /// </summary>
    [Fact]
    public async Task AsyncApis_MatchSynchronousContract()
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_HighWorkload(1);

        var id = await generator.NextIdAsync();
        var idString = await generator.NextIdStringAsync();
        var ids = await generator.NextIdsAsync(5);
        var idStrings = await generator.NextIdStringsAsync(5);

        Assert.True(id > 0);
        Assert.False(string.IsNullOrEmpty(idString));
        Assert.Equal(5, ids.Length);
        Assert.Equal(5, ids.Distinct().Count());
        Assert.Equal(5, idStrings.Length);
    }

    /// <summary>
    /// 异步批量数量不是正数时同样拒绝
    /// </summary>
    [Fact]
    public async Task NextIdsAsync_WhenCountNotPositive_Throws()
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload(1);

        await Assert.ThrowsAsync<ArgumentException>(() => generator.NextIdsAsync(0));
    }

    /// <summary>
    /// 基准时间晚于当前时间时构造直接失败，避免生成负偏移的 ID
    /// </summary>
    [Fact]
    public void Constructor_WhenBaseTimeInFuture_Throws()
    {
        var options = new SnowflakeIdOptions
        {
            WorkerId = 1,
            BaseTime = DateTime.UtcNow.AddDays(30)
        };

        var exception = Record.Exception(() => { _ = new SnowflakeIdGenerator(options); });

        Assert.NotNull(exception);
        Assert.Contains("基准时间", exception.Message);
    }

    /// <summary>
    /// 秒级时间戳配置下依然能正常生成并反解时间
    /// </summary>
    [Fact]
    public void SecondsTimestamp_StillGeneratesAndExtractsTime()
    {
        var options = new SnowflakeIdOptions
        {
            WorkerId = 1,
            TimestampType = TimestampTypes.Seconds
        };
        var generator = new SnowflakeIdGenerator(options);
        var before = DateTime.UtcNow;

        var id = generator.NextId();

        Assert.True(id > 0);
        Assert.InRange(generator.ExtractTime(id), before.AddSeconds(-10), DateTime.UtcNow.AddSeconds(10));
    }
}
