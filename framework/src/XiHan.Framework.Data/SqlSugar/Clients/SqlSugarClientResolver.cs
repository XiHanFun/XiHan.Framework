// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Routing;
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
    private readonly IEntityDataSourceResolver _entityDataSourceResolver;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ISqlSugarConnectionConfigurator _connectionConfigurator;
    private readonly ISqlSugarTenantConnectionProvider? _connectionProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="sqlSugarScope">SqlSugar 根作用域</param>
    /// <param name="tenantConnectionResolver">租户连接解析器</param>
    /// <param name="entityDataSourceResolver">实体数据源解析器</param>
    /// <param name="unitOfWorkManager">工作单元管理器</param>
    /// <param name="currentTenant">当前租户</param>
    /// <param name="connectionConfigurator">连接配置器</param>
    /// <param name="connectionProviders">租户连接提供器（可选，业务层实现库隔离时注册；未注册则退化为静态 ConfigId 解析）</param>
    public SqlSugarClientResolver(
        SqlSugarScope sqlSugarScope,
        ISqlSugarTenantConnectionResolver tenantConnectionResolver,
        IEntityDataSourceResolver entityDataSourceResolver,
        IUnitOfWorkManager unitOfWorkManager,
        ICurrentTenant currentTenant,
        ISqlSugarConnectionConfigurator connectionConfigurator,
        IEnumerable<ISqlSugarTenantConnectionProvider> connectionProviders)
    {
        _sqlSugarScope = sqlSugarScope;
        _tenantConnectionResolver = tenantConnectionResolver;
        _entityDataSourceResolver = entityDataSourceResolver;
        _unitOfWorkManager = unitOfWorkManager;
        _currentTenant = currentTenant;
        _connectionConfigurator = connectionConfigurator;
        _connectionProvider = connectionProviders.FirstOrDefault();
    }

    /// <summary>
    /// 获取当前租户对应的客户端，租户声明了独立连接则解析其独立连接，否则按 ConfigId 解析
    /// </summary>
    /// <returns>当前 Scope 级客户端</returns>
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

    /// <summary>
    /// 获取实体对应的客户端：实体声明了数据源取该库，否则按当前租户上下文解析
    /// </summary>
    /// <remarks>
    /// 数据源声明优先于租户连接解析：模块库由所有租户共用，实体行级租户隔离仍由全局过滤器承担。
    /// 声明的 ConfigId 未注册连接时 fail-closed 抛异常，避免静默落到默认库造成跨库串写。
    /// </remarks>
    /// <param name="entityType">实体类型</param>
    /// <returns>Scope 级客户端</returns>
    public ISqlSugarClient GetClientForEntity(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        var configId = _entityDataSourceResolver.ResolveConfigId(entityType);
        if (string.IsNullOrWhiteSpace(configId))
        {
            return GetCurrentClient();
        }

        var normalizedConfigId = configId.Trim();
        if (!_sqlSugarScope.IsAnyConnection(normalizedConfigId))
        {
            throw new XiHanException(
                $"实体 {entityType.FullName} 声明的数据源 [{normalizedConfigId}] 没有对应的连接配置，已按 fail-closed 拒绝请求。" +
                $"请在 {XiHanSqlSugarCoreOptions.SectionName}:ConnectionConfigs 中补齐该 ConfigId 的连接。");
        }

        return GetClient(normalizedConfigId);
    }

    /// <summary>
    /// 按 ConfigId 获取指定客户端，并登记进当前事务型工作单元
    /// </summary>
    /// <param name="configId">连接配置标识</param>
    /// <returns>Scope 级客户端</returns>
    public ISqlSugarClient GetClient(string configId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configId);
        var client = _sqlSugarScope.GetConnectionScope(configId.Trim());
        return EnlistCurrentUnitOfWork(client);
    }

    /// <summary>
    /// 获取全部连接配置标识
    /// </summary>
    /// <returns>连接配置标识集合</returns>
    public IReadOnlyCollection<string> GetAllConfigIds()
    {
        return _tenantConnectionResolver.GetConfigIds();
    }

    /// <summary>
    /// 按顺序获取所有库的客户端（初始化/种子数据等场景使用）
    /// </summary>
    /// <returns>各连接的客户端序列</returns>
    public IEnumerable<ISqlSugarClient> GetAllClients()
    {
        foreach (var configId in GetAllConfigIds())
        {
            yield return _sqlSugarScope.GetConnectionScope(configId);
        }
    }

    /// <summary>
    /// 获取底层 SqlSugarScope（仅在需要多库切换/租户管理等高级场景使用）
    /// </summary>
    /// <returns>多连接容器</returns>
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

        // 事务已被回滚：钉住的连接上事务已失效，此时继续复用会让写入脱离事务被逐条自动提交
        if (unitOfWork.IsRolledback)
        {
            throw new XiHanException(
                $"工作单元 {unitOfWork.Id} 已被回滚，其事务已失效，不能再执行数据操作。" +
                "如需在回滚后写入（例如记录失败原因），请经 IUnitOfWorkManager.Begin(requiresNew: true) 另开工作单元。");
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

        // 要求独立连接的工作单元（requiresNew）不能沿用共享上下文：同一 ConfigId 的 ScopedContext 是同一个实例，
        // 外层工作单元很可能已在它上面开了事务，而同一连接无法嵌套事务。
        // CopyNew 会按同一 ConfigId 物化一条全新连接，并继承构建期挂载的 AOP 与全局过滤器
        // （审计列填充、租户与软删过滤照常生效），因此隔离连接上的写入语义与共享连接一致。
        var requiresIsolation = unitOfWork.Options.RequiresIsolatedConnection;
        if (requiresIsolation)
        {
            concreteClient = concreteClient.CopyNew();
        }

        // 先建事务、成功后再钉住——顺序不可颠倒：若 BeginTran 因瞬时故障抛异常（工厂抛出则事务 API 不落字典），
        // 已钉住的条目会让同一工作单元内的重试在上方命中处短路、永不再尝试开启事务，
        // 整个「事务型」工作单元将静默退化为逐条自动提交。
        var transactionKey = $"{TransactionApiPrefix}:{configId}";
        try
        {
            unitOfWork.GetOrAddTransactionApi(
                transactionKey,
                () => new SqlSugarTransactionApi(
                    concreteClient,
                    unitOfWork.Options.IsolationLevel,
                    requireOwnTransaction: requiresIsolation,
                    ownsClient: requiresIsolation));
        }
        catch
        {
            // 隔离连接尚未登记进工作单元，异常路径必须就地释放，否则连接泄漏。
            if (requiresIsolation)
            {
                concreteClient.Dispose();
            }

            throw;
        }

        unitOfWork.Items[itemKey] = concreteClient;
        return concreteClient;
    }
}
