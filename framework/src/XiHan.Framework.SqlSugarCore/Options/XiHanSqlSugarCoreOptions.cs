#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanSqlSugarCoreOptions
// Guid:3de67a9c-9f8a-4c79-8e2c-7a6c3d09e124
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2023-11-15 8:40:52
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;

namespace XiHan.Framework.SqlSugarCore.Options;

/// <summary>
/// SqlSugar连接配置
/// </summary>
public class SqlSugarConnectionConfig
{
    /// <summary>
    /// 配置ID
    /// </summary>
    public string ConfigId { get; set; } = "Default";

    /// <summary>
    /// 连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = null!;

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DbType DbType { get; set; } = DbType.SqlServer;

    /// <summary>
    /// 是否自动关闭连接
    /// </summary>
    public bool IsAutoCloseConnection { get; set; } = true;

    /// <summary>
    /// 初始化键类型
    /// </summary>
    public InitKeyType InitKeyType { get; set; } = InitKeyType.Attribute;

    /// <summary>
    /// 更多设置
    /// </summary>
    public ConnMoreSettings? MoreSettings { get; set; }

    /// <summary>
    /// 从库连接配置
    /// </summary>
    public List<SlaveConnectionConfig>? SlaveConnectionConfigs { get; set; }
}

/// <summary>
/// 曦寒框架 SqlSugarCore 选项
/// </summary>
public class XiHanSqlSugarCoreOptions
{
    /// <summary>
    /// 连接配置集合
    /// </summary>
    public List<SqlSugarConnectionConfig> ConnectionConfigs { get; set; } = [];

    /// <summary>
    /// 全局过滤器
    /// </summary>
    public Dictionary<Type, Func<object, bool>> GlobalFilters { get; set; } = [];

    /// <summary>
    /// 是否启用SQL日志
    /// </summary>
    public bool EnableSqlLog { get; set; } = false;

    /// <summary>
    /// SQL日志动作
    /// </summary>
    public Action<string, SugarParameter[]>? SqlLogAction { get; set; }

    /// <summary>
    /// 配置SqlSugar客户端的动作
    /// </summary>
    public Action<ISqlSugarClient>? ConfigureDbAction { get; set; }
}
