// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.RabbitMQ.Tests;

public sealed class RabbitMQMessagingOptionsTests
{
    private static IConfiguration Build(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Fact]
    public void FromConfigurationAndBinding_PrefersConfiguration_AndValidates()
    {
        var binding = Maybe.Some(new BindingValue("queue", "queue", "amqp://guest:guest@bound:5672", new Dictionary<string, string>()));

        Assert.Equal("amqp://guest:guest@bound:5672", RabbitMQMessagingOptions.FromConfiguration(Build(), binding).Configuration);
        var configured = RabbitMQMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:RabbitMQ:Configuration", "amqps://cfg:5671"), ("XiHan:Messaging:RabbitMQ:QueuePrefix", "app."), ("XiHan:Messaging:RabbitMQ:PrefetchCount", "4")), binding);
        Assert.Equal("amqps://cfg:5671", configured.Configuration);
        Assert.Equal("app.", configured.QueuePrefix);
        Assert.Equal(4, configured.PrefetchCount);
        Assert.Equal("xihan.", RabbitMQMessagingOptions.FromConfiguration(Build(), binding).QueuePrefix);

        Assert.Throws<ArgumentException>(() => RabbitMQMessagingOptions.FromConfiguration(Build(), default));
        Assert.Throws<ArgumentException>(() => RabbitMQMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:RabbitMQ:Configuration", "redis://nope")), default));
        Assert.Throws<ArgumentException>(() => RabbitMQMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:RabbitMQ:PrefetchCount", "0")), binding));
        Assert.Throws<ArgumentException>(static () => new RabbitMQMessagingOptions { Configuration = "not a uri" });
    }
}
