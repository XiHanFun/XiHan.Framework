#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DomainEventRecord
// Guid:d192025a-e504-4834-ba1b-ab3b3bbdf1ef
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/20 04:56:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Events.Abstracts;

namespace XiHan.Framework.Domain.Events;

/// <summary>
/// 领域事件记录
/// </summary>
public class DomainEventRecord
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <param name="eventOrder">事件顺序</param>
    public DomainEventRecord(IDomainEvent eventData, long eventOrder)
    {
        EventData = eventData;
        EventOrder = eventOrder;
    }

    /// <summary>
    /// 事件数据
    /// </summary>
    public IDomainEvent EventData { get; }

    /// <summary>
    /// 事件顺序
    /// </summary>
    public long EventOrder { get; }
}
