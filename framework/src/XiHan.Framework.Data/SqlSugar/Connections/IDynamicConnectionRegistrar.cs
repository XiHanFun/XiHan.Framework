// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;

namespace XiHan.Framework.Data.SqlSugar.Connections;

/// <summary>
/// 动态连接注册器（把运行期才知道的外部数据库连接登记进 SqlSugar）
/// </summary>
/// <remarks>
/// 面向"连接信息存在业务库里、启动时无从配置"的场景，典型如代码生成器的外部数据源。
/// <para>
/// 注册的连接**只用于读取库表元数据**：不挂全局过滤器、不挂 AOP、不参与多租户解析。
/// 外部库不是本系统的实体，套上这些会直接抛错。
/// </para>
/// <para>
/// 进程内记账避免重复注册；同一 ConfigId 重复调用 <see cref="Register"/> 为幂等空操作。
/// </para>
/// </remarks>
public interface IDynamicConnectionRegistrar
{
    /// <summary>
    /// 指定连接是否已注册
    /// </summary>
    /// <param name="configId">连接配置标识</param>
    bool IsRegistered(string configId);

    /// <summary>
    /// 注册连接（已存在则为空操作）
    /// </summary>
    /// <param name="descriptor">连接描述</param>
    void Register(DynamicConnectionDescriptor descriptor);

    /// <summary>
    /// 获取已注册连接的客户端
    /// </summary>
    /// <remarks>
    /// 不登记进当前工作单元：外部库的读取不应参与本地业务事务，
    /// 否则会在别人的库上开启事务并绑定到本地 UoW 的提交/回滚生命周期。
    /// </remarks>
    /// <param name="configId">连接配置标识</param>
    /// <returns>客户端；未注册时返回 null</returns>
    ISqlSugarClient? GetClient(string configId);
}

/// <summary>
/// 动态连接描述
/// </summary>
/// <param name="ConfigId">连接配置标识（全局唯一）</param>
/// <param name="DbType">数据库类型</param>
/// <param name="ConnectionString">连接字符串</param>
public sealed record DynamicConnectionDescriptor(string ConfigId, DbType DbType, string ConnectionString);
