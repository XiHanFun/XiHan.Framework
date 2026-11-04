// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Kafka.Tests;

/// <summary>
/// Kafka 选项：配置优先于绑定、默认值、透传属性与校验
/// </summary>
public sealed class KafkaMessagingOptionsTests
{
    /// <summary>
    /// 用内存键值对构建配置
    /// </summary>
    private static IConfiguration Build(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Fact]
    public void FromConfigurationAndBinding_PrefersBinding_AndValidates()
    {
        var binding = Maybe.Some(new BindingValue("stream", "stream", "bound:9092", new Dictionary<string, string>()));
        var bound = KafkaMessagingOptions.FromConfiguration(Build(), binding);
        Assert.Equal("bound:9092", bound.BootstrapServers);
        Assert.Equal("xihan", bound.GroupId);
        Assert.Equal("xihan.", bound.TopicPrefix);
        Assert.Empty(bound.Properties);

        var configured = KafkaMessagingOptions.FromConfiguration(Build(
            ("XiHan:Messaging:Kafka:BootstrapServers", "a:9092,b:9092"),
            ("XiHan:Messaging:Kafka:GroupId", "orders"),
            ("XiHan:Messaging:Kafka:TopicPrefix", "app."),
            ("XiHan:Messaging:Kafka:Properties:security.protocol", "SASL_SSL"),
            ("XiHan:Messaging:Kafka:Properties:sasl.mechanism", "PLAIN")), binding);
        // 两者都给时取绑定的引导地址，其余键照读配置节
        Assert.Equal("bound:9092", configured.BootstrapServers);
        Assert.Equal("a:9092,b:9092", KafkaMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:Kafka:BootstrapServers", "a:9092,b:9092")), default).BootstrapServers);
        Assert.Equal("orders", configured.GroupId);
        Assert.Equal("app.", configured.TopicPrefix);
        Assert.Equal("SASL_SSL", configured.Properties["security.protocol"]);
        Assert.Equal("PLAIN", configured.Properties["sasl.mechanism"]);

        Assert.Throws<ArgumentException>(() => KafkaMessagingOptions.FromConfiguration(Build(), default));
        Assert.Throws<ArgumentException>(static () => new KafkaMessagingOptions { BootstrapServers = " " });
        Assert.Throws<ArgumentException>(static () => new KafkaMessagingOptions { BootstrapServers = "x:1", GroupId = "" });

        // 投递没有完成时重新投递前的等待：缺省 5 秒，配置为正数
        Assert.Equal(TimeSpan.FromSeconds(5), bound.RedeliveryDelay);
        Assert.Equal(TimeSpan.FromSeconds(0.5), KafkaMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:Kafka:RedeliveryDelaySeconds", "0.5")), binding).RedeliveryDelay);
        Assert.Throws<ArgumentException>(() => KafkaMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:Kafka:RedeliveryDelaySeconds", "0")), binding));
        Assert.Throws<ArgumentException>(() => KafkaMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:Kafka:RedeliveryDelaySeconds", "soon")), binding));
        Assert.Throws<ArgumentOutOfRangeException>(static () => new KafkaMessagingOptions { BootstrapServers = "x:1", RedeliveryDelay = TimeSpan.Zero });
    }

    [Fact]
    public void BindingSaslProperties_OverrideTheSameNamedProperties_AndNothingElseIsPassedThrough()
    {
        var configuration = Build(
            ("XiHan:Messaging:Kafka:BootstrapServers", "cfg:9092"),
            ("XiHan:Messaging:Kafka:Properties:security.protocol", "SASL_PLAINTEXT"),
            ("XiHan:Messaging:Kafka:Properties:sasl.mechanism", "PLAIN"),
            ("XiHan:Messaging:Kafka:Properties:sasl.username", "app"),
            ("XiHan:Messaging:Kafka:Properties:sasl.password", "from-config"),
            ("XiHan:Messaging:Kafka:Properties:ssl.ca.location", "/certs/ca.pem"));

        // 绑定只给凭据：引导地址仍来自配置节，四项 SASL 属性逐项取绑定的，空白的不算给出；
        // 绑定目录里的其余文件（provider、host）不是 librdkafka 的配置项，不透传
        var binding = Maybe.Some(new BindingValue("stream", "stream", null, new Dictionary<string, string>
        {
            ["security.protocol"] = "SASL_SSL",
            ["sasl.mechanism"] = " ",
            ["sasl.username"] = "ops",
            ["sasl.password"] = "secret",
            ["provider"] = "confluent",
            ["host"] = "bound",
        }));
        var options = KafkaMessagingOptions.FromConfiguration(configuration, binding);

        Assert.Equal("cfg:9092", options.BootstrapServers);
        Assert.Equal("SASL_SSL", options.Properties["security.protocol"]);
        Assert.Equal("PLAIN", options.Properties["sasl.mechanism"]);
        Assert.Equal("ops", options.Properties["sasl.username"]);
        Assert.Equal("secret", options.Properties["sasl.password"]);
        Assert.Equal("/certs/ca.pem", options.Properties["ssl.ca.location"]);
        Assert.False(options.Properties.ContainsKey("provider"));
        Assert.False(options.Properties.ContainsKey("host"));

        // 没有绑定属性：配置节的照用
        Assert.Equal("from-config", KafkaMessagingOptions.FromConfiguration(configuration, default).Properties["sasl.password"]);
    }
}
