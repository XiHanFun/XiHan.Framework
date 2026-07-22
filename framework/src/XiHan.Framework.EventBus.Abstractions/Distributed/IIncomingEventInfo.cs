// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ObjectMapping.Extensions.Data;

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 传入事件信息接口
/// 定义分布式事件总线接收到的事件信息结构，包含事件标识、名称、数据等基本信息
/// 继承自 IHasExtraProperties 接口，支持扩展属性
/// </summary>
public interface IIncomingEventInfo : IHasExtraProperties
{
    /// <summary>
    /// 获取事件的唯一标识符
    /// </summary>
    /// <value>事件的全局唯一唯一标识</value>
    Guid Id { get; }

    /// <summary>
    /// 获取消息标识符
    /// 用于在消息中间件中唯一标识此消息
    /// </summary>
    /// <value>消息的唯一标识字符串</value>
    string MessageId { get; }

    /// <summary>
    /// 获取事件名称
    /// 用于标识事件的类型，通常对应事件类的全名
    /// </summary>
    /// <value>事件类型名称</value>
    string EventName { get; }

    /// <summary>
    /// 获取序列化后的事件数据
    /// 包含事件的完整数据内容，以字节数组形式存储
    /// </summary>
    /// <value>序列化的事件数据字节数组</value>
    byte[] EventData { get; }

    /// <summary>
    /// 获取事件创建时间
    /// 表示事件在源系统中被创建的时间点
    /// </summary>
    /// <value>事件创建的 UTC 时间</value>
    DateTime CreatedTime { get; }
}
