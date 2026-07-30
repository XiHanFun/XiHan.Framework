// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;

namespace XiHan.Framework.Data.SqlSugar.Clients;

/// <summary>
/// SqlSugar 客户端解析器
/// </summary>
/// <remarks>
/// 仅负责根据当前租户上下文解析 ConfigId 并返回对应的 <see cref="ISqlSugarClient"/>。
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
    /// 按顺序获取所有库的客户端（初始化/种子数据等场景使用）
    /// </summary>
    IEnumerable<ISqlSugarClient> GetAllClients();

    /// <summary>
    /// 底层 SqlSugarScope（仅在需要多库切换/租户管理等高级场景使用）
    /// </summary>
    ITenant AsTenant();
}
