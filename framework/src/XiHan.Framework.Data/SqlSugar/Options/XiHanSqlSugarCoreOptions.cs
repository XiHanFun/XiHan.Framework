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
    /// 全局过滤器
    /// </summary>
    public Dictionary<Type, Func<object, bool>> GlobalFilters { get; set; } = [];

    /// <summary>
    /// SQL日志动作
    /// </summary>
    public Action<string, SugarParameter[]>? SqlLogAction { get; set; }

    /// <summary>
    /// 构建前连接配置钩子：把框架已填好默认值的原生 <see cref="ConnectionConfig"/> 完整交给调用方
    /// </summary>
    /// <remarks>
    /// 在 <see cref="global::SqlSugar.SqlSugarScope"/> 构造之前触发，参数为**原生** SqlSugar 连接配置列表。
    /// 调用方可在此完整定制任何原生能力——增删/调整 <c>SlaveConnectionConfigs</c>（含按需设置 <c>HitRate</c> 权重，
    /// 该字段无法经 appsettings 绑定，只能在此以代码方式赋值）、挂 <c>ConfigureExternalServices</c>（如可空列处理）、
    /// 写主从探活逻辑等。不设置则一律吃框架默认。
    /// 该钩子同样会在运行时新增租户连接时以单元素列表形式触发，调用方应按 <c>ConfigId</c> 分支处理，保证幂等。
    /// </remarks>
    public Action<List<ConnectionConfig>>? ConfigureConnectionConfigs { get; set; }

    /// <summary>
    /// 追加式 DataExecuting 钩子：核心焊死、只许追加
    /// </summary>
    /// <remarks>
    /// 框架核心的雪花主键 / 审计字段 / 租户标识注入始终**先于**本委托执行且不可被覆盖；
    /// 调用方在此仅能追加逻辑（如业务自有字段填充），不会破坏多租户与审计。
    /// 请勿在 <see cref="ConfigureDbAction"/> 里直接给 <c>Aop.DataExecuting</c> 赋值——SqlSugar 该事件为单次赋值，
    /// 直接赋值会整体冲掉框架核心注入，务必改用此追加口子。
    /// </remarks>
    public Action<object, DataFilterModel>? AppendDataExecuting { get; set; }

    /// <summary>
    /// 配置SqlSugar客户端的动作
    /// </summary>
    public Action<ISqlSugarClient>? ConfigureDbAction { get; set; }

    /// <summary>
    /// 从库默认权重：appsettings 里 <c>HitRate</c> 因是字段无法绑定，恒为 0 会导致从库永不被选中；
    /// 框架在构建时把 <c>HitRate &lt;= 0</c> 的从库归一化为此权重，保证经 appsettings 配置的从库能真正分担读流量。
    /// 需要按从库设置差异化权重时，请改用 <see cref="ConfigureConnectionConfigs"/> 钩子在代码里赋值。）
    /// </summary>
    public int DefaultSlaveHitRate { get; set; } = 10;

    /// <summary>
    /// 是否启用从库健康探测（现成探针，默认关闭；开启后定期探活并对不可用从库摘除权重、恢复期回填）
    /// </summary>
    public bool EnableSlaveHealthCheck { get; set; } = false;

    /// <summary>
    /// 从库健康探测周期（秒）
    /// </summary>
    public int SlaveHealthCheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// 从库故障冷却时间（秒）：从库探测失败后，在此窗口内即便恢复也暂不回填权重，避免抖动
    /// </summary>
    public int SlaveFailureCooldownSeconds { get; set; } = 120;

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
    /// <remarks>
    /// 纯观测用途：仅用于慢 SQL 日志判定（<see cref="EnableSlowSqlLog"/>），不影响语句执行。
    /// 命令执行超时请配置 <see cref="CommandTimeoutSeconds"/>，两者职责独立；
    /// 若需超时保护，务必保证超时明显大于本阈值，否则慢 SQL 会先被驱动杀掉、慢日志永远记不到。
    /// </remarks>
    public int SlowSqlThresholdMilliseconds { get; set; } = 10000;

    /// <summary>
    /// ADO 命令执行超时（秒）
    /// </summary>
    /// <remarks>
    /// 默认 300 秒（与 SqlSugar 出厂默认一致）；配置 0 或负值表示不覆盖、沿用 SqlSugar 默认。
    /// 注意 ADO.NET 的 <c>CommandTimeout = 0</c> 语义是无限等待，框架刻意不允许经此选项设置为 0。
    /// 个别长任务（DDL、批量导入、导出统计）需要更长超时时，在调用点用 <c>db.Ado.CommandTimeOut</c> 临时覆盖。
    /// </remarks>
    public int CommandTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// 是否启用实体差异日志
    /// </summary>
    /// <remarks>
    /// 开启后仓储的 Insert/Update/Delete 会走 SqlSugar DiffLog AOP，
    /// 自动生成 before/after 快照并调用 <see cref="XiHan.Framework.Auditing.IEntityDiffLogWriter"/> 落库。
    /// 该开关需同时配合业务层实现 <see cref="XiHan.Framework.Auditing.IEntityDiffLogWriter"/>
    /// 与 <see cref="XiHan.Framework.Auditing.IEntityAuditContextProvider"/>；未实现时即便打开也不会产生记录。
    /// </remarks>
    public bool EnableDiffLog { get; set; } = false;

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
