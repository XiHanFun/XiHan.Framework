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
    public string StreamKey { get; set; } = "XiHan:EventBus:Stream";

    /// <summary>
    /// 消费者组名称（同组内竞争消费，保证分布式事件在集群中只被处理一次）
    /// </summary>
    public string ConsumerGroup { get; set; } = "XiHan.EventBus";

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
}
