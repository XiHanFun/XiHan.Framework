#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UnitOfWorkEventRecord
// Guid:cec11ac9-ba1f-4d54-b43a-d16cfef610df
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 04:09:11
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Uow;

/// <summary>
/// 工作单元事件记录
/// </summary>
public class UnitOfWorkEventRecord
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="eventType"></param>
    /// <param name="eventData"></param>
    /// <param name="eventOrder"></param>
    /// <param name="useOutbox"></param>
    public UnitOfWorkEventRecord(Type eventType, object eventData, long eventOrder, bool useOutbox = true)
    {
        EventType = eventType;
        EventData = eventData;
        EventOrder = eventOrder;
        UseOutbox = useOutbox;
    }

    /// <summary>
    /// 事件数据
    /// </summary>
    public object EventData { get; }

    /// <summary>
    /// 事件类型
    /// </summary>
    public Type EventType { get; }

    /// <summary>
    /// 事件顺序
    /// </summary>
    public long EventOrder { get; protected set; }

    /// <summary>
    /// 是否使用 Outbox
    /// </summary>
    public bool UseOutbox { get; }

    /// <summary>
    /// 额外属性
    /// </summary>
    public Dictionary<string, object> Properties { get; } = [];

    /// <summary>
    /// 设置事件顺序
    /// </summary>
    /// <param name="order"></param>
    public void SetOrder(long order)
    {
        EventOrder = order;
    }
}
