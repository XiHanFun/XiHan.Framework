// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.DistributedIds.Extensions.DependencyInjection;
using XiHan.Framework.DistributedIds.Guids;
using XiHan.Framework.DistributedIds.NanoIds;
using XiHan.Framework.DistributedIds.SnowflakeIds;
using XiHan.Framework.DistributedIds.Sqids;

namespace XiHan.Framework.DistributedIds.Tests.Extensions.DependencyInjection;

/// <summary>
/// 分布式唯一标识服务注册扩展的测试
/// </summary>
/// <remarks>
/// 这段注册的关键语义是「基线默认值 + 配置覆盖」：先落高负载基线，再由配置节覆盖。
/// 覆盖顺序一旦颠倒，集群里每个节点都会退回 WorkerId=1 并产出重复 ID，
/// 所以「空配置走基线」和「配置生效」两条路径都必须验证。
/// </remarks>
public class XiHanDistributedIdsServiceCollectionExtensionsTests
{
    /// <summary>
    /// 注册方法返回同一个服务集合以支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanDistributedIds_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddXiHanDistributedIds(BuildConfiguration(new Dictionary<string, string?>()));

        Assert.Same(services, result);
    }

    /// <summary>
    /// 长整型生成器被注册为雪花实现的单例
    /// </summary>
    [Fact]
    public void AddXiHanDistributedIds_RegistersSnowflakeGeneratorAsSingleton()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>());

        var first = provider.GetRequiredService<IDistributedIdGenerator<long>>();
        var second = provider.GetRequiredService<IDistributedIdGenerator<long>>();

        Assert.IsType<SnowflakeIdGenerator>(first);
        Assert.Same(first, second);
    }

    /// <summary>
    /// GUID 生成器被注册为顺序 GUID 实现的单例
    /// </summary>
    [Fact]
    public void AddXiHanDistributedIds_RegistersSequentialGuidGeneratorAsSingleton()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>());

        var first = provider.GetRequiredService<IDistributedIdGenerator<Guid>>();
        var second = provider.GetRequiredService<IDistributedIdGenerator<Guid>>();

        Assert.IsType<SequentialGuidGenerator>(first);
        Assert.Same(first, second);
    }

    /// <summary>
    /// 配置节为空时雪花选项落在高负载基线上
    /// </summary>
    [Fact]
    public void AddXiHanDistributedIds_WithEmptyConfiguration_AppliesHighWorkloadBaseline()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>());

        var options = provider.GetRequiredService<IOptions<SnowflakeIdOptions>>().Value;

        Assert.Equal(6, options.WorkerIdBitLength);
        Assert.Equal(12, options.SeqBitLength);
        Assert.Equal(1, options.WorkerId);
    }

    /// <summary>
    /// 配置节写了机器码时覆盖基线，并真正作用到生成的 ID 上
    /// </summary>
    [Fact]
    public void AddXiHanDistributedIds_WithConfiguredWorkerId_OverridesBaseline()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{SnowflakeIdOptions.SectionName}:WorkerId"] = "42"
        });

        var options = provider.GetRequiredService<IOptions<SnowflakeIdOptions>>().Value;
        var generator = provider.GetRequiredService<IDistributedIdGenerator<long>>();

        Assert.Equal(42, options.WorkerId);
        Assert.Equal(42, generator.ExtractWorkerId(generator.NextId()));
    }

    /// <summary>
    /// 配置节写了前缀时作用到字符串形式的 ID 上
    /// </summary>
    [Fact]
    public void AddXiHanDistributedIds_WithConfiguredPrefix_AppliesToIdString()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{SnowflakeIdOptions.SectionName}:IdPrefix"] = "SN-"
        });

        var generator = provider.GetRequiredService<IDistributedIdGenerator<long>>();

        Assert.StartsWith("SN-", generator.NextIdString());
    }

    /// <summary>
    /// 配置节写了顺序 GUID 类型时覆盖默认的末尾形式
    /// </summary>
    [Fact]
    public void AddXiHanDistributedIds_WithConfiguredGuidType_OverridesDefault()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{SequentialGuidOptions.SectionName}:DefaultSequentialGuidType"] = "SequentialAsString"
        });

        var options = provider.GetRequiredService<IOptions<SequentialGuidOptions>>().Value;
        var generator = provider.GetRequiredService<IDistributedIdGenerator<Guid>>();

        Assert.Equal(SequentialGuidType.SequentialAsString, options.GetDefaultSequentialGuidType());
        Assert.Equal("SequentialAsString", (string)generator.GetStats()["GuidType"]);
    }

    /// <summary>
    /// 未配置顺序 GUID 类型时回落到末尾形式
    /// </summary>
    [Fact]
    public void AddXiHanDistributedIds_WithoutGuidType_FallsBackToAtEnd()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>());

        var generator = provider.GetRequiredService<IDistributedIdGenerator<Guid>>();

        Assert.Equal("SequentialAtEnd", (string)generator.GetStats()["GuidType"]);
    }

    /// <summary>
    /// NanoID 与 Sqids 的选项也被登记进容器，可以按默认值解析
    /// </summary>
    [Fact]
    public void AddXiHanDistributedIds_RegistersNanoIdAndSqidsOptions()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>());

        var nanoIdOptions = provider.GetRequiredService<IOptions<NanoIdOptions>>().Value;
        var sqidsOptions = provider.GetRequiredService<IOptions<SqidsOptions>>().Value;

        Assert.Equal(21, nanoIdOptions.Size);
        Assert.Equal(NanoIdOptions.DefaultAlphabet, nanoIdOptions.Alphabet);
        Assert.Equal(5, sqidsOptions.MinLength);
        Assert.NotEmpty(sqidsOptions.Alphabet);
        Assert.NotEmpty(sqidsOptions.BlockList);
    }

    /// <summary>
    /// 配置节写了 NanoID 长度时覆盖默认值
    /// </summary>
    [Fact]
    public void AddXiHanDistributedIds_WithConfiguredNanoIdSize_OverridesDefault()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{NanoIdOptions.SectionName}:Size"] = "8"
        });

        var options = provider.GetRequiredService<IOptions<NanoIdOptions>>().Value;

        Assert.Equal(8, options.Size);
    }

    /// <summary>
    /// 按给定配置项构建服务提供者
    /// </summary>
    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddXiHanDistributedIds(BuildConfiguration(settings));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 用内存字典构建配置
    /// </summary>
    private static IConfiguration BuildConfiguration(Dictionary<string, string?> settings)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
