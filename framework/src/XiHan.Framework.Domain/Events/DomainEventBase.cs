#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DomainEventBase
// Guid:wxy12f9d-8e3a-4b5c-9a2e-1234567890wx
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 16:15:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Events;

/// <summary>
/// 领域事件基类
/// </summary>
public abstract class DomainEventBase : IDomainEvent
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected DomainEventBase()
    {
        EventId = Guid.NewGuid();
        OccurredOn = DateTimeOffset.Now;
    }

    /// <summary>
    /// 事件发生时间
    /// </summary>
    public DateTimeOffset OccurredOn { get; }

    /// <summary>
    /// 事件唯一标识
    /// </summary>
    public Guid EventId { get; }

    /// <summary>
    /// 重写 ToString 方法
    /// </summary>
    /// <returns>事件的字符串表示</returns>
    public override string ToString()
    {
        return $"{GetType().Name} [EventId: {EventId}, OccurredOn: {OccurredOn:yyyy-MM-dd HH:mm:ss}]";
    }
}
