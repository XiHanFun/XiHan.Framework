// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;

namespace XiHan.Framework.Data.SqlSugar.Options;

/// <summary>
/// SqlSugar连接配置
/// </summary>
public class SqlSugarConnectionConfigOptions
{
    /// <summary>
    /// 配置唯一标识
    /// </summary>
    public string ConfigId { get; set; } = "Default";

    /// <summary>
    /// 连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = null!;

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DbType DbType { get; set; }

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
    /// 数据库链接名（原生字段，跨库 DbLink 场景使用，可选）
    /// </summary>
    public string? DbLinkName { get; set; }

    /// <summary>
    /// 语言类型（原生字段，影响 SqlSugar 报错信息本地化，可选）
    /// </summary>
    public LanguageType? LanguageType { get; set; }

    /// <summary>
    /// 索引后缀（原生字段，可选）
    /// </summary>
    public string? IndexSuffix { get; set; }

    /// <summary>
    /// 从库连接配置（原生 <see cref="SlaveConnectionConfig"/>）
    /// </summary>
    /// <remarks>
    /// 注意：<c>SlaveConnectionConfig.HitRate</c> 是**字段**而非属性，无法经 appsettings 绑定（恒为 0）；
    /// 框架在构建时会把 <c>HitRate &lt;= 0</c> 归一化为
    /// <see cref="XiHanSqlSugarCoreOptions.DefaultSlaveHitRate"/>，保证配置的从库能真正分担读流量。
    /// 需要差异化权重或更多原生定制时，请使用 <see cref="XiHanSqlSugarCoreOptions.ConfigureConnectionConfigs"/> 代码钩子。
    /// </remarks>
    public List<SlaveConnectionConfig>? SlaveConnectionConfigs { get; set; }

    /// <summary>
    /// 模块数据源连接配置：本套布局下各模块数据源分别用哪个库
    /// </summary>
    /// <remarks>
    /// <para>
    /// 一条 <see cref="SqlSugarConnectionConfigOptions"/> 描述的是<b>一整套数据库布局</b>——
    /// 主库（本条自身）加上若干模块库（本属性）。实体标 <c>[ModuleDataSource("Erp")]</c> 即落进对应的模块库。
    /// </para>
    /// <para>
    /// 模块库的实际 ConfigId 由框架派生为 <c>{父ConfigId}_{模块数据源名}</c>，配置里无需书写。
    /// 因此模块名不占用顶层 ConfigId 命名空间，与租户连接的命名结构上不相交。
    /// </para>
    /// <para>
    /// 租户独立库时，该租户自成一条连接配置并带上自己的模块库；未声明的模块回退到默认连接的同名模块库，
    /// 于是「租户主库独立、模块库仍共享」这种组合也能表达。
    /// </para>
    /// </remarks>
    public List<SqlSugarModuleDataSourceConfigOptions>? ModuleDataSourceConfigs { get; set; }
}
