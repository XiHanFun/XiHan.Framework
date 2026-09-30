// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Initializers;

namespace XiHan.Framework.Workflow.SqlSugar.Entities;

/// <summary>
/// 流程定义实体
/// </summary>
[SugarTable("sys_workflow_definition")]
[TableInitialization(Target = DbInitializationTarget.Platform)]
[SugarIndex("ux_{table}_code_version", nameof(Code), OrderByType.Asc, nameof(Version), OrderByType.Asc, true)]
public class SysWorkflowDefinition : SugarEntity<string>
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysWorkflowDefinition() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">定义标识</param>
    public SysWorkflowDefinition(string basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 流程编码
    /// </summary>
    [SugarColumn(ColumnName = "Code", Length = 128, IsNullable = false, ColumnDescription = "流程编码")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 流程名称
    /// </summary>
    [SugarColumn(ColumnName = "Name", Length = 256, IsNullable = false, ColumnDescription = "流程名称")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 版本号
    /// </summary>
    [SugarColumn(ColumnName = "Version", IsNullable = false, ColumnDescription = "版本号")]
    public int Version { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    [SugarColumn(ColumnName = "Description", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "描述")]
    public string? Description { get; set; }

    /// <summary>
    /// 分类
    /// </summary>
    [SugarColumn(ColumnName = "Category", Length = 128, IsNullable = true, ColumnDescription = "分类")]
    public string? Category { get; set; }

    /// <summary>
    /// 状态，0 草稿，1 已发布，2 已停用，3 已归档
    /// </summary>
    [SugarColumn(ColumnName = "Status", IsNullable = false, ColumnDescription = "状态，0 草稿，1 已发布，2 已停用，3 已归档")]
    public int Status { get; set; }

    /// <summary>
    /// 是否启用补偿
    /// </summary>
    [SugarColumn(ColumnName = "Enable_Compensation", IsNullable = false, ColumnDescription = "是否启用补偿")]
    public bool EnableCompensation { get; set; }

    /// <summary>
    /// 节点集合的 JSON
    /// </summary>
    [SugarColumn(ColumnName = "Nodes_Json", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = false, ColumnDescription = "节点集合的 JSON")]
    public string NodesJson { get; set; } = "[]";

    /// <summary>
    /// 连线集合的 JSON
    /// </summary>
    [SugarColumn(ColumnName = "Transitions_Json", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = false, ColumnDescription = "连线集合的 JSON")]
    public string TransitionsJson { get; set; } = "[]";

    /// <summary>
    /// 启动变量声明的 JSON
    /// </summary>
    [SugarColumn(ColumnName = "Variables_Json", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = false, ColumnDescription = "启动变量声明的 JSON")]
    public string VariablesJson { get; set; } = "[]";

    /// <summary>
    /// 扩展属性的 JSON
    /// </summary>
    [SugarColumn(ColumnName = "Extra_Properties", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = false, ColumnDescription = "扩展属性的 JSON")]
    public string ExtraProperties { get; set; } = "{}";

    /// <summary>
    /// 租户标识，为空表示平台级定义
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", IsNullable = true, ColumnDescription = "租户标识，为空表示平台级定义")]
    public long? OwnerTenantId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(ColumnName = "Creation_Time", IsNullable = false, ColumnDescription = "创建时间")]
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// 发布时间
    /// </summary>
    [SugarColumn(ColumnName = "Publish_Time", IsNullable = true, ColumnDescription = "发布时间")]
    public DateTime? PublishTime { get; set; }
}
