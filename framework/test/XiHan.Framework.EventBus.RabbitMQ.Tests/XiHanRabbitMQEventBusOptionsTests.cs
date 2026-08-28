// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.RabbitMQ.Tests;

/// <summary>
/// RabbitMQ 分布式事件总线配置选项测试
/// </summary>
/// <remarks>
/// 这些默认值是「不配置也能连上本机 RabbitMQ 并跑通事件」的隐式契约，
/// 同时交换机名/队列名/配置节名一旦漂移，已部署的集群会静默连到另一套拓扑上（旧队列积压、新队列空跑），
/// 属于不会报错但会丢事件的故障，因此逐个锁死。
/// </remarks>
public class XiHanRabbitMQEventBusOptionsTests
{
    /// <summary>
    /// 配置节名称是外部 appsettings 依赖的键，不允许漂移
    /// </summary>
    [Fact]
    public void SectionName_IsStableConfigurationKey()
    {
        Assert.Equal("XiHan:EventBus:RabbitMQ", XiHanRabbitMQEventBusOptions.SectionName);
    }

    /// <summary>
    /// 连接相关默认值指向本机 RabbitMQ 的标准端口与账户
    /// </summary>
    [Fact]
    public void Defaults_ForConnection_PointToLocalBroker()
    {
        var options = new XiHanRabbitMQEventBusOptions();

        Assert.Null(options.Uri);
        Assert.Equal("localhost", options.HostName);
        Assert.Equal(5672, options.Port);
        Assert.Equal("guest", options.UserName);
        Assert.Equal("guest", options.Password);
        Assert.Equal("/", options.VirtualHost);
    }

    /// <summary>
    /// 拓扑相关默认值确定交换机、交换机类型与队列
    /// </summary>
    [Fact]
    public void Defaults_ForTopology_AreXiHanDirectExchangeAndSharedQueue()
    {
        var options = new XiHanRabbitMQEventBusOptions();

        Assert.Equal("Default", options.ExchangeName);
        Assert.Equal("direct", options.ExchangeType);
        Assert.Equal("Default.EventBus", options.QueueName);
    }

    /// <summary>
    /// 消费相关默认值确定预取数量与客户端连接名
    /// </summary>
    [Fact]
    public void Defaults_ForConsumer_AreFiftyPrefetchAndNamedClient()
    {
        var options = new XiHanRabbitMQEventBusOptions();

        Assert.Equal((ushort)50, options.PrefetchCount);
        Assert.Equal("Default.EventBus", options.ClientProvidedName);
    }

    /// <summary>
    /// 队列名默认值在所有实例上相同，集群才能形成竞争消费
    /// </summary>
    /// <remarks>
    /// 若默认队列名带上实例唯一后缀，同一应用的每个实例都会拿到全量事件，
    /// 分布式事件「集群内只处理一次」的语义会被悄悄破坏，所以这里显式验证默认值是共享常量。
    /// </remarks>
    [Fact]
    public void Defaults_QueueName_IsSharedAcrossInstances()
    {
        var first = new XiHanRabbitMQEventBusOptions();
        var second = new XiHanRabbitMQEventBusOptions();

        Assert.Equal(first.QueueName, second.QueueName);
    }

    /// <summary>
    /// 未配置交换机类型时回退到 direct
    /// </summary>
    [Fact]
    public void GetExchangeTypeOrDefault_WhenNotConfigured_ReturnsDirect()
    {
        var options = new XiHanRabbitMQEventBusOptions();

        Assert.Equal("direct", options.GetExchangeTypeOrDefault());
    }

    /// <summary>
    /// 交换机类型为空或全空白时回退到 direct
    /// </summary>
    /// <param name="exchangeType">交换机类型</param>
    /// <remarks>
    /// 空白值多半来自 appsettings 里留了空字符串的键，直接透传给 ExchangeDeclare 会让初始化失败；
    /// 回退到 direct 才能保持「配置写错也别炸在启动阶段」的容错口径。
    /// </remarks>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void GetExchangeTypeOrDefault_WhenBlank_ReturnsDirect(string exchangeType)
    {
        var options = new XiHanRabbitMQEventBusOptions
        {
            ExchangeType = exchangeType
        };

        Assert.Equal("direct", options.GetExchangeTypeOrDefault());
    }

    /// <summary>
    /// 交换机类型为 null 时回退到 direct
    /// </summary>
    [Fact]
    public void GetExchangeTypeOrDefault_WhenNull_ReturnsDirect()
    {
        var options = new XiHanRabbitMQEventBusOptions
        {
            ExchangeType = null!
        };

        Assert.Equal("direct", options.GetExchangeTypeOrDefault());
    }

    /// <summary>
    /// 显式配置的交换机类型原样返回
    /// </summary>
    /// <param name="exchangeType">交换机类型</param>
    [Theory]
    [InlineData("direct")]
    [InlineData("topic")]
    [InlineData("fanout")]
    [InlineData("headers")]
    public void GetExchangeTypeOrDefault_WhenConfigured_ReturnsConfiguredValue(string exchangeType)
    {
        var options = new XiHanRabbitMQEventBusOptions
        {
            ExchangeType = exchangeType
        };

        Assert.Equal(exchangeType, options.GetExchangeTypeOrDefault());
    }

    /// <summary>
    /// 交换机类型带前后空白时原样返回，回退只针对全空白
    /// </summary>
    /// <remarks>
    /// 锁住边界：只有「全是空白」才回退，带内容的值不做 Trim，避免实现悄悄加上裁剪而改变可观测行为。
    /// </remarks>
    [Fact]
    public void GetExchangeTypeOrDefault_WhenPadded_IsNotTrimmed()
    {
        var options = new XiHanRabbitMQEventBusOptions
        {
            ExchangeType = " topic "
        };

        Assert.Equal(" topic ", options.GetExchangeTypeOrDefault());
    }

    /// <summary>
    /// 各项配置可独立覆盖，选项之间没有隐藏联动
    /// </summary>
    [Fact]
    public void Properties_AreIndependentlyAssignable()
    {
        var options = new XiHanRabbitMQEventBusOptions
        {
            Uri = "amqp://user:pass@mq.example.com:5673/prod",
            HostName = "mq.example.com",
            Port = 5673,
            UserName = "app",
            Password = "secret",
            VirtualHost = "/prod",
            ExchangeName = "Prod.Exchange",
            ExchangeType = "topic",
            QueueName = "Prod.Queue",
            PrefetchCount = 200,
            ClientProvidedName = "Prod.Client"
        };

        Assert.Equal("amqp://user:pass@mq.example.com:5673/prod", options.Uri);
        Assert.Equal("mq.example.com", options.HostName);
        Assert.Equal(5673, options.Port);
        Assert.Equal("app", options.UserName);
        Assert.Equal("secret", options.Password);
        Assert.Equal("/prod", options.VirtualHost);
        Assert.Equal("Prod.Exchange", options.ExchangeName);
        Assert.Equal("topic", options.ExchangeType);
        Assert.Equal("Prod.Queue", options.QueueName);
        Assert.Equal((ushort)200, options.PrefetchCount);
        Assert.Equal("Prod.Client", options.ClientProvidedName);
    }
}
