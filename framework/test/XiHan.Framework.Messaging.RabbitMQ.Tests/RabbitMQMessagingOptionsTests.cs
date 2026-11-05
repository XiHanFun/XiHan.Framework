// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.RabbitMQ.Tests;

/// <summary>
/// RabbitMQ 选项从配置与绑定解析的测试
/// </summary>
public sealed class RabbitMQMessagingOptionsTests
{
    /// <summary>
    /// 用内存键值对构造配置
    /// </summary>
    private static IConfiguration Build(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Fact]
    public void FromConfigurationAndBinding_PrefersBinding_AndValidates()
    {
        var binding = Maybe.Some(new BindingValue("queue", "queue", "amqp://guest:guest@bound:5672", new Dictionary<string, string>()));

        Assert.Equal("amqp://guest:guest@bound:5672", RabbitMQMessagingOptions.FromConfiguration(Build(), binding).Configuration);
        var configured = RabbitMQMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:RabbitMQ:Configuration", "amqps://cfg:5671"), ("XiHan:Messaging:RabbitMQ:QueuePrefix", "app."), ("XiHan:Messaging:RabbitMQ:PrefetchCount", "4")), binding);
        // 两者都给时取绑定的连接，其余键照读配置节
        Assert.Equal("amqp://guest:guest@bound:5672", configured.Configuration);
        Assert.Equal("amqps://cfg:5671", RabbitMQMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:RabbitMQ:Configuration", "amqps://cfg:5671")), default).Configuration);
        Assert.Equal("app.", configured.QueuePrefix);
        Assert.Equal(4, configured.PrefetchCount);
        Assert.Equal("xihan.", RabbitMQMessagingOptions.FromConfiguration(Build(), binding).QueuePrefix);

        Assert.Throws<ArgumentException>(() => RabbitMQMessagingOptions.FromConfiguration(Build(), default));
        Assert.Throws<ArgumentException>(() => RabbitMQMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:RabbitMQ:Configuration", "redis://nope")), default));
        Assert.Throws<ArgumentException>(() => RabbitMQMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:RabbitMQ:PrefetchCount", "0")), binding));
        Assert.Throws<ArgumentException>(static () => new RabbitMQMessagingOptions { Configuration = "not a uri" });
    }

    [Fact]
    public void BindingCredentials_OverrideTheUriUserInfo_WithoutRewritingIt()
    {
        var configuration = Build(("XiHan:Messaging:RabbitMQ:Configuration", "amqp://app:from-config@cfg:5672/orders"));

        // 绑定只给凭据：连接仍来自配置节，用户名与密码取绑定的；含 @ 与 : 的密码不必转义
        var credentials = Maybe.Some(new BindingValue("queue", "queue", null, new Dictionary<string, string> { ["username"] = "ops", ["password"] = "p@ss:w/rd" }));
        var options = RabbitMQMessagingOptions.FromConfiguration(configuration, credentials);
        Assert.Equal("amqp://app:from-config@cfg:5672/orders", options.Configuration);
        Assert.Equal("ops", options.UserName);
        Assert.Equal("p@ss:w/rd", options.Password);
        var factory = RabbitMQMessageBroker.CreateFactory(options);
        Assert.Equal("ops", factory.UserName);
        Assert.Equal("p@ss:w/rd", factory.Password);
        Assert.Equal("cfg", factory.HostName);
        Assert.Equal("orders", factory.VirtualHost);

        // 只给密码、用户名空白：用户名照读 URI
        var passwordOnly = Maybe.Some(new BindingValue("queue", "queue", null, new Dictionary<string, string> { ["username"] = " ", ["password"] = "secret" }));
        var partial = RabbitMQMessageBroker.CreateFactory(RabbitMQMessagingOptions.FromConfiguration(configuration, passwordOnly));
        Assert.Equal("app", partial.UserName);
        Assert.Equal("secret", partial.Password);

        // 没有绑定凭据：URI 里的照用
        var unbound = RabbitMQMessageBroker.CreateFactory(RabbitMQMessagingOptions.FromConfiguration(configuration, default));
        Assert.Equal("app", unbound.UserName);
        Assert.Equal("from-config", unbound.Password);

        // 连接不合法时报错不带凭据
        var invalid = Assert.Throws<ArgumentException>(() => RabbitMQMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:RabbitMQ:Configuration", "redis://app:leaked@cfg")), credentials));
        Assert.DoesNotContain("leaked", invalid.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("p@ss", invalid.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Exchange_DefaultsToXihan_IsReadFromConfiguration_AndRejectsEmptyOrReservedNames()
    {
        var binding = Maybe.Some(new BindingValue("queue", "queue", "amqp://guest:guest@bound:5672", new Dictionary<string, string>()));

        Assert.Equal("xihan", RabbitMQMessagingOptions.FromConfiguration(Build(), binding).Exchange);
        Assert.Equal("orders", RabbitMQMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:RabbitMQ:Exchange", "orders")), binding).Exchange);
        Assert.Throws<ArgumentException>(() => RabbitMQMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:RabbitMQ:Exchange", " ")), binding));
        Assert.Throws<ArgumentException>(() => RabbitMQMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:RabbitMQ:Exchange", "amq.direct")), binding));
    }
}
