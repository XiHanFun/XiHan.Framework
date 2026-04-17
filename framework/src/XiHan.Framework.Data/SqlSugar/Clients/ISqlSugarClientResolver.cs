#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISqlSugarClientResolver
// Guid:3a1f0b0e-9c6f-4b89-8d47-0f87a2e5c6a1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/17 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using XiHan.Framework.Uow.Abstracts;

namespace XiHan.Framework.Data.SqlSugar.Clients;

/// <summary>
/// SqlSugar 客户端解析器
/// </summary>
/// <remarks>
/// 仅负责根据当前租户上下文解析 ConfigId 并返回对应的 <see cref="ISqlSugarClient"/>。
/// 仓储层直接注入 <see cref="ISqlSugarClient"/> 即可，不需要再看到此解析器。
/// 底层初始化器（DbInitializer/DataSeeder 等）通过 <see cref="GetClient(string)"/>/<see cref="GetAllClients"/> 遍历所有库。
/// 同时实现 <see cref="IDatabaseApi"/> 以便 UnitOfWork 作为数据库访问句柄注入。
/// </remarks>
public interface ISqlSugarClientResolver : IDatabaseApi
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
