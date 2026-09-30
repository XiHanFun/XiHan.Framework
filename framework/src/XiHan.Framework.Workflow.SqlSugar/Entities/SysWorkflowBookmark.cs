// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Initializers;

namespace XiHan.Framework.Workflow.SqlSugar.Entities;

/// <summary>
/// 流程书签实体
/// </summary>
[SugarTable("sys_workflow_bookmark")]
[TableInitialization(Target = DbInitializationTarget.Platform)]
[SugarIndex("idx_{table}_instance", nameof(InstanceId), OrderByType.Asc, nameof(CreationTime), OrderByType.Asc)]
[SugarIndex("idx_{table}_node_instance", nameof(NodeInstanceId), OrderByType.Asc)]
[SugarIndex("idx_{table}_due", nameof(DueTime), OrderByType.Asc)]
[SugarIndex("idx_{table}_kind_key", nameof(Kind), OrderByType.Asc, nameof(BookmarkKey), OrderByType.Asc, nameof(CreationTime), OrderByType.Asc)]
public class SysWorkflowBookmark : SugarEntity<string>
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysWorkflowBookmark() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">书签标识</param>
    public SysWorkflowBookmark(string basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 所属流程实例标识
    /// </summary>
    [SugarColumn(ColumnName = "Instance_Id", Length = 255, IsNullable = false, ColumnDescription = "所属流程实例标识")]
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 节点标识
    /// </summary>
    [SugarColumn(ColumnName = "Node_Id", Length = 255, IsNullable = false, ColumnDescription = "节点标识")]
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// 节点实例标识
    /// </summary>
    [SugarColumn(ColumnName = "Node_Instance_Id", Length = 255, IsNullable = false, ColumnDescription = "节点实例标识")]
    public string NodeInstanceId { get; set; } = string.Empty;

    /// <summary>
    /// 书签种类
    /// </summary>
    [SugarColumn(ColumnName = "Kind", Length = 64, IsNullable = false, ColumnDescription = "书签种类")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 索引键，语义随种类而定：受理人标识、信号名称或父节点实例标识
    /// </summary>
    [SugarColumn(ColumnName = "Bookmark_Key", Length = 256, IsNullable = true, ColumnDescription = "索引键，语义随种类而定：受理人标识、信号名称或父节点实例标识")]
    public string? BookmarkKey { get; set; }

    /// <summary>
    /// 附加数据的 JSON
    /// </summary>
    [SugarColumn(ColumnName = "Payload_Json", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = false, ColumnDescription = "附加数据的 JSON")]
    public string PayloadJson { get; set; } = "{}";

    /// <summary>
    /// 到期时间
    /// </summary>
    [SugarColumn(ColumnName = "Due_Time", IsNullable = true, ColumnDescription = "到期时间")]
    public DateTime? DueTime { get; set; }

    /// <summary>
    /// 业务相关性标识
    /// </summary>
    [SugarColumn(ColumnName = "Correlation_Id", Length = 255, IsNullable = true, ColumnDescription = "业务相关性标识")]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(ColumnName = "Creation_Time", IsNullable = false, ColumnDescription = "创建时间")]
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// 租户标识
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", IsNullable = true, ColumnDescription = "租户标识")]
    public long? OwnerTenantId { get; set; }
}
