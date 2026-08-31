// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;

namespace XiHan.Framework.Data.SqlSugar.Options;

/// <summary>
/// 模块数据源连接配置：挂在某条连接之下，声明该套布局里某个模块数据源用哪个库
/// </summary>
/// <remarks>
/// <para>
/// 除 <see cref="ModuleDataSource"/> 外全部字段都是<b>可选覆盖</b>：不填就继承所属的父连接。
/// 所以最常见的写法只有两行——模块名 + 连接串，数据库类型、自动关闭、从库等一律跟随主库。
/// </para>
/// <para>
/// <see cref="ConnectionString"/> 留空表示<b>该模块不分库</b>，直接用父连接的主库；
/// 这与「压根没有这一条」不同——后者是未配置，框架会 fail-closed 抛异常。
/// </para>
/// </remarks>
public class SqlSugarModuleDataSourceConfigOptions
{
    /// <summary>
    /// 模块数据源名，与实体上的 <c>[ModuleDataSource("Erp")]</c> 同名
    /// </summary>
    /// <remarks>
    /// 它是<b>库的分组名</b>，不是程序集或模块工程名——多个模块共用一个库时，它们标同一个名字。
    /// </remarks>
    public string ModuleDataSource { get; set; } = string.Empty;

    /// <summary>
    /// 模块库连接字符串；留空表示该模块不分库、直接用父连接的主库
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// 数据库类型；不填继承父连接
    /// </summary>
    public DbType? DbType { get; set; }

    /// <summary>
    /// 是否自动关闭连接；不填继承父连接
    /// </summary>
    public bool? IsAutoCloseConnection { get; set; }

    /// <summary>
    /// 初始化键类型；不填继承父连接
    /// </summary>
    public InitKeyType? InitKeyType { get; set; }

    /// <summary>
    /// 更多设置；不填继承父连接
    /// </summary>
    public ConnMoreSettings? MoreSettings { get; set; }

    /// <summary>
    /// 从库连接配置；不填继承父连接，填了则整体替换
    /// </summary>
    /// <remarks>
    /// 模块库通常有自己的从库，因此这里是「替换」而不是「合并」——
    /// 把父连接的从库照搬到另一个库上是错的。
    /// </remarks>
    public List<SlaveConnectionConfig>? SlaveConnectionConfigs { get; set; }

    /// <summary>
    /// 数据库链接名（原生字段）；不填继承父连接
    /// </summary>
    public string? DbLinkName { get; set; }

    /// <summary>
    /// 语言类型（原生字段）；不填继承父连接
    /// </summary>
    public LanguageType? LanguageType { get; set; }

    /// <summary>
    /// 索引后缀（原生字段）；不填继承父连接
    /// </summary>
    public string? IndexSuffix { get; set; }
}
