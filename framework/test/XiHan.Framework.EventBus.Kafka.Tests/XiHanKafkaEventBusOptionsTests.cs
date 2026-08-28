// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace XiHan.Framework.EventBus.Kafka.Tests;

/// <summary>
/// Kafka 分布式事件总线选项测试
/// </summary>
/// <remarks>
/// 这些值全部是对外契约：配置节名与键名一旦漂移，宿主 appsettings 里的配置会静默失效，
/// 事件总线仍然会用默认值连上 localhost 而不报错——所以按名逐项锁死绑定结果，而不是只测一两个字段。
/// </remarks>
public class XiHanKafkaEventBusOptionsTests
{
    /// <summary>
    /// 配置节名称是宿主 appsettings 依赖的字面量，不允许漂移
    /// </summary>
    [Fact]
    public void SectionName_IsStableConfigurationKey()
    {
        Assert.Equal("XiHan:EventBus:Kafka", XiHanKafkaEventBusOptions.SectionName);
    }

    /// <summary>
    /// 默认值指向本机单节点 Broker，且开箱即用
    /// </summary>
    /// <remarks>
    /// 默认值决定了「什么都不配也能在开发机跑起来」，同时也决定了配置写错时会连到哪里。
    /// </remarks>
    [Fact]
    public void Defaults_TargetLocalSingleNodeBroker()
    {
        var options = new XiHanKafkaEventBusOptions();

        Assert.Equal("localhost:9092", options.BootstrapServers);
        Assert.Equal("XiHan.EventBus", options.TopicName);
        Assert.Equal("XiHan.EventBus", options.GroupId);
        Assert.Equal("earliest", options.AutoOffsetReset);
        Assert.True(options.EnsureTopicExists);
        Assert.Equal(1, options.TopicPartitionCount);
        Assert.Equal((short)1, options.TopicReplicationFactor);
    }

    /// <summary>
    /// 默认主题名与消费者组名一致
    /// </summary>
    /// <remarks>
    /// 所有事件写入同一主题、同组竞争消费是该实现的核心设计，两者默认同名是这一设计的直接体现。
    /// </remarks>
    [Fact]
    public void Defaults_TopicNameAndGroupIdAreTheSame()
    {
        var options = new XiHanKafkaEventBusOptions();

        Assert.Equal(options.TopicName, options.GroupId);
    }

    /// <summary>
    /// 所有选项均可写，且实例之间互不影响
    /// </summary>
    [Fact]
    public void Properties_AreWritableAndPerInstance()
    {
        var options = new XiHanKafkaEventBusOptions
        {
            BootstrapServers = "broker-a:9092,broker-b:9092",
            TopicName = "Custom.Topic",
            GroupId = "Custom.Group",
            AutoOffsetReset = "latest",
            EnsureTopicExists = false,
            TopicPartitionCount = 12,
            TopicReplicationFactor = 3
        };

        Assert.Equal("broker-a:9092,broker-b:9092", options.BootstrapServers);
        Assert.Equal("Custom.Topic", options.TopicName);
        Assert.Equal("Custom.Group", options.GroupId);
        Assert.Equal("latest", options.AutoOffsetReset);
        Assert.False(options.EnsureTopicExists);
        Assert.Equal(12, options.TopicPartitionCount);
        Assert.Equal((short)3, options.TopicReplicationFactor);

        Assert.Equal("localhost:9092", new XiHanKafkaEventBusOptions().BootstrapServers);
    }

    /// <summary>
    /// 配置节中的每一个键都能绑定到对应属性
    /// </summary>
    [Fact]
    public void Bind_FromConfigurationSection_AppliesEveryOption()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            [$"{XiHanKafkaEventBusOptions.SectionName}:BootstrapServers"] = "kafka-1:9092,kafka-2:9092",
            [$"{XiHanKafkaEventBusOptions.SectionName}:TopicName"] = "MyApp.Events",
            [$"{XiHanKafkaEventBusOptions.SectionName}:GroupId"] = "MyApp.Consumers",
            [$"{XiHanKafkaEventBusOptions.SectionName}:AutoOffsetReset"] = "latest",
            [$"{XiHanKafkaEventBusOptions.SectionName}:EnsureTopicExists"] = "false",
            [$"{XiHanKafkaEventBusOptions.SectionName}:TopicPartitionCount"] = "6",
            [$"{XiHanKafkaEventBusOptions.SectionName}:TopicReplicationFactor"] = "3"
        });

        Assert.Equal("kafka-1:9092,kafka-2:9092", options.BootstrapServers);
        Assert.Equal("MyApp.Events", options.TopicName);
        Assert.Equal("MyApp.Consumers", options.GroupId);
        Assert.Equal("latest", options.AutoOffsetReset);
        Assert.False(options.EnsureTopicExists);
        Assert.Equal(6, options.TopicPartitionCount);
        Assert.Equal((short)3, options.TopicReplicationFactor);
    }

    /// <summary>
    /// 只配置部分键时，其余键保留默认值
    /// </summary>
    /// <remarks>
    /// 绑定器对缺失键是「不覆盖」而非「置零」，副本数被悄悄置 0 会让建主题直接失败。
    /// </remarks>
    [Fact]
    public void Bind_WithPartialSection_KeepsDefaultsForMissingKeys()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            [$"{XiHanKafkaEventBusOptions.SectionName}:BootstrapServers"] = "kafka-1:9092"
        });

        Assert.Equal("kafka-1:9092", options.BootstrapServers);
        Assert.Equal("XiHan.EventBus", options.TopicName);
        Assert.Equal("XiHan.EventBus", options.GroupId);
        Assert.Equal("earliest", options.AutoOffsetReset);
        Assert.True(options.EnsureTopicExists);
        Assert.Equal(1, options.TopicPartitionCount);
        Assert.Equal((short)1, options.TopicReplicationFactor);
    }

    /// <summary>
    /// 配置源里没有该节时，得到的仍是一份完整的默认选项
    /// </summary>
    [Fact]
    public void Bind_WhenSectionAbsent_KeepsAllDefaults()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["Unrelated:Key"] = "value"
        });

        Assert.Equal("localhost:9092", options.BootstrapServers);
        Assert.Equal("XiHan.EventBus", options.TopicName);
        Assert.True(options.EnsureTopicExists);
    }

    /// <summary>
    /// 大小写不敏感的键名同样能绑定
    /// </summary>
    /// <remarks>
    /// 配置系统按不区分大小写匹配键名，宿主里常见 camelCase 写法，这里确认它不会静默失效。
    /// </remarks>
    [Theory]
    [InlineData("BootstrapServers")]
    [InlineData("bootstrapServers")]
    [InlineData("BOOTSTRAPSERVERS")]
    public void Bind_KeyNamesAreCaseInsensitive(string key)
    {
        var options = Bind(new Dictionary<string, string?>
        {
            [$"{XiHanKafkaEventBusOptions.SectionName}:{key}"] = "kafka-case:9092"
        });

        Assert.Equal("kafka-case:9092", options.BootstrapServers);
    }

    /// <summary>
    /// 按 Kafka 配置节绑定选项
    /// </summary>
    /// <param name="settings">配置键值</param>
    /// <returns>绑定后的选项</returns>
    private static XiHanKafkaEventBusOptions Bind(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.Configure<XiHanKafkaEventBusOptions>(
            configuration.GetSection(XiHanKafkaEventBusOptions.SectionName));

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<XiHanKafkaEventBusOptions>>().Value;
    }
}
