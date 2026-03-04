#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanSqlSugarSplitTableOptions
// Guid:5d10274e-4cb3-4f10-bf24-c886f6a21910
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/05 20:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Data.SqlSugar.Options;

/// <summary>
/// SqlSugar 分表配置
/// </summary>
public sealed class XiHanSqlSugarSplitTableOptions
{
    /// <summary>
    /// 是否启用分表 CodeFirst 初始化
    /// </summary>
    public bool EnableCodeFirstSplitTableInitialization { get; set; } = true;
}
