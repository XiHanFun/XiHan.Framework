#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanKafkaEventBusOptions
// Guid:b4d4c955-187e-466b-b027-b55c0a4aaf93
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.EventBus.Kafka;

/// <summary>
/// 曦寒 Kafka 分布式事件总线配置选项
/// </summary>
public class XiHanKafkaEventBusOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:EventBus:Kafka";

    /// <summary>
    /// Kafka 集群地址（逗号分隔，形如 host1:9092,host2:9092）
    /// </summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>
    /// 主题名称（所有事件写入同一主题，以事件名作为消息 Key）
    /// </summary>
    public string TopicName { get; set; } = "XiHan.EventBus";

    /// <summary>
    /// 消费者组 Id（同组内竞争消费，保证分布式事件在集群中只被处理一次）
    /// </summary>
    public string GroupId { get; set; } = "XiHan.EventBus";

    /// <summary>
    /// 首次消费的偏移策略（earliest / latest）
    /// </summary>
    public string AutoOffsetReset { get; set; } = "earliest";

    /// <summary>
    /// 是否在初始化时自动确保主题存在（生产环境常关闭 broker 自动建主题）
    /// </summary>
    public bool EnsureTopicExists { get; set; } = true;

    /// <summary>
    /// 自动建主题时的分区数
    /// </summary>
    public int TopicPartitionCount { get; set; } = 1;

    /// <summary>
    /// 自动建主题时的副本数
    /// </summary>
    public short TopicReplicationFactor { get; set; } = 1;
}
