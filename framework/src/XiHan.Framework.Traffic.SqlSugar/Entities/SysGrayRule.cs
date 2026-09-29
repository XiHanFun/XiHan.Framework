// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;

namespace XiHan.Framework.Traffic.SqlSugar.Entities;

/// <summary>
/// 灰度规则实体
/// </summary>
[SugarTable("sys_gray_rule")]
public class SysGrayRule : SugarEntity<string>
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysGrayRule() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键，即规则标识</param>
    public SysGrayRule(string basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 规则名称
    /// </summary>
    [SugarColumn(ColumnName = "Rule_Name", Length = 128, IsNullable = false, ColumnDescription = "规则名称")]
    public string RuleName { get; set; } = string.Empty;

    /// <summary>
    /// 规则类型
    /// </summary>
    [SugarColumn(ColumnName = "Rule_Type", IsNullable = false, ColumnDescription = "规则类型")]
    public int RuleType { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    [SugarColumn(ColumnName = "Is_Enabled", IsNullable = false, ColumnDescription = "是否启用")]
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 优先级
    /// </summary>
    [SugarColumn(ColumnName = "Priority", IsNullable = false, ColumnDescription = "优先级")]
    public int Priority { get; set; }

    /// <summary>
    /// 目标版本
    /// </summary>
    [SugarColumn(ColumnName = "Target_Version", Length = 64, IsNullable = true, ColumnDescription = "目标版本")]
    public string? TargetVersion { get; set; }

    /// <summary>
    /// 目标服务标识
    /// </summary>
    [SugarColumn(ColumnName = "Target_Service_Id", Length = 128, IsNullable = true, ColumnDescription = "目标服务标识")]
    public string? TargetServiceId { get; set; }

    /// <summary>
    /// 规则配置（JSON 格式）
    /// </summary>
    [SugarColumn(ColumnName = "Configuration", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "规则配置")]
    public string? Configuration { get; set; }

    /// <summary>
    /// 生效时间
    /// </summary>
    [SugarColumn(ColumnName = "Effective_Time", IsNullable = true, ColumnDescription = "生效时间")]
    public DateTimeOffset? EffectiveTime { get; set; }

    /// <summary>
    /// 失效时间
    /// </summary>
    [SugarColumn(ColumnName = "Expiry_Time", IsNullable = true, ColumnDescription = "失效时间")]
    public DateTimeOffset? ExpiryTime { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(ColumnName = "Created_Time", IsNullable = false, ColumnDescription = "创建时间")]
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    [SugarColumn(ColumnName = "Updated_Time", IsNullable = true, ColumnDescription = "更新时间")]
    public DateTimeOffset? UpdatedTime { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [SugarColumn(ColumnName = "Remark", Length = 512, IsNullable = true, ColumnDescription = "备注")]
    public string? Remark { get; set; }
}
