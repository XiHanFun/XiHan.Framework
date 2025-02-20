#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DomainEventRecord
// Guid:d192025a-e504-4834-ba1b-ab3b3bbdf1ef
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/20 4:56:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Ddd.Domain.Events;

/// <summary>
/// 领域事件记录
/// </summary>
public class DomainEventRecord
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="eventData"></param>
    /// <param name="eventOrder"></param>
    public DomainEventRecord(object eventData, long eventOrder)
    {
        EventData = eventData;
        EventOrder = eventOrder;
    }

    /// <summary>
    /// 事件数据
    /// </summary>
    public object EventData { get; }

    /// <summary>
    /// 事件顺序
    /// </summary>
    public long EventOrder { get; }
}
