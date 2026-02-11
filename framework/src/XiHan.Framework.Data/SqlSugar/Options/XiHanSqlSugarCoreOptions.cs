#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanSqlSugarCoreOptions
// Guid:3de67a9c-9f8a-4c79-8e2c-7a6c3d09e124
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2023/11/15 08:40:52
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;

namespace XiHan.Framework.Data.SqlSugar.Options;

/// <summary>
/// 曦寒框架 SqlSugarCore 选项
/// </summary>
public class XiHanSqlSugarCoreOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Data:SqlSugarCore";

    /// <summary>
    /// 连接配置集合
    /// </summary>
    public List<SqlSugarConnectionConfigOptions> ConnectionConfigs { get; set; } = [];

    /// <summary>
    /// 全局过滤器
    /// </summary>
    public Dictionary<Type, Func<object, bool>> GlobalFilters { get; set; } = [];

    /// <summary>
    /// SQL日志动作
    /// </summary>
    public Action<string, SugarParameter[]>? SqlLogAction { get; set; }

    /// <summary>
    /// 配置SqlSugar客户端的动作
    /// </summary>
    public Action<ISqlSugarClient>? ConfigureDbAction { get; set; }

    /// <summary>
    /// 是否启用SQL日志
    /// </summary>
    public bool EnableSqlLog { get; set; } = false;

    /// <summary>
    /// 是否启用数据库初始化
    /// </summary>
    public bool EnableDbInitialization { get; set; } = false;

    /// <summary>
    /// 是否启用表结构初始化
    /// </summary>
    public bool EnableTableInitialization { get; set; } = false;

    /// <summary>
    /// 是否启用种子数据
    /// </summary>
    public bool EnableDataSeeding { get; set; } = false;
}
