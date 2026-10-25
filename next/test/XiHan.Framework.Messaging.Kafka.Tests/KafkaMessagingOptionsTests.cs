// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Kafka.Tests;

/// <summary>
/// Kafka 选项：配置优先于绑定、默认值、透传属性与校验。
/// </summary>
public sealed class KafkaMessagingOptionsTests
{
    /// <summary>
    /// 用内存键值对构建配置。
    /// </summary>
    private static IConfiguration Build(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Fact]
    public void FromConfigurationAndBinding_PrefersConfiguration_AndValidates()
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
        Assert.Equal("a:9092,b:9092", configured.BootstrapServers);
        Assert.Equal("orders", configured.GroupId);
        Assert.Equal("app.", configured.TopicPrefix);
        Assert.Equal("SASL_SSL", configured.Properties["security.protocol"]);
        Assert.Equal("PLAIN", configured.Properties["sasl.mechanism"]);

        Assert.Throws<ArgumentException>(() => KafkaMessagingOptions.FromConfiguration(Build(), default));
        Assert.Throws<ArgumentException>(static () => new KafkaMessagingOptions { BootstrapServers = " " });
        Assert.Throws<ArgumentException>(static () => new KafkaMessagingOptions { BootstrapServers = "x:1", GroupId = "" });
    }
}
