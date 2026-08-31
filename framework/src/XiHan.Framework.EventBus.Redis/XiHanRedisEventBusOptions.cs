// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Redis;

/// <summary>
/// 曦寒 Redis（Streams）分布式事件总线配置选项
/// </summary>
public class XiHanRedisEventBusOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:EventBus:Redis";

    /// <summary>
    /// StackExchange.Redis 连接串（与缓存各连各的，独立连接）
    /// </summary>
    public string Configuration { get; set; } = "localhost:6379";

    /// <summary>
    /// 事件流键（所有事件写入同一 Stream，以字段区分事件名）
    /// </summary>
    public string StreamKey { get; set; } = "Default:EventBus:Stream";

    /// <summary>
    /// 消费者组名称（同组内竞争消费，保证分布式事件在集群中只被处理一次）
    /// </summary>
    public string ConsumerGroup { get; set; } = "Default.EventBus";

    /// <summary>
    /// 单次读取批量大小
    /// </summary>
    public int ReadBatchSize { get; set; } = 10;

    /// <summary>
    /// 无消息时的轮询间隔（毫秒）
    /// </summary>
    public int PollIntervalMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Stream 近似最大长度（自动裁剪，防止无限增长）；小于等于 0 表示不裁剪
    /// </summary>
    public int MaxStreamLength { get; set; } = 100000;

    /// <summary>
    /// 待处理消息被判定为滞留、可由其他消费者接管的空闲时长（毫秒）
    /// </summary>
    /// <remarks>
    /// 消费者在读取与确认之间崩溃时，消息会留在该消费者的待处理列表里。
    /// 超过本时长即允许其他消费者接管，否则这条消息永远不会被再次投递。
    /// 取值需大于正常处理耗时，否则处理中的消息会被并发接管造成重复处理。
    /// </remarks>
    public int ClaimMinIdleMilliseconds { get; set; } = 60000;

    /// <summary>
    /// 两次接管滞留消息之间的最小间隔（毫秒）
    /// </summary>
    public int ClaimIntervalMilliseconds { get; set; } = 30000;

    /// <summary>
    /// 单次接管的最大消息数
    /// </summary>
    public int ClaimBatchSize { get; set; } = 20;

    /// <summary>
    /// 最大投递次数，超过后转入死信 Stream
    /// </summary>
    /// <remarks>
    /// 无上限地重投毒消息会让它反复占用消费能力；转入死信后原消息被确认，
    /// 消费得以继续，同时消息本身仍可查、可重放，不会像直接确认那样凭空消失。
    /// </remarks>
    public int MaxDeliveryCount { get; set; } = 5;

    /// <summary>
    /// 死信 Stream 键；为空时取主 Stream 键加 ":dead" 后缀
    /// </summary>
    public string? DeadLetterStreamKey { get; set; }

    /// <summary>
    /// 获取实际生效的死信 Stream 键
    /// </summary>
    /// <returns>死信 Stream 键</returns>
    public string ResolveDeadLetterStreamKey()
    {
        return string.IsNullOrWhiteSpace(DeadLetterStreamKey) ? StreamKey + ":dead" : DeadLetterStreamKey;
    }
}
