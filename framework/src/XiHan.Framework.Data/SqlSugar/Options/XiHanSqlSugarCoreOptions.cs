#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanSqlSugarCoreOptions
// Guid:e2459dc8-3292-48e6-9955-cae7a0503ad0
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
    /// 默认连接配置标识
    /// </summary>
    public string DefaultConfigId { get; set; } = "Default";

    /// <summary>
    /// 租户连接配置前缀
    /// </summary>
    public string TenantConfigIdPrefix { get; set; } = "Tenant_";

    /// <summary>
    /// 当租户有值但找不到连接时是否抛异常
    /// </summary>
    public bool ThrowIfTenantConnectionNotFound { get; set; } = false;

    /// <summary>
    /// 自定义租户连接解析
    /// </summary>
    public Func<long?, string?, string?>? ResolveConnectionConfigId { get; set; }

    /// <summary>
    /// 是否启用租户过滤器
    /// </summary>
    public bool EnableTenantFilter { get; set; } = true;

    /// <summary>
    /// 是否启用软删除过滤器
    /// </summary>
    public bool EnableSoftDeleteFilter { get; set; } = true;

    /// <summary>
    /// 是否对更新自动附加查询过滤条件
    /// </summary>
    public bool EnableAutoUpdateQueryFilter { get; set; } = true;

    /// <summary>
    /// 是否对删除自动附加查询过滤条件
    /// </summary>
    public bool EnableAutoDeleteQueryFilter { get; set; } = true;

    /// <summary>
    /// 分表配置
    /// </summary>
    public XiHanSqlSugarSplitTableOptions SplitTable { get; set; } = new();

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
    /// 是否启用SQL异常日志
    /// </summary>
    public bool EnableSqlErrorLog { get; set; } = true;

    /// <summary>
    /// 是否启用慢SQL日志
    /// </summary>
    public bool EnableSlowSqlLog { get; set; } = true;

    /// <summary>
    /// 慢SQL阈值（毫秒）
    /// </summary>
    public int SlowSqlThresholdMilliseconds { get; set; } = 10000;

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
