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
/// <para>
/// <b>事务钉连接</b>：<see cref="SqlSugarScope"/> 按异步上下文惰性创建客户端，且 AsyncLocal 在 async 方法返回后不回流调用方——
/// 若事务型工作单元期间持有 <see cref="SqlSugarScopeProvider"/>（其每次 <c>.Ado</c> 都重新解析当前上下文），
/// 同一工作单元内后续仓储调用可能落在无事务的新上下文连接上自动提交，而提交帧解析到的裸 provider 对空事务静默 no-op，
/// 造成首个写入永不提交（静默丢写）。因此事务型工作单元首次触达某 ConfigId 时，立即物化当前帧的
/// <see cref="SqlSugarScopeProvider.ScopedContext"/>（具体 <see cref="SqlSugarProvider"/>）钉入 <c>IUnitOfWork.Items</c>，
/// 同一工作单元内的所有后续解析直接复用，保证全部操作与 Begin/Commit/Rollback 落在同一连接、同一事务上。
/// 工作单元内的数据访问是顺序语义（并行访问共享连接本就不受 SqlSugar 支持），Items 用普通字典无并发问题。
/// </para>
/// </remarks>
public sealed class SqlSugarClientResolver : ISqlSugarClientResolver
{
    private const string TransactionApiPrefix = "SqlSugarTransaction";
    private const string TransactionClientItemPrefix = "SqlSugarTransactionClient";

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
                return EnlistCurrentUnitOfWork(tenantClient);
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
        return EnlistCurrentUnitOfWork(client);
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

    /// <summary>
    /// 将客户端登记进当前事务型工作单元，并返回本次操作应使用的客户端。
    /// </summary>
    /// <remarks>
    /// 非事务场景原样返回传入的 <see cref="SqlSugarScopeProvider"/>；
    /// 事务型工作单元内返回钉住的具体 <see cref="SqlSugarProvider"/>（见类型注释「事务钉连接」），
    /// 确保同一工作单元的所有数据操作与事务生命周期落在同一连接上。
    /// </remarks>
    /// <param name="client">按 ConfigId 解析出的作用域客户端</param>
    /// <returns>本次操作应使用的客户端</returns>
    private ISqlSugarClient EnlistCurrentUnitOfWork(ISqlSugarClient client)
    {
        var unitOfWork = _unitOfWorkManager.Current;
        if (unitOfWork is null ||
            unitOfWork.IsReserved ||
            unitOfWork.IsDisposed ||
            unitOfWork.IsCompleted ||
            !unitOfWork.Options.IsTransactional)
        {
            return client;
        }

        var configId = client.CurrentConnectionConfig.ConfigId?.ToString();
        if (string.IsNullOrWhiteSpace(configId))
        {
            return client;
        }

        // 同一工作单元已钉住该 ConfigId 的具体连接 → 直接复用（钉住动作在事务 API 创建成功之后才发生，命中即事务在位）
        var itemKey = $"{TransactionClientItemPrefix}:{configId}";
        if (unitOfWork.Items.TryGetValue(itemKey, out var pinned) && pinned is ISqlSugarClient pinnedClient)
        {
            return pinnedClient;
        }

        // 首次触达：物化当前帧的具体 provider，事务在这个具体连接上开启。
        // 此后同一工作单元内无论异步上下文如何流转，操作与 Commit/Rollback 都作用于它。
        var concreteClient = client is SqlSugarScopeProvider scopeProvider ? scopeProvider.ScopedContext : client;

        // 先建事务、成功后再钉住——顺序不可颠倒：若 BeginTran 因瞬时故障抛异常（工厂抛出则事务 API 不落字典），
        // 已钉住的条目会让同一工作单元内的重试在上方命中处短路、永不再尝试开启事务，
        // 整个「事务型」工作单元将静默退化为逐条自动提交。
        var transactionKey = $"{TransactionApiPrefix}:{configId}";
        unitOfWork.GetOrAddTransactionApi(
            transactionKey,
            () => new SqlSugarTransactionApi(concreteClient, unitOfWork.Options.IsolationLevel));

        unitOfWork.Items[itemKey] = concreteClient;
        return concreteClient;
    }
}
