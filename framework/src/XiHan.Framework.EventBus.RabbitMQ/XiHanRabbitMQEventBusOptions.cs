// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.RabbitMQ;

/// <summary>
/// 曦寒 RabbitMQ 分布式事件总线配置选项
/// </summary>
public class XiHanRabbitMQEventBusOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:EventBus:RabbitMQ";

    /// <summary>
    /// 完整连接串（形如 amqp://user:pass@host:5672/vhost）；设置后优先于下方单项配置
    /// </summary>
    public string? Uri { get; set; }

    /// <summary>
    /// 主机名
    /// </summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// 端口
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// 用户名
    /// </summary>
    public string UserName { get; set; } = "guest";

    /// <summary>
    /// 密码
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// 虚拟主机
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// 交换机名称
    /// </summary>
    public string ExchangeName { get; set; } = "XiHan";

    /// <summary>
    /// 交换机类型（direct / topic / fanout），默认 direct
    /// </summary>
    public string ExchangeType { get; set; } = "direct";

    /// <summary>
    /// 队列名称（同一应用的多个实例共享同一队列 → 竞争消费，保证分布式事件在集群中只被处理一次）
    /// </summary>
    public string QueueName { get; set; } = "XiHan.EventBus";

    /// <summary>
    /// 消费者预取数量（QoS）
    /// </summary>
    public ushort PrefetchCount { get; set; } = 50;

    /// <summary>
    /// 客户端连接名称（便于在 RabbitMQ 管理台识别）
    /// </summary>
    public string ClientProvidedName { get; set; } = "XiHan.EventBus";

    /// <summary>
    /// 获取交换机类型（空则回退 direct）
    /// </summary>
    /// <returns>交换机类型</returns>
    public string GetExchangeTypeOrDefault()
    {
        return string.IsNullOrWhiteSpace(ExchangeType) ? "direct" : ExchangeType;
    }
}
