// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.DistributedIds.Guids;
using XiHan.Framework.DistributedIds.SnowflakeIds;

namespace XiHan.Framework.DistributedIds.Tests;

/// <summary>
/// 分布式唯一标识生成模块的测试
/// </summary>
/// <remarks>
/// 模块自身只做一件事：从服务集合里取出配置，再转交给 <c>AddXiHanDistributedIds</c>。
/// 因此这里验证的是「模块装配后容器里确实能解析出生成器」以及「配置真的被透传下去」。
/// </remarks>
public class XiHanDistributedIdsModuleTests
{
    /// <summary>
    /// 模块继承自框架模块基类，才能被模块系统发现
    /// </summary>
    [Fact]
    public void Module_DerivesFromXiHanModule()
    {
        Assert.IsAssignableFrom<XiHanModule>(new XiHanDistributedIdsModule());
    }

    /// <summary>
    /// 模块装配后两种生成器都能从容器解析出来
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersBothGenerators()
    {
        var services = BuildServicesWithConfiguration(new Dictionary<string, string?>());

        new XiHanDistributedIdsModule().ConfigureServices(new ServiceConfigurationContext(services));

        using var provider = services.BuildServiceProvider();

        Assert.IsType<SnowflakeIdGenerator>(provider.GetRequiredService<IDistributedIdGenerator<long>>());
        Assert.IsType<SequentialGuidGenerator>(provider.GetRequiredService<IDistributedIdGenerator<Guid>>());
    }

    /// <summary>
    /// 模块把服务集合里的配置真正透传给了生成器
    /// </summary>
    [Fact]
    public void ConfigureServices_PassesConfigurationThrough()
    {
        var services = BuildServicesWithConfiguration(new Dictionary<string, string?>
        {
            [$"{SnowflakeIdOptions.SectionName}:WorkerId"] = "17"
        });

        new XiHanDistributedIdsModule().ConfigureServices(new ServiceConfigurationContext(services));

        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<IDistributedIdGenerator<long>>();

        Assert.Equal(17, generator.ExtractWorkerId(generator.NextId()));
    }

    /// <summary>
    /// 构建带有配置单例的服务集合
    /// </summary>
    private static ServiceCollection BuildServicesWithConfiguration(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        return services;
    }
}
