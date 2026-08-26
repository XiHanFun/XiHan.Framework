// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Initializers;

/// <summary>
/// 本次初始化所处的库上下文
/// </summary>
/// <remarks>
/// 由 <see cref="IDbInitializer"/> 在每个连接上构造，交给 <see cref="IDbEntityTypeProvider"/> 与
/// <see cref="IDataSeederSelector"/> 判定当前库该建哪些表、跑哪些种子。
/// </remarks>
public sealed class DbInitializationContext
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="connectionConfigId">当前连接配置标识</param>
    /// <param name="tenantId">当前租户标识</param>
    /// <param name="isTenantDatabase">当前连接是否租户独立库</param>
    public DbInitializationContext(string? connectionConfigId, long? tenantId, bool isTenantDatabase)
    {
        ConnectionConfigId = connectionConfigId;
        TenantId = tenantId;
        IsTenantDatabase = isTenantDatabase;
    }

    /// <summary>
    /// 当前连接配置标识
    /// </summary>
    public string? ConnectionConfigId { get; }

    /// <summary>
    /// 当前租户标识，平台上下文为 null
    /// </summary>
    public long? TenantId { get; }

    /// <summary>
    /// 当前连接是否租户独立库（ConfigId 以租户连接前缀开头）
    /// </summary>
    public bool IsTenantDatabase { get; }

    /// <summary>
    /// 当前连接对应的初始化目标
    /// </summary>
    public DbInitializationTarget Target => IsTenantDatabase ? DbInitializationTarget.Tenant : DbInitializationTarget.Platform;
}
