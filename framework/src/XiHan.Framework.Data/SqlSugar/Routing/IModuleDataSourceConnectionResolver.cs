// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;

namespace XiHan.Framework.Data.SqlSugar.Routing;

/// <summary>
/// 模块数据源连接解析器：把「模块数据源名 + 当前租户所在布局」解析成实际连接
/// </summary>
/// <remarks>
/// 这是「模块分库」与「租户分库」两条维度的交汇点。
/// 业务层要自定义整套路由策略时用 <c>services.Replace</c> 替换本服务；
/// 只想给某些租户配独立库，在 <c>ISqlSugarTenantConnectionProvider</c> 返回的描述符里
/// 带上该租户的模块库即可，不必替换本服务。
/// </remarks>
public interface IModuleDataSourceConnectionResolver
{
    /// <summary>
    /// 解析模块数据源在当前租户布局下对应的客户端
    /// </summary>
    /// <param name="moduleDataSource">模块数据源名</param>
    /// <param name="parentConfigId">当前租户所在布局的父连接 ConfigId</param>
    /// <returns>该模块库的 Scope 级客户端</returns>
    ISqlSugarClient ResolveClient(string moduleDataSource, string parentConfigId);
}
