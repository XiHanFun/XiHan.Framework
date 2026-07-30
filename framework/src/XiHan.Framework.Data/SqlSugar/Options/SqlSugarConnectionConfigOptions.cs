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
}
