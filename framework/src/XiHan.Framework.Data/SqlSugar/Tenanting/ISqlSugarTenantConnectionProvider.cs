#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISqlSugarTenantConnectionProvider
// Guid:6b1e9f2a-8c47-4d3b-9a1e-2f5c7d0b4a63
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 09:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Data.SqlSugar.Tenanting;

/// <summary>
/// SqlSugar 租户连接提供器（可选，由业务层实现并注册）
/// </summary>
/// <remarks>
/// 用于「库隔离」多租户：框架在解析当前租户对应的 <see cref="global::SqlSugar.ISqlSugarClient"/> 时，
/// 若存在已注册的提供器且当前处于租户上下文，则先咨询本提供器：
/// <list type="bullet">
///   <item>返回 <c>null</c>：该租户不需要独立连接（字段/行隔离），走默认平台连接；</item>
///   <item>返回描述符：框架据此在运行时幂等注册该租户连接（<c>AddConnection</c>）并补挂全局过滤器与 AOP；</item>
///   <item>抛出异常：视为 fail-closed（如声明库隔离却缺连接串、或声明了尚未支持的隔离模式），请求失败而非静默退化为行隔离。</item>
/// </list>
/// 实现须自行缓存，并避免在解析过程中递归查询「当前租户连接」——读取租户元数据时应显式使用平台/默认连接。
/// </remarks>
public interface ISqlSugarTenantConnectionProvider
{
    /// <summary>
    /// 解析指定租户的独立连接描述符
    /// </summary>
    /// <param name="tenantId">当前租户标识</param>
    /// <param name="tenantName">当前租户名称（可空）</param>
    /// <returns>需要独立连接时返回描述符；无需独立连接（走默认连接）时返回 <c>null</c></returns>
    SqlSugarTenantConnection? Resolve(long tenantId, string? tenantName);
}
