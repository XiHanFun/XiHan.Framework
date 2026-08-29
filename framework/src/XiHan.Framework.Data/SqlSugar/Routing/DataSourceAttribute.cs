// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Routing;

/// <summary>
/// 声明实体所属的逻辑数据源
/// </summary>
/// <remarks>
/// <para>
/// 标注的是**逻辑数据源名**（如 <c>Erp</c>），不是连接标识。落到哪条连接由
/// <see cref="IDataSourceConnectionResolver"/> 按「数据源名 + 当前租户」解析：
/// 平台态或字段隔离租户落共享的模块库，库隔离租户落该租户自己的模块库。
/// 这样「模块分库」与「租户分库」是两条独立的维度，可以任意组合。
/// </para>
/// <para>
/// 多个实体标同一个名字即共用一个库——建议官方/基础模块一律不标注（同落主库），
/// 只有确实要独立成库的业务模块才标。
/// </para>
/// <para>
/// 建表初始化同口径：声明了数据源的实体只在该数据源的库里建表。
/// 数据源名解析不到任何连接时 fail-closed 抛异常，不回落主库。
/// </para>
/// <para>
/// 租户隔离与数据源路由是两件事：共享模块库由所有租户共用，实体若实现
/// <see cref="XiHan.Framework.Domain.Entities.Abstracts.IMultiTenantEntity"/>，行级租户过滤照常生效。
/// </para>
/// <para>
/// 同一工作单元跨多个数据源写入时，每个连接各开一个本地事务，框架不提供跨库分布式事务。
/// </para>
/// <example>
/// <code>
/// [SugarTable("erp_order")]
/// [DataSource("Erp")]
/// public class ErpOrder : SugarEntity&lt;long&gt; { }
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class DataSourceAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">逻辑数据源名</param>
    public DataSourceAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    /// <summary>
    /// 逻辑数据源名
    /// </summary>
    public string Name { get; }
}
