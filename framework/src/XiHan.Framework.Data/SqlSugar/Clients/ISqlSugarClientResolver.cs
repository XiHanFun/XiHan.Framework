// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;

namespace XiHan.Framework.Data.SqlSugar.Clients;

/// <summary>
/// SqlSugar 客户端解析器
/// </summary>
/// <remarks>
/// 负责根据实体声明的数据源或当前租户上下文解析 ConfigId 并返回对应的 <see cref="ISqlSugarClient"/>。
/// 若当前存在事务型工作单元，解析器会自动把当前连接加入工作单元事务。
/// 底层初始化器（DbInitializer/DataSeeder 等）通过 <see cref="GetClient(string)"/>/<see cref="GetAllClients"/> 遍历所有库。
/// </remarks>
public interface ISqlSugarClientResolver
{
    /// <summary>
    /// 获取当前租户对应的客户端
    /// </summary>
    /// <returns>当前 Scope 级客户端</returns>
    ISqlSugarClient GetCurrentClient();

    /// <summary>
    /// 获取实体对应的客户端
    /// </summary>
    /// <remarks>
    /// 实体经 <c>[DataSource("XXX")]</c> 声明了数据源则返回该库的客户端，否则等同 <see cref="GetCurrentClient"/>。
    /// 声明的 ConfigId 没有对应连接时抛异常，不回退默认库。
    /// </remarks>
    /// <param name="entityType">实体类型</param>
    /// <returns>Scope 级客户端</returns>
    ISqlSugarClient GetClientForEntity(Type entityType);

    /// <summary>
    /// 获取实体对应的客户端
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <returns>Scope 级客户端</returns>
    ISqlSugarClient GetClientForEntity<TEntity>()
    {
        return GetClientForEntity(typeof(TEntity));
    }

    /// <summary>
    /// 按 ConfigId 获取指定客户端
    /// </summary>
    /// <param name="configId">连接配置标识</param>
    /// <returns>Scope 级客户端</returns>
    ISqlSugarClient GetClient(string configId);

    /// <summary>
    /// 获取全部连接配置标识
    /// </summary>
    IReadOnlyCollection<string> GetAllConfigIds();

    /// <summary>
    /// 获取当前租户所在布局的全部连接配置标识：主库在前，其下已建连的模块库在后
    /// </summary>
    /// <returns>连接配置标识集合</returns>
    IReadOnlyList<string> GetCurrentLayoutConfigIds();

    /// <summary>
    /// 获取当前工作单元已登记的连接配置标识
    /// </summary>
    /// <remarks>
    /// 无当前工作单元、工作单元非事务型或已结束时返回空集合。
    /// 返回顺序不作承诺，调用方不得依赖顺序表达优先级。
    /// </remarks>
    /// <returns>已登记的连接配置标识</returns>
    IReadOnlyList<string> GetEnlistedConfigIds()
    {
        return [];
    }

    /// <summary>
    /// 按顺序获取所有库的客户端（初始化/种子数据等场景使用）
    /// </summary>
    IEnumerable<ISqlSugarClient> GetAllClients();

    /// <summary>
    /// 底层 SqlSugarScope（仅在需要多库切换/租户管理等高级场景使用）
    /// </summary>
    ITenant AsTenant();
}
