// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Tenanting;

/// <summary>
/// SqlSugar 租户级数据源连接提供器（可选，由业务层实现并注册）
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="ISqlSugarTenantConnectionProvider"/> 同构，区别只在多了一个数据源名维度：
/// 前者回答「租户 X 的主库在哪」，本接口回答「租户 X 的 Erp 库在哪」。
/// 两者共同把「模块分库」与「租户分库」正交化。
/// </para>
/// <list type="bullet">
///   <item>返回 <c>null</c>：该租户在这个数据源上不需要独立库，落共享的模块库；</item>
///   <item>返回描述符：框架据此在运行时幂等注册该连接并补挂全局过滤器与 AOP；</item>
///   <item>抛出异常：fail-closed，请求失败而非静默退化。</item>
/// </list>
/// <para>
/// 实现须自行缓存，并避免在解析过程中递归查询「当前租户连接」——读取租户元数据时应显式使用平台/默认连接。
/// </para>
/// </remarks>
public interface ISqlSugarTenantDataSourceProvider
{
    /// <summary>
    /// 解析指定租户在指定数据源上的独立连接描述符
    /// </summary>
    /// <param name="tenantId">当前租户标识</param>
    /// <param name="tenantName">当前租户名称（可空）</param>
    /// <param name="dataSourceName">逻辑数据源名</param>
    /// <returns>需要独立库时返回描述符；落共享模块库时返回 <c>null</c></returns>
    SqlSugarTenantConnection? Resolve(long tenantId, string? tenantName, string dataSourceName);
}
