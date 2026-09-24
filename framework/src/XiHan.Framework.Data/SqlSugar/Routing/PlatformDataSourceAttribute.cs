// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Routing;

/// <summary>
/// 声明实体固定落在平台库（默认布局的主库），不随当前租户的独立库切换
/// </summary>
/// <remarks>
/// <para>
/// 库隔离部署下，租户的业务与运行数据落各自的独立库；而平台目录、账号、成员关系与读共享的模板
/// 要跨租户读取、或与平台数据在同一条 SQL 里关联，只能集中在平台库。标注后：
/// </para>
/// <list type="bullet">
/// <item>运行期无论当前租户是否独立库，读写都走平台库；行级租户过滤照常生效（TenantId 仍在行上）。</item>
/// <item>建表只在平台库建，不进租户独立库与模块库。</item>
/// </list>
/// <para>
/// 字段隔离的租户本就落在平台库，标注对它们没有任何影响。
/// 不能与 <see cref="ModuleDataSourceAttribute"/> 同时声明：平台库与模块库是两个互斥的落点。
/// 同一工作单元同时写平台库与租户独立库时，每个连接各开一个本地事务，框架不提供跨库分布式事务。
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class PlatformDataSourceAttribute : Attribute;
