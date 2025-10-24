#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OutgoingEventInfo
// Guid:f6b81be5-47f6-4e4e-b14f-96468c6146c1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 8:00:26
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.ObjectMapping.Data;
using XiHan.Framework.Utils.Collections;
using XiHan.Framework.Utils.Diagnostics;

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 传出事件信息实现类
/// 实现 IOutgoingEventInfo 接口，表示向分布式事件总线发送的事件信息
/// 包含事件的完整元数据和扩展属性支持
/// </summary>
public class OutgoingEventInfo : IOutgoingEventInfo
{
    /// <summary>
    /// 初始化 OutgoingEventInfo 类的新实例
    /// </summary>
    /// <param name="id">事件唯一标识符</param>
    /// <param name="eventName">事件名称</param>
    /// <param name="eventData">序列化的事件数据</param>
    /// <param name="creationTime">事件创建时间</param>
    /// <exception cref="ArgumentException">当 eventName 为 null、空或超过最大长度时</exception>
    /// <exception cref="ArgumentNullException">当 eventData 为 null 时</exception>
    public OutgoingEventInfo(
        Guid id,
        string eventName,
        byte[] eventData,
        DateTime creationTime)
    {
        Id = id;
        EventName = Guard.NotNullOrWhiteSpace(eventName, nameof(eventName), MaxEventNameLength);

        if (eventName.Length > MaxEventNameLength)
        {
            throw new ArgumentException($"事件名称长度不能超过 {MaxEventNameLength} 个字符", nameof(eventName));
        }

        EventData = eventData ?? throw new ArgumentNullException(nameof(eventData));
        CreationTime = creationTime;
        ExtraProperties = [];
        this.SetDefaultsForExtraProperties();
    }

    /// <summary>
    /// 初始化 OutgoingEventInfo 类的新实例（受保护的无参构造函数）
    /// 主要用于框架内部或继承类的特殊场景
    /// </summary>
    protected OutgoingEventInfo()
    {
        ExtraProperties = [];
        this.SetDefaultsForExtraProperties();
    }

    /// <summary>
    /// 获取或设置事件名称的最大长度限制
    /// </summary>
    /// <value>事件名称允许的最大字符数，默认为 256</value>
    public static int MaxEventNameLength { get; set; } = 256;

    /// <summary>
    /// 获取扩展属性字典
    /// 用于存储事件的额外元数据信息
    /// </summary>
    /// <value>扩展属性的键值对集合</value>
    public ExtraPropertyDictionary ExtraProperties { get; protected set; }

    /// <summary>
    /// 获取事件的唯一标识符
    /// </summary>
    /// <value>事件的全局唯一唯一标识</value>
    public Guid Id { get; }

    /// <summary>
    /// 获取事件名称
    /// 用于标识事件的类型，通常对应事件类的全名
    /// </summary>
    /// <value>事件类型名称</value>
    public string EventName { get; } = default!;

    /// <summary>
    /// 获取序列化后的事件数据
    /// 包含事件的完整数据内容，以字节数组形式存储
    /// </summary>
    /// <value>序列化的事件数据字节数组</value>
    public byte[] EventData { get; } = default!;

    /// <summary>
    /// 获取事件创建时间
    /// 表示事件在源系统中被创建的时间点
    /// </summary>
    /// <value>事件创建的 UTC 时间</value>
    public DateTime CreationTime { get; }

    /// <summary>
    /// 设置关联标识符
    /// 用于跟踪和关联分布式系统中的相关事件和操作
    /// </summary>
    /// <param name="correlationId">关联标识符</param>
    /// <exception cref="ArgumentException">当 correlationId 为 null 或空字符串时</exception>
    public void SetCorrelationId(string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ExtraProperties[EventBusConsts.CorrelationIdHeaderName] = correlationId;
    }

    /// <summary>
    /// 获取关联标识符
    /// 从扩展属性中提取关联标识符
    /// </summary>
    /// <returns>关联标识符字符串，如果不存在则返回 null</returns>
    public string? GetCorrelationId()
    {
        return ExtraProperties.GetOrDefault(EventBusConsts.CorrelationIdHeaderName)?.ToString();
    }
}
