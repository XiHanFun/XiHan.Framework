// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Tenanting;

namespace XiHan.Framework.Data.SqlSugar.Clients;

/// <summary>
/// SqlSugar 连接配置器
/// </summary>
/// <remarks>
/// 统一承载「为一个连接补挂全局过滤器 + AOP」以及「运行时幂等注册租户连接」的逻辑，
/// 供初始连接构建（<see cref="global::SqlSugar.SqlSugarScope"/> 配置回调）与运行时动态租户连接共用；
/// 避免运行时新增连接遗漏租户/软删过滤器与审计 AOP。
/// </remarks>
public interface ISqlSugarConnectionConfigurator
{
    /// <summary>
    /// 为指定连接作用域应用全局过滤器与 AOP
    /// </summary>
    /// <param name="provider">连接作用域提供器</param>
    void Configure(SqlSugarScopeProvider provider);

    /// <summary>
    /// 幂等确保租户连接已注册并完成配置，返回其作用域客户端
    /// </summary>
    /// <param name="tenant">SqlSugar 多连接容器</param>
    /// <param name="descriptor">租户连接描述符</param>
    /// <returns>该租户连接的作用域客户端</returns>
    SqlSugarScopeProvider EnsureTenantConnection(ITenant tenant, SqlSugarTenantConnection descriptor);
}
