#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IOutgoingEventInfo
// Guid:2c9a3501-3eec-45be-903f-c43fe997d6e7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 4:56:44
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.ObjectMapping.Data;

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 传出事件信息接口
/// 定义分布式事件总线发送事件的信息结构，包含事件标识、名称、数据等基本信息
/// 继承自 IHasExtraProperties 接口，支持扩展属性
/// </summary>
public interface IOutgoingEventInfo : IHasExtraProperties
{
    /// <summary>
    /// 获取事件的唯一标识符
    /// </summary>
    /// <value>事件的全局唯一 Id</value>
    Guid Id { get; }

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
    DateTime CreationTime { get; }
}
