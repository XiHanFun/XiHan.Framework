// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;

namespace XiHan.Framework.Data.SqlSugar.Tenanting;

/// <summary>
/// SqlSugar 租户连接描述符
/// </summary>
/// <param name="ConfigId">连接配置标识（须全局唯一，建议带租户前缀避免与平台连接撞号，如 <c>Tenant_123</c>）</param>
/// <param name="ConnectionString">数据库连接字符串（明文，调用方负责解密后传入）</param>
/// <param name="DbType">数据库类型</param>
/// <param name="IsAutoCloseConnection">是否自动关闭连接</param>
/// <param name="SlaveConnectionConfigs">
/// 从库连接配置（可选，让库隔离租户也享受读写分离）。
/// 权重为 0 的从库会被框架按 <c>DefaultSlaveHitRate</c> 归一化；构建前钩子对该租户连接同样生效。
/// </param>
public sealed record SqlSugarTenantConnection(
    string ConfigId,
    string ConnectionString,
    DbType DbType,
    bool IsAutoCloseConnection = true,
    List<SlaveConnectionConfig>? SlaveConnectionConfigs = null);
