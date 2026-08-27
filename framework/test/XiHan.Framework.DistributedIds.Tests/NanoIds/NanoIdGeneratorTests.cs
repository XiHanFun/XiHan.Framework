// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DistributedIds.NanoIds;

namespace XiHan.Framework.DistributedIds.Tests.NanoIds;

/// <summary>
/// NanoID 生成器的测试
/// </summary>
/// <remarks>
/// NanoID 的两条对外契约是分开的：<c>NextIdString</c> 走加密随机、只保证长度与字符集；
/// <c>NextId</c> 走「时间戳 + 序列号」，保证单调递增且可反解时间。两条线分别断言，不要混谈。
/// </remarks>
public class NanoIdGeneratorTests
{
    /// <summary>
    /// 配置为空时构造直接失败
    /// </summary>
    [Fact]
    public void Constructor_WhenOptionsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => { _ = new NanoIdGenerator(null!); });
    }

    /// <summary>
    /// 字符串形式严格遵守配置的长度与字符集
    /// </summary>
    [Theory]
    [InlineData(NanoIdOptions.NumbersAlphabet, 10)]
    [InlineData(NanoIdOptions.LowercaseAlphabet, 16)]
    [InlineData(NanoIdOptions.UppercaseAlphabet, 16)]
    [InlineData(NanoIdOptions.UrlSafeAlphabet, 21)]
    [InlineData(NanoIdOptions.SafeAlphabet, 21)]
    [InlineData(NanoIdOptions.HexAlphabet, 32)]
    public void NextIdString_UsesConfiguredAlphabetAndSize(string alphabet, int size)
    {
        var generator = new NanoIdGenerator(new NanoIdOptions
        {
            Size = size,
            Alphabet = alphabet
        });

        foreach (var id in generator.NextIdStrings(20))
        {
            Assert.Equal(size, id.Length);
            Assert.All(id, character => Assert.True(alphabet.Contains(character), $"字符 {character} 不在配置的字符集内"));
        }
    }

    /// <summary>
    /// 只有两个字符的最小字符集也能正常出串
    /// </summary>
    [Fact]
    public void NextIdString_WithMinimalAlphabet_StillWorks()
    {
        var generator = new NanoIdGenerator(new NanoIdOptions
        {
            Size = 30,
            Alphabet = "ab"
        });

        var id = generator.NextIdString();

        Assert.Equal(30, id.Length);
        Assert.All(id, character => Assert.True(character is 'a' or 'b'));
    }

    /// <summary>
    /// 默认长度 21 的随机串在批量生成时不重复
    /// </summary>
    [Fact]
    public void NextIdStrings_AreDistinct()
    {
        var generator = new NanoIdGenerator(NanoIdOptions.UrlSafe());

        var ids = generator.NextIdStrings(500);

        Assert.Equal(500, ids.Length);
        Assert.Equal(500, ids.Distinct().Count());
    }

    /// <summary>
    /// 批量数量不是正数时拒绝生成
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void NextIds_WhenCountNotPositive_Throws(int count)
    {
        var generator = new NanoIdGenerator(new NanoIdOptions());

        Assert.Throws<ArgumentException>(() => { _ = generator.NextIds(count); });
        Assert.Throws<ArgumentException>(() => { _ = generator.NextIdStrings(count); });
    }

    /// <summary>
    /// 数值形式的 ID 连续生成时严格递增
    /// </summary>
    [Fact]
    public void NextId_Sequentially_IsStrictlyIncreasing()
    {
        var generator = new NanoIdGenerator(new NanoIdOptions());

        var previous = generator.NextId();
        for (var index = 0; index < 1000; index++)
        {
            var current = generator.NextId();

            Assert.True(current > previous, $"第 {index} 次生成未严格递增：{previous} -> {current}");
            previous = current;
        }
    }

    /// <summary>
    /// 多线程并发生成数值形式 ID 时不出现重复
    /// </summary>
    [Fact]
    public async Task NextId_UnderConcurrency_ProducesUniqueIds()
    {
        const int workerCount = 8;
        const int perWorker = 250;
        var generator = new NanoIdGenerator(new NanoIdOptions());

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
    /// 从数值形式 ID 中反解出的时间落在生成时刻附近
    /// </summary>
    [Fact]
    public void ExtractTime_ReturnsGenerationMoment()
    {
        var generator = new NanoIdGenerator(new NanoIdOptions());
        var before = DateTime.UtcNow;

        var extracted = generator.ExtractTime(generator.NextId());

        Assert.InRange(extracted, before.AddSeconds(-10), DateTime.UtcNow.AddSeconds(10));
    }

    /// <summary>
    /// 同一毫秒内生成的 ID 序列号逐个加一
    /// </summary>
    [Fact]
    public void ExtractSequence_WithinSameMillisecond_IncrementsByOne()
    {
        var generator = new NanoIdGenerator(new NanoIdOptions());
        var ids = generator.NextIds(50);

        var comparedPairs = 0;
        for (var index = 1; index < ids.Length; index++)
        {
            if (generator.ExtractTime(ids[index]) != generator.ExtractTime(ids[index - 1]))
            {
                continue;
            }

            Assert.Equal(generator.ExtractSequence(ids[index - 1]) + 1, generator.ExtractSequence(ids[index]));
            comparedPairs++;
        }

        Assert.True(comparedPairs > 0, "50 次生成全部跨越了毫秒边界，未能验证同毫秒内的序列号递增");
    }

    /// <summary>
    /// NanoID 不承载机器码与数据中心标识，恒返回 0
    /// </summary>
    [Fact]
    public void ExtractWorkerIdAndDataCenterId_AlwaysReturnZero()
    {
        var generator = new NanoIdGenerator(new NanoIdOptions());
        var id = generator.NextId();

        Assert.Equal(0, generator.ExtractWorkerId(id));
        Assert.Equal(0, generator.ExtractDataCenterId(id));
    }

    /// <summary>
    /// 生成器类型字符串固定为 NanoId
    /// </summary>
    [Fact]
    public void GetGeneratorType_IsNanoId()
    {
        var generator = new NanoIdGenerator(new NanoIdOptions());

        Assert.Equal("NanoId", generator.GetGeneratorType());
    }

    /// <summary>
    /// 状态字典带出当前字符集与长度配置
    /// </summary>
    [Fact]
    public void GetStats_ExposesAlphabetConfiguration()
    {
        var generator = new NanoIdGenerator(NanoIdOptions.Hex(12));
        _ = generator.NextId();

        var stats = generator.GetStats();

        Assert.Equal("NanoId", (string)stats["GeneratorType"]);
        Assert.Equal(12, (int)stats["Size"]);
        Assert.Equal(NanoIdOptions.HexAlphabet, (string)stats["Alphabet"]);
        Assert.Equal(16, (int)stats["AlphabetSize"]);
        Assert.Equal("Milliseconds", (string)stats["TimestampType"]);
        Assert.Equal(NanoIdOptions.DefaultStartTime, (DateTime)stats["StartTime"]);
        Assert.Contains("LastTimestamp", stats.Keys);
        Assert.Contains("CurrentSequence", stats.Keys);
    }

    /// <summary>
    /// 秒级时间戳配置下时间仍可反解
    /// </summary>
    [Fact]
    public void SecondsTimestamp_StillExtractsTime()
    {
        var generator = new NanoIdGenerator(new NanoIdOptions
        {
            TimestampType = TimestampTypes.Seconds
        });
        var before = DateTime.UtcNow;

        var extracted = generator.ExtractTime(generator.NextId());

        Assert.InRange(extracted, before.AddSeconds(-10), DateTime.UtcNow.AddSeconds(10));
    }

    /// <summary>
    /// 异步入口与同步入口给出同样的契约
    /// </summary>
    [Fact]
    public async Task AsyncApis_MatchSynchronousContract()
    {
        var generator = new NanoIdGenerator(NanoIdOptions.UrlSafe());

        var id = await generator.NextIdAsync();
        var idString = await generator.NextIdStringAsync();
        var ids = await generator.NextIdsAsync(5);
        var idStrings = await generator.NextIdStringsAsync(5);

        Assert.True(id > 0);
        Assert.Equal(21, idString.Length);
        Assert.Equal(5, ids.Length);
        Assert.Equal(5, ids.Distinct().Count());
        Assert.Equal(5, idStrings.Length);
        Assert.All(idStrings, value => Assert.Equal(21, value.Length));
    }
}
