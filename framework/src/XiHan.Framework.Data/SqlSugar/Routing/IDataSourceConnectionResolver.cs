// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;

namespace XiHan.Framework.Data.SqlSugar.Routing;

/// <summary>
/// 数据源连接解析器：把「逻辑数据源名 + 当前租户」解析成实际连接
/// </summary>
/// <remarks>
/// 这是「模块分库」与「租户分库」两条维度的交汇点。
/// 业务层要自定义整套路由策略时用 <c>services.Replace</c> 替换本服务；
/// 只想给某些租户的某些数据源指定独立库，实现
/// <see cref="XiHan.Framework.Data.SqlSugar.Tenanting.ISqlSugarTenantDataSourceProvider"/> 即可，不必替换本服务。
/// </remarks>
public interface IDataSourceConnectionResolver
{
    /// <summary>
    /// 解析逻辑数据源在当前租户下对应的客户端
    /// </summary>
    /// <param name="dataSourceName">逻辑数据源名</param>
    /// <returns>该数据源对应的 Scope 级客户端</returns>
    ISqlSugarClient ResolveClient(string dataSourceName);
}
