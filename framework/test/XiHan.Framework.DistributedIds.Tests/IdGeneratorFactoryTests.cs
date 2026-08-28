// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DistributedIds.Guids;
using XiHan.Framework.DistributedIds.NanoIds;
using XiHan.Framework.DistributedIds.SnowflakeIds;

namespace XiHan.Framework.DistributedIds.Tests;

/// <summary>
/// ID 生成器工厂的测试
/// </summary>
/// <remarks>
/// 工厂是使用方唯一的入口，每个方法都对应一份预设配置。
/// 这里逐个断言「解析出的具体实现类型 + 该预设最有辨识度的可观测特征」，
/// 防止预设被改错却因为返回接口而无人察觉。
/// </remarks>
public class IdGeneratorFactoryTests
{
    /// <summary>
    /// 传入自定义配置时创建雪花生成器
    /// </summary>
    [Fact]
    public void CreateSnowflakeIdGenerator_WithOptions_ReturnsSnowflakeGenerator()
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator(SnowflakeIdOptions.LowWorkload(1));

        Assert.IsType<SnowflakeIdGenerator>(generator);
        Assert.True(generator.NextId() > 0);
    }

    /// <summary>
    /// 三档负载预设都落到雪花漂移算法
    /// </summary>
    [Fact]
    public void CreateSnowflakeIdGenerator_WorkloadPresets_UseDriftAlgorithm()
    {
        var low = IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload(1);
        var medium = IdGeneratorFactory.CreateSnowflakeIdGenerator_MediumWorkload(1);
        var high = IdGeneratorFactory.CreateSnowflakeIdGenerator_HighWorkload(1);

        Assert.IsType<SnowflakeIdGenerator>(low);
        Assert.IsType<SnowflakeIdGenerator>(medium);
        Assert.IsType<SnowflakeIdGenerator>(high);
        Assert.Equal("SnowflakeId (SnowFlakeMethod)", low.GetGeneratorType());
        Assert.Equal("SnowflakeId (SnowFlakeMethod)", medium.GetGeneratorType());
        Assert.Equal("SnowflakeId (SnowFlakeMethod)", high.GetGeneratorType());
    }

    /// <summary>
    /// 负载预设都能反解出传入的机器码
    /// </summary>
    [Fact]
    public void CreateSnowflakeIdGenerator_WorkloadPresets_KeepWorkerId()
    {
        var low = IdGeneratorFactory.CreateSnowflakeIdGenerator_LowWorkload(13);
        var medium = IdGeneratorFactory.CreateSnowflakeIdGenerator_MediumWorkload(21);
        var high = IdGeneratorFactory.CreateSnowflakeIdGenerator_HighWorkload(34);

        Assert.Equal(13, low.ExtractWorkerId(low.NextId()));
        Assert.Equal(21, medium.ExtractWorkerId(medium.NextId()));
        Assert.Equal(34, high.ExtractWorkerId(high.NextId()));
    }

    /// <summary>
    /// 短唯一标识预设输出定长 10 位字符串
    /// </summary>
    [Fact]
    public void CreateSnowflakeIdGenerator_ShortId_ProducesTenCharacterStrings()
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_ShortId(1);

        Assert.IsType<SnowflakeIdGenerator>(generator);
        Assert.All(generator.NextIdStrings(5), id => Assert.Equal(10, id.Length));
    }

    /// <summary>
    /// 前缀预设把前缀带进字符串形式
    /// </summary>
    [Fact]
    public void CreateSnowflakeIdGenerator_PrefixedId_KeepsPrefix()
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_PrefixedId("INV_", 1);

        Assert.All(generator.NextIdStrings(5), id => Assert.StartsWith("INV_", id));
    }

    /// <summary>
    /// 经典预设落到传统雪花算法并保留数据中心标识
    /// </summary>
    [Fact]
    public void CreateSnowflakeIdGenerator_Classic_UsesClassicAlgorithm()
    {
        var generator = IdGeneratorFactory.CreateSnowflakeIdGenerator_Classic(5, 7);

        var id = generator.NextId();

        Assert.IsType<SnowflakeIdGenerator>(generator);
        Assert.Equal("SnowflakeId (ClassicSnowFlakeMethod)", generator.GetGeneratorType());
        Assert.Equal(5, generator.ExtractWorkerId(id));
        Assert.Equal(7, generator.ExtractDataCenterId(id));
    }

    /// <summary>
    /// 传入自定义配置时创建 NanoID 生成器
    /// </summary>
    [Fact]
    public void CreateNanoIdGenerator_WithOptions_ReturnsNanoIdGenerator()
    {
        var generator = IdGeneratorFactory.CreateNanoIdGenerator(NanoIdOptions.UrlSafe(12));

        Assert.IsType<NanoIdGenerator>(generator);
        Assert.Equal("NanoId", generator.GetGeneratorType());
        Assert.Equal(12, generator.NextIdString().Length);
    }

    /// <summary>
    /// NanoID 各预设的默认长度与字符集符合文档
    /// </summary>
    [Fact]
    public void CreateNanoIdGenerator_Presets_UseDocumentedSizeAndAlphabet()
    {
        AssertNanoIdShape(IdGeneratorFactory.CreateNanoIdGenerator_Numeric(), 10, NanoIdOptions.NumbersAlphabet);
        AssertNanoIdShape(IdGeneratorFactory.CreateNanoIdGenerator_Lowercase(), 16, NanoIdOptions.LowercaseAlphabet);
        AssertNanoIdShape(IdGeneratorFactory.CreateNanoIdGenerator_Uppercase(), 16, NanoIdOptions.UppercaseAlphabet);
        AssertNanoIdShape(IdGeneratorFactory.CreateNanoIdGenerator_UrlSafe(), 21, NanoIdOptions.UrlSafeAlphabet);
        AssertNanoIdShape(IdGeneratorFactory.CreateNanoIdGenerator_Safe(), 21, NanoIdOptions.SafeAlphabet);
        AssertNanoIdShape(IdGeneratorFactory.CreateNanoIdGenerator_Hex(), 32, NanoIdOptions.HexAlphabet);
    }

    /// <summary>
    /// 自定义字符集预设按调用方给的字符集与长度出串
    /// </summary>
    [Fact]
    public void CreateNanoIdGenerator_Custom_UsesGivenAlphabet()
    {
        var generator = IdGeneratorFactory.CreateNanoIdGenerator_Custom("abcdef", 12);

        AssertNanoIdShape(generator, 12, "abcdef");
    }

    /// <summary>
    /// 传入自定义配置时创建顺序 GUID 生成器
    /// </summary>
    [Fact]
    public void CreateSequentialGuidGenerator_WithOptions_ReturnsSequentialGuidGenerator()
    {
        var generator = IdGeneratorFactory.CreateSequentialGuidGenerator(SequentialGuidOptions.AsBinary());

        Assert.IsType<SequentialGuidGenerator>(generator);
        Assert.Equal("SequentialGuid", generator.GetGeneratorType());
        Assert.Equal("SequentialAsBinary", (string)generator.GetStats()["GuidType"]);
    }

    /// <summary>
    /// 顺序 GUID 各预设落到对应的排序形态
    /// </summary>
    [Fact]
    public void CreateSequentialGuidGenerator_Presets_UseMatchingGuidType()
    {
        Assert.Equal("SequentialAtEnd", (string)IdGeneratorFactory.CreateSequentialGuidGenerator_Default().GetStats()["GuidType"]);
        Assert.Equal("SequentialAsString", (string)IdGeneratorFactory.CreateSequentialGuidGenerator_AsString().GetStats()["GuidType"]);
        Assert.Equal("SequentialAsBinary", (string)IdGeneratorFactory.CreateSequentialGuidGenerator_AsBinary().GetStats()["GuidType"]);
        Assert.Equal("SequentialAtEnd", (string)IdGeneratorFactory.CreateSequentialGuidGenerator_AtEnd().GetStats()["GuidType"]);
    }

    /// <summary>
    /// 工厂每次调用都返回互相独立的新实例
    /// </summary>
    [Fact]
    public void Factory_ReturnsIndependentInstances()
    {
        var first = IdGeneratorFactory.CreateSnowflakeIdGenerator_HighWorkload(1);
        var second = IdGeneratorFactory.CreateSnowflakeIdGenerator_HighWorkload(1);

        Assert.NotSame(first, second);
    }

    /// <summary>
    /// 断言 NanoID 生成器输出的长度与字符集
    /// </summary>
    private static void AssertNanoIdShape(IDistributedIdGenerator<long> generator, int expectedSize, string expectedAlphabet)
    {
        Assert.IsType<NanoIdGenerator>(generator);

        var id = generator.NextIdString();

        Assert.Equal(expectedSize, id.Length);
        Assert.All(id, character => Assert.True(expectedAlphabet.Contains(character), $"字符 {character} 不在预设字符集内"));
    }
}
