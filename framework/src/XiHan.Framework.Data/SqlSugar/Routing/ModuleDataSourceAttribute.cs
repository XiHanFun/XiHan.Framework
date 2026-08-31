// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Routing;

/// <summary>
/// 声明实体所属的模块数据源
/// </summary>
/// <remarks>
/// <para>
/// 标注的是<b>库的分组名</b>（如 <c>Erp</c>），不是程序集或模块工程名——
/// 多个模块共用一个库时，它们标同一个名字；官方/基础模块一律不标注，同落主库。
/// </para>
/// <para>
/// 与配置里的 <c>ConnectionConfigs[].ModuleDataSourceConfigs[].ModuleDataSource</c> 同名对应。
/// 实际连接由框架按「当前租户所在的那套布局 + 模块数据源名」派生出 ConfigId
/// <c>{父ConfigId}_{模块数据源名}</c>，因此「模块分库」与「租户分库」是两条独立维度：
/// </para>
/// <code>
/// 平台态 / 未独立库的租户    Erp → Default_Erp
/// 主库独立的租户 1001        Erp → Tenant_1001_Erp（声明了）或 Default_Erp（未声明，回退共享）
/// </code>
/// <para>
/// 声明的模块数据源在当前布局与默认布局里都找不到配置时，框架 fail-closed 抛异常，绝不回落主库——
/// 静默写进另一个库是极难排查的故障。
/// </para>
/// <para>
/// 租户隔离与模块路由是两件事：共享模块库由所有租户共用，实体若实现
/// <see cref="XiHan.Framework.Domain.Entities.Abstracts.IMultiTenantEntity"/>，行级租户过滤照常生效。
/// 同一工作单元跨多个库写入时，每个连接各开一个本地事务，框架不提供跨库分布式事务。
/// </para>
/// <example>
/// <code>
/// [SugarTable("erp_order")]
/// [ModuleDataSource("Erp")]
/// public class ErpOrder : SugarEntity&lt;long&gt; { }
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class ModuleDataSourceAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">模块数据源名</param>
    public ModuleDataSourceAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    /// <summary>
    /// 模块数据源名
    /// </summary>
    public string Name { get; }
}
