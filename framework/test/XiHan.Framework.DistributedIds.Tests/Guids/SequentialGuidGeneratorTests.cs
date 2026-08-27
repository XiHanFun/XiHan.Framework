// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DistributedIds.Guids;

namespace XiHan.Framework.DistributedIds.Tests.Guids;

/// <summary>
/// 顺序 GUID 生成器的测试
/// </summary>
/// <remarks>
/// 三种 <see cref="SequentialGuidType"/> 的价值全在「按什么维度有序」上：
/// 末尾形式与二进制形式按字节序有序（分别落在尾 6 字节与首 6 字节），字符串形式按 ToString 文本序有序。
/// 因此排序用例必须按各自的比较维度断言，不能统统拿 Guid.CompareTo 糊过去。
/// 同一毫秒内的两个 GUID 只有随机段不同、没有顺序可言，所以取样之间必须真实拉开时间。
/// </remarks>
public class SequentialGuidGeneratorTests
{
    /// <summary>
    /// 取样之间的间隔，必须大于系统时钟的毫秒分辨率
    /// </summary>
    private const int SampleGapMilliseconds = 40;

    /// <summary>
    /// 配置为空时构造直接失败
    /// </summary>
    [Fact]
    public void Constructor_WhenOptionsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => { _ = new SequentialGuidGenerator(null!); });
    }

    /// <summary>
    /// 连续生成的 GUID 互不相同且不是空 GUID
    /// </summary>
    [Fact]
    public void NextId_ProducesDistinctNonEmptyGuids()
    {
        var generator = new SequentialGuidGenerator(SequentialGuidOptions.Default());

        var ids = generator.NextIds(500);

        Assert.Equal(500, ids.Distinct().Count());
        Assert.All(ids, id => Assert.NotEqual(Guid.Empty, id));
    }

    /// <summary>
    /// 静态入口对三种类型都能产出互不相同的 GUID
    /// </summary>
    [Theory]
    [InlineData(SequentialGuidType.SequentialAsString)]
    [InlineData(SequentialGuidType.SequentialAsBinary)]
    [InlineData(SequentialGuidType.SequentialAtEnd)]
    public void NextGuid_Static_ProducesDistinctGuids(SequentialGuidType guidType)
    {
        var guids = Enumerable.Range(0, 200).Select(_ => SequentialGuidGenerator.NextGuid(guidType)).ToArray();

        Assert.Equal(200, guids.Distinct().Count());
        Assert.All(guids, guid => Assert.NotEqual(Guid.Empty, guid));
    }

    /// <summary>
    /// 批量数量不是正数时拒绝生成
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NextIds_WhenCountNotPositive_Throws(int count)
    {
        var generator = new SequentialGuidGenerator(SequentialGuidOptions.Default());

        Assert.Throws<ArgumentException>(() => { _ = generator.NextIds(count); });
        Assert.Throws<ArgumentException>(() => { _ = generator.NextIdStrings(count); });
    }

    /// <summary>
    /// 末尾形式随时间推移在「尾 6 字节」上递增
    /// </summary>
    [Fact]
    public void SequentialAtEnd_IsOrderedByTrailingBytes()
    {
        var earlier = SequentialGuidGenerator.NextGuid(SequentialGuidType.SequentialAtEnd);
        Thread.Sleep(SampleGapMilliseconds);
        var later = SequentialGuidGenerator.NextGuid(SequentialGuidType.SequentialAtEnd);

        Assert.True(CompareSegment(earlier, later, 10) < 0, "末尾形式的尾 6 字节未随时间递增");
    }

    /// <summary>
    /// 二进制形式随时间推移在「首 6 字节」上递增
    /// </summary>
    [Fact]
    public void SequentialAsBinary_IsOrderedByLeadingBytes()
    {
        var earlier = SequentialGuidGenerator.NextGuid(SequentialGuidType.SequentialAsBinary);
        Thread.Sleep(SampleGapMilliseconds);
        var later = SequentialGuidGenerator.NextGuid(SequentialGuidType.SequentialAsBinary);

        Assert.True(CompareSegment(earlier, later, 0) < 0, "二进制形式的首 6 字节未随时间递增");
    }

    /// <summary>
    /// 字符串形式随时间推移在文本前缀上递增
    /// </summary>
    /// <remarks>
    /// 前 13 个字符（第一段 8 位 + 连字符 + 第二段 4 位）正好承载 6 字节时间戳，字符串排序只看这一段。
    /// </remarks>
    [Fact]
    public void SequentialAsString_IsOrderedByTextPrefix()
    {
        var earlier = SequentialGuidGenerator.NextGuid(SequentialGuidType.SequentialAsString);
        Thread.Sleep(SampleGapMilliseconds);
        var later = SequentialGuidGenerator.NextGuid(SequentialGuidType.SequentialAsString);

        var earlierPrefix = earlier.ToString()[..13];
        var laterPrefix = later.ToString()[..13];

        Assert.True(string.CompareOrdinal(earlierPrefix, laterPrefix) < 0, $"字符串形式的文本前缀未随时间递增：{earlierPrefix} -> {laterPrefix}");
    }

    /// <summary>
    /// 三种类型都能把生成时刻反解回来
    /// </summary>
    [Theory]
    [InlineData(SequentialGuidType.SequentialAsString)]
    [InlineData(SequentialGuidType.SequentialAsBinary)]
    [InlineData(SequentialGuidType.SequentialAtEnd)]
    public void ExtractTime_ReturnsGenerationMoment(SequentialGuidType guidType)
    {
        var generator = new SequentialGuidGenerator(new SequentialGuidOptions
        {
            DefaultSequentialGuidType = guidType
        });
        var before = DateTime.UtcNow;

        var extracted = generator.ExtractTime(generator.NextId());

        Assert.InRange(extracted, before.AddSeconds(-10), DateTime.UtcNow.AddSeconds(10));
    }

    /// <summary>
    /// 顺序 GUID 不承载机器码、序列号与数据中心标识，恒返回 0
    /// </summary>
    [Fact]
    public void ExtractNumericFields_AlwaysReturnZero()
    {
        var generator = new SequentialGuidGenerator(SequentialGuidOptions.Default());
        var id = generator.NextId();

        Assert.Equal(0, generator.ExtractWorkerId(id));
        Assert.Equal(0, generator.ExtractSequence(id));
        Assert.Equal(0, generator.ExtractDataCenterId(id));
    }

    /// <summary>
    /// 生成器类型字符串固定为 SequentialGuid
    /// </summary>
    [Fact]
    public void GetGeneratorType_IsSequentialGuid()
    {
        var generator = new SequentialGuidGenerator(SequentialGuidOptions.Default());

        Assert.Equal("SequentialGuid", generator.GetGeneratorType());
    }

    /// <summary>
    /// 状态字典记录当前类型、累计数量与最后一个 GUID
    /// </summary>
    [Fact]
    public void GetStats_TracksGeneratedCountAndLastGuid()
    {
        var generator = new SequentialGuidGenerator(SequentialGuidOptions.AsString());

        var first = generator.NextId();
        var last = generator.NextId();
        var stats = generator.GetStats();

        Assert.Equal("SequentialGuid", (string)stats["GeneratorType"]);
        Assert.Equal("SequentialAsString", (string)stats["GuidType"]);
        Assert.Equal(2L, (long)stats["GeneratedCount"]);
        Assert.Equal(last, (Guid)stats["LastGeneratedGuid"]);
        Assert.NotEqual(first, last);
    }

    /// <summary>
    /// 未生成任何 GUID 时状态字典给出空 GUID 与零计数
    /// </summary>
    [Fact]
    public void GetStats_BeforeAnyGeneration_IsEmptyState()
    {
        var generator = new SequentialGuidGenerator(SequentialGuidOptions.Default());

        var stats = generator.GetStats();

        Assert.Equal(0L, (long)stats["GeneratedCount"]);
        Assert.Equal(Guid.Empty, (Guid)stats["LastGeneratedGuid"]);
        Assert.Equal("SequentialAtEnd", (string)stats["GuidType"]);
    }

    /// <summary>
    /// 多线程并发生成时不出现重复，且计数与总量一致
    /// </summary>
    [Fact]
    public async Task NextId_UnderConcurrency_ProducesUniqueIds()
    {
        const int workerCount = 8;
        const int perWorker = 300;
        var generator = new SequentialGuidGenerator(SequentialGuidOptions.Default());

        var tasks = Enumerable.Range(0, workerCount)
            .Select(_ => Task.Run(() =>
            {
                var ids = new Guid[perWorker];
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
        Assert.Equal((long)(workerCount * perWorker), (long)generator.GetStats()["GeneratedCount"]);
    }

    /// <summary>
    /// 字符串形式就是标准 GUID 文本
    /// </summary>
    [Fact]
    public void NextIdString_IsParsableGuidText()
    {
        var generator = new SequentialGuidGenerator(SequentialGuidOptions.Default());

        var text = generator.NextIdString();

        Assert.True(Guid.TryParse(text, out var parsed));
        Assert.NotEqual(Guid.Empty, parsed);
    }

    /// <summary>
    /// 异步入口与同步入口给出同样的契约
    /// </summary>
    [Fact]
    public async Task AsyncApis_MatchSynchronousContract()
    {
        var generator = new SequentialGuidGenerator(SequentialGuidOptions.Default());

        var id = await generator.NextIdAsync();
        var idString = await generator.NextIdStringAsync();
        var ids = await generator.NextIdsAsync(5);
        var idStrings = await generator.NextIdStringsAsync(5);

        Assert.NotEqual(Guid.Empty, id);
        Assert.True(Guid.TryParse(idString, out _));
        Assert.Equal(5, ids.Length);
        Assert.Equal(5, ids.Distinct().Count());
        Assert.Equal(5, idStrings.Length);
        Assert.All(idStrings, value => Assert.True(Guid.TryParse(value, out _)));
    }

    /// <summary>
    /// 按字节比较两个 GUID 从 offset 开始的 6 字节时间戳段
    /// </summary>
    private static int CompareSegment(Guid first, Guid second, int offset)
    {
        var left = first.ToByteArray();
        var right = second.ToByteArray();

        for (var index = offset; index < offset + 6; index++)
        {
            var comparison = left[index].CompareTo(right[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }
}
