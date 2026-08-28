// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Routing;

/// <summary>
/// 声明实体所属的数据源（连接配置标识）
/// </summary>
/// <remarks>
/// 标注后该实体的仓储读写固定落在此 <c>ConfigId</c> 的库上，不随当前租户上下文切换连接；
/// 建表初始化也只在该库执行，其它库不建此表。
/// 未标注的实体沿用「按当前租户解析连接」的既有行为。
/// 标注在基类上对派生实体同样生效。
/// <para>
/// 租户隔离与数据源路由是两件事：模块库由所有租户共用，实体若实现
/// <see cref="XiHan.Framework.Domain.Entities.Abstracts.IMultiTenantEntity"/>，行级租户过滤在模块库上照常生效。
/// </para>
/// <para>
/// 同一工作单元跨多个数据源写入时，每个 <c>ConfigId</c> 各开一个本地事务，
/// 框架不提供跨库分布式事务。
/// </para>
/// <example>
/// <code>
/// [SugarTable("erp_order")]
/// [DataSource("Erp")]
/// public class ErpOrder : SugarEntity { }
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class DataSourceAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="configId">连接配置标识，须与 <c>XiHan:Data:SqlSugarCore:ConnectionConfigs</c> 中某项的 ConfigId 一致</param>
    public DataSourceAttribute(string configId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configId);
        ConfigId = configId.Trim();
    }

    /// <summary>
    /// 连接配置标识
    /// </summary>
    public string ConfigId { get; }
}
