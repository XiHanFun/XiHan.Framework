// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Workflow.SqlSugar.Options;

/// <summary>
/// 工作流 SqlSugar 存储配置
/// </summary>
public class XiHanWorkflowSqlSugarOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Workflow:SqlSugar";

    /// <summary>
    /// 工作流数据表所在连接的配置标识，为空时使用数据访问的默认连接配置标识
    /// </summary>
    public string? ConfigId { get; set; }
}
