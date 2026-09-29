// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;

namespace XiHan.Framework.Settings.SqlSugar.Entities;

/// <summary>
/// 设置值实体
/// </summary>
[SugarTable("sys_setting")]
[SugarIndex("uk_sys_setting_key",
    nameof(SettingName), OrderByType.Asc,
    nameof(ProviderName), OrderByType.Asc,
    nameof(ProviderKey), OrderByType.Asc,
    isUnique: true)]
public class SysSetting : SugarEntity<long>
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysSetting() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SysSetting(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 设置名称
    /// </summary>
    [SugarColumn(ColumnName = "Setting_Name", Length = 128, IsNullable = false, ColumnDescription = "设置名称")]
    public string SettingName { get; set; } = string.Empty;

    /// <summary>
    /// 提供者名称，未指定时存归一化占位符
    /// </summary>
    [SugarColumn(ColumnName = "Provider_Name", Length = 32, IsNullable = false, ColumnDescription = "提供者名称，未指定时存归一化占位符")]
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// 提供者键，未指定时存归一化占位符
    /// </summary>
    [SugarColumn(ColumnName = "Provider_Key", Length = 64, IsNullable = false, ColumnDescription = "提供者键，未指定时存归一化占位符")]
    public string ProviderKey { get; set; } = string.Empty;

    /// <summary>
    /// 设置值
    /// </summary>
    [SugarColumn(ColumnName = "Setting_Value", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "设置值")]
    public string? SettingValue { get; set; }
}
