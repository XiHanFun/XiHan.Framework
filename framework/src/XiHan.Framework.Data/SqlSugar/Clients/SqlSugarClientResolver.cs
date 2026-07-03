#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarClientResolver
// Guid:8d6f1c89-2e4a-4a7f-9d9b-1b5f6a8c3e2d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/17 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Tenanting;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Uow;

namespace XiHan.Framework.Data.SqlSugar.Clients;

/// <summary>
/// SqlSugar 客户端解析器默认实现
/// </summary>
/// <remarks>
/// 基于 <see cref="SqlSugarScope"/>（线程安全单例）+ <see cref="ISqlSugarTenantConnectionResolver"/> 组合。
/// 当前租户上下文变化时，由 <c>ISqlSugarTenantConnectionResolver</c> 重新解析 ConfigId。
/// </remarks>
public sealed class SqlSugarClientResolver : ISqlSugarClientResolver
{
    private const string TransactionApiPrefix = "SqlSugarTransaction";

    private readonly SqlSugarScope _sqlSugarScope;
    private readonly ISqlSugarTenantConnectionResolver _tenantConnectionResolver;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ISqlSugarConnectionConfigurator _connectionConfigurator;
    private readonly ISqlSugarTenantConnectionProvider? _connectionProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sqlSugarScope">SqlSugar 根作用域</param>
    /// <param name="tenantConnectionResolver">租户连接解析器</param>
    /// <param name="unitOfWorkManager">工作单元管理器</param>
    /// <param name="currentTenant">当前租户</param>
    /// <param name="connectionConfigurator">连接配置器</param>
    /// <param name="connectionProviders">租户连接提供器（可选，业务层实现库隔离时注册；未注册则退化为静态 ConfigId 解析）</param>
    public SqlSugarClientResolver(
        SqlSugarScope sqlSugarScope,
        ISqlSugarTenantConnectionResolver tenantConnectionResolver,
        IUnitOfWorkManager unitOfWorkManager,
        ICurrentTenant currentTenant,
        ISqlSugarConnectionConfigurator connectionConfigurator,
        IEnumerable<ISqlSugarTenantConnectionProvider> connectionProviders)
    {
        _sqlSugarScope = sqlSugarScope;
        _tenantConnectionResolver = tenantConnectionResolver;
        _unitOfWorkManager = unitOfWorkManager;
        _currentTenant = currentTenant;
        _connectionConfigurator = connectionConfigurator;
        _connectionProvider = connectionProviders.FirstOrDefault();
    }

    /// <inheritdoc />
    public ISqlSugarClient GetCurrentClient()
    {
        // 库隔离：存在租户连接提供器且处于租户上下文时，优先解析该租户的独立连接
        // 提供器返回 null → 走静态 ConfigId 解析（字段/行隔离）；抛异常 → fail-closed
        if (_connectionProvider is not null && _currentTenant.Id is { } tenantId)
        {
            var descriptor = _connectionProvider.Resolve(tenantId, _currentTenant.Name);
            if (descriptor is not null)
            {
                var tenantClient = _connectionConfigurator.EnsureTenantConnection(_sqlSugarScope, descriptor);
                EnlistCurrentUnitOfWork(tenantClient);
                return tenantClient;
            }
        }

        var configId = _tenantConnectionResolver.ResolveCurrentConfigId();
        return GetClient(configId);
    }

    /// <inheritdoc />
    public ISqlSugarClient GetClient(string configId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configId);
        var client = _sqlSugarScope.GetConnectionScope(configId.Trim());
        EnlistCurrentUnitOfWork(client);
        return client;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetAllConfigIds()
    {
        return _tenantConnectionResolver.GetConfigIds();
    }

    /// <inheritdoc />
    public IEnumerable<ISqlSugarClient> GetAllClients()
    {
        foreach (var configId in GetAllConfigIds())
        {
            yield return _sqlSugarScope.GetConnectionScope(configId);
        }
    }

    /// <inheritdoc />
    public ITenant AsTenant()
    {
        return _sqlSugarScope;
    }

    private void EnlistCurrentUnitOfWork(ISqlSugarClient client)
    {
        var unitOfWork = _unitOfWorkManager.Current;
        if (unitOfWork is null ||
            unitOfWork.IsReserved ||
            unitOfWork.IsDisposed ||
            unitOfWork.IsCompleted ||
            !unitOfWork.Options.IsTransactional)
        {
            return;
        }

        var configId = client.CurrentConnectionConfig.ConfigId?.ToString();
        if (string.IsNullOrWhiteSpace(configId))
        {
            return;
        }

        var transactionKey = $"{TransactionApiPrefix}:{configId}";
        unitOfWork.GetOrAddTransactionApi(
            transactionKey,
            () => new SqlSugarTransactionApi(client, unitOfWork.Options.IsolationLevel));
    }
}
