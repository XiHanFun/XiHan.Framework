// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Initializers;

namespace XiHan.Framework.EventBus.SqlSugar.Entities;

/// <summary>
/// 发件箱实体
/// </summary>
[SugarTable("sys_event_outbox")]
[SugarIndex("ix_sys_event_outbox_status", nameof(SysEventOutbox.Status), OrderByType.Asc, nameof(SysEventOutbox.CreatedTime), OrderByType.Asc)]
[TableInitialization(IncludeModuleConnections = true)]
public class SysEventOutbox : SugarEntity<Guid>
{
    /// <summary>
    /// 待发送状态
    /// </summary>
    public const int StatusPending = 0;

    /// <summary>
    /// 已领取状态
    /// </summary>
    public const int StatusClaimed = 1;

    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysEventOutbox() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">事件唯一标识</param>
    public SysEventOutbox(Guid basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 事件名称
    /// </summary>
    [SugarColumn(ColumnName = "Event_Name", Length = 256, IsNullable = false, ColumnDescription = "事件名称")]
    public string EventName { get; set; } = string.Empty;

    /// <summary>
    /// 序列化后的事件数据
    /// </summary>
    [SugarColumn(ColumnName = "Event_Data", IsNullable = false, ColumnDescription = "序列化后的事件数据")]
    public byte[] EventData { get; set; } = [];

    /// <summary>
    /// 事件创建时间
    /// </summary>
    [SugarColumn(ColumnName = "Created_Time", IsNullable = false, ColumnDescription = "事件创建时间")]
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// 扩展属性的 JSON
    /// </summary>
    [SugarColumn(ColumnName = "Extra_Properties", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "扩展属性的 JSON")]
    public string? ExtraProperties { get; set; }

    /// <summary>
    /// 发送状态，0 待发送，1 已领取
    /// </summary>
    [SugarColumn(ColumnName = "Status", IsNullable = false, ColumnDescription = "发送状态，0 待发送，1 已领取")]
    public int Status { get; set; }

    /// <summary>
    /// 领取令牌
    /// </summary>
    [SugarColumn(ColumnName = "Claim_Token", Length = 64, IsNullable = true, ColumnDescription = "领取令牌")]
    public string? ClaimToken { get; set; }

    /// <summary>
    /// 领取时刻
    /// </summary>
    [SugarColumn(ColumnName = "Claim_Time", IsNullable = true, ColumnDescription = "领取时刻")]
    public DateTimeOffset? ClaimTime { get; set; }
}
