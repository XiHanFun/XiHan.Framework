// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Routing;
using XiHan.Framework.Data.SqlSugar.Tenanting;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Uow.Options;

namespace XiHan.Framework.Data.Tests;

/// <summary>
/// <c>requiresNew</c> 工作单元的物理连接与事务隔离测试。
/// </summary>
/// <remarks>
/// <para>
/// 这些用例跑在真实 SQLite 上，走完整的 <see cref="SqlSugarClientResolver"/> 与工作单元管理器，
/// 断言的是「内层提交在外层回滚后依然存在」这条只能由真实事务证明的不变量。
/// </para>
/// <para>
/// 这里刻意不使用任何工作单元替身：本不变量此前长期静默失效，正是因为测试把事务语义模拟掉了。
/// </para>
/// <para>
/// <b>SQLite 覆盖不到的部分</b>：SQLite 是库级单写者，同一个库文件上无法同时存在两个写事务，
/// 因此「外层与内层落在同一个库」的隔离提交无法在这里验证——内层取得独立连接后会直接撞上
/// <c>database is locked</c>。该形态需在 PostgreSQL 或 MySQL 上验证。
/// 这不是取巧回避：同库隔离本身就意味着内外两条连接会争锁，见
/// <see cref="XiHan.Framework.Uow.Options.IXiHanUnitOfWorkOptions.RequiresIsolatedConnection"/> 的调用方义务。
/// </para>
/// </remarks>
public sealed class RequiresNewIsolationTests : IDisposable
{
    private const string OuterConfigId = "Outer";
    private const string InnerConfigId = "Inner";

    private readonly string _outerDatabasePath = Path.Combine(Path.GetTempPath(), $"xihan-uow-outer-{Guid.NewGuid():N}.db");
    private readonly string _innerDatabasePath = Path.Combine(Path.GetTempPath(), $"xihan-uow-inner-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _serviceProvider;
    private readonly SqlSugarScope _scope;
    private readonly SqlSugarClientResolver _resolver;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    /// <summary>
    /// 建立两库、真实工作单元基础设施与客户端解析器。
    /// </summary>
    public RequiresNewIsolationTests()
    {
        _scope = new SqlSugarScope(
        [
            BuildConfig(OuterConfigId, _outerDatabasePath),
            BuildConfig(InnerConfigId, _innerDatabasePath),
        ]);
        _scope.GetConnectionScope(OuterConfigId).CodeFirst.InitTables<Note>();
        _scope.GetConnectionScope(InnerConfigId).CodeFirst.InitTables<Note>();

        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();
        services.AddSingleton<IAmbientUnitOfWork, AmbientUnitOfWork>();
        services.AddSingleton<IUnitOfWorkEventPublisher, NullUnitOfWorkEventPublisher>();
        services.AddTransient<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IUnitOfWorkManager, UnitOfWorkManager>();
        _serviceProvider = services.BuildServiceProvider();
        _unitOfWorkManager = _serviceProvider.GetRequiredService<IUnitOfWorkManager>();

        _resolver = new SqlSugarClientResolver(
            _scope,
            new FixedTenantConnectionResolver(OuterConfigId, [OuterConfigId, InnerConfigId]),
            new EntityDataSourceResolver(),
            _unitOfWorkManager,
            new NoTenant(),
            new PassThroughConnectionConfigurator(),
            []);
    }

    /// <summary>
    /// 内层 requiresNew 工作单元完成后，其写入必须不受外层回滚影响。
    /// </summary>
    /// <remarks>
    /// 覆盖范围要说清楚：本例中外层从未触达内层那个库，因此它验证的是<b>内层提交路径本身独立生效</b>，
    /// 并<b>不</b>复现「目标库已被外层工作单元登记并开启事务」这个真正触发缺陷的前提——
    /// 复现它需要内外两条连接同时在同一个库上持有事务，而 SQLite 是库级单写者，做不到。
    /// 那一层由 <see cref="RequiresNew_ShouldMaterializeClientDistinctFromSharedContext"/> 从物化行为侧覆盖，
    /// 端到端形态需在 PostgreSQL 或 MySQL 上验证。
    /// </remarks>
    [Fact]
    public async Task RequiresNew_InnerCommitShouldSurviveOuterRollback()
    {
        using (var outer = _unitOfWorkManager.Begin(new XiHanUnitOfWorkOptions(isTransactional: true)))
        {
            Insert(OuterConfigId, 1, "outer");

            using (var inner = _unitOfWorkManager.Begin(new XiHanUnitOfWorkOptions(isTransactional: true), requiresNew: true))
            {
                Insert(InnerConfigId, 2, "inner");
                await inner.CompleteAsync();
            }

            // 外层不 Complete，随 Dispose 回滚。
        }

        Assert.Equal(0, CountWithFreshConnection(_outerDatabasePath));
        Assert.Equal(1, CountWithFreshConnection(_innerDatabasePath));
    }

    /// <summary>
    /// 隔离工作单元必须物化出一条新的物理客户端，而不是复用共享上下文。
    /// </summary>
    /// <remarks>
    /// 这是修复的核心：同一 ConfigId 的 <c>ScopedContext</c> 是同一个实例，
    /// 复用它意味着复用外层可能已开启事务的那条连接，内层提交随之退化为空操作。
    /// 用例让外层工作单元存在但尚未触达该库，从而在 SQLite 单写者限制下仍能观察到物化行为。
    /// </remarks>
    [Fact]
    public void RequiresNew_ShouldMaterializeClientDistinctFromSharedContext()
    {
        var sharedContext = ((SqlSugarScopeProvider)_scope.GetConnectionScope(OuterConfigId)).ScopedContext;

        using var outer = _unitOfWorkManager.Begin(new XiHanUnitOfWorkOptions(isTransactional: true));
        using var inner = _unitOfWorkManager.Begin(new XiHanUnitOfWorkOptions(isTransactional: true), requiresNew: true);
        var innerClient = _resolver.GetClient(OuterConfigId);

        Assert.NotSame(sharedContext, innerClient);
        // 隔离连接仍指向同一个库，只是换了物理连接。
        Assert.Equal(OuterConfigId, innerClient.CurrentConnectionConfig.ConfigId?.ToString());
    }

    /// <summary>
    /// 非隔离工作单元仍应复用共享上下文，避免为普通事务多开连接。
    /// </summary>
    [Fact]
    public void WithoutRequiresNew_ShouldReuseSharedContext()
    {
        var sharedContext = ((SqlSugarScopeProvider)_scope.GetConnectionScope(OuterConfigId)).ScopedContext;

        using var outer = _unitOfWorkManager.Begin(new XiHanUnitOfWorkOptions(isTransactional: true));
        var client = _resolver.GetClient(OuterConfigId);

        Assert.Same(sharedContext, client);
    }

    /// <summary>
    /// 没有外层工作单元时不应额外物化连接，沿用共享上下文即可。
    /// </summary>
    /// <remarks>隔离连接有真实成本，只有确实可能与外层事务冲突时才付出。</remarks>
    [Fact]
    public void RequiresNew_WithoutOuterUnitOfWork_ShouldNotRequireIsolation()
    {
        using var only = _unitOfWorkManager.Begin(new XiHanUnitOfWorkOptions(isTransactional: true), requiresNew: true);

        Assert.False(only.Options.RequiresIsolatedConnection);
    }

    /// <summary>
    /// 要求独立事务却拿到已有事务的连接时必须响亮失败，而不是退化为空提交。
    /// </summary>
    [Fact]
    public void TransactionApi_RequireOwnTransactionOnBusyConnection_ShouldThrow()
    {
        var client = _scope.GetConnectionScope(OuterConfigId);
        client.Ado.BeginTran();
        try
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => new SqlSugarTransactionApi(client, null, requireOwnTransaction: true));
            Assert.Contains("独立提交", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            client.Ado.RollbackTran();
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _serviceProvider.Dispose();
        _scope.Dispose();
        DeleteDatabase(_outerDatabasePath);
        DeleteDatabase(_innerDatabasePath);
    }

    private static ConnectionConfig BuildConfig(string configId, string path) => new()
    {
        ConfigId = configId,
        // 关闭连接池，避免用例结束后驱动仍持有临时库文件句柄。
        ConnectionString = $"DataSource={path};Pooling=False",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = true,
    };

    private static void DeleteDatabase(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 用一条全新连接统计行数，确保断言看到的是已提交状态而不是某个事务内的视图。
    /// </summary>
    private static int CountWithFreshConnection(string path)
    {
        using var probe = new SqlSugarClient(BuildConfig("Probe", path));
        return probe.Queryable<Note>().Count();
    }

    private void Insert(string configId, long id, string text)
    {
        _ = _resolver.GetClient(configId).Insertable(new Note { Id = id, Text = text }).ExecuteCommand();
    }

    /// <summary>测试用最小实体。</summary>
    [SugarTable(TableName = "Note")]
    public class Note
    {
        /// <summary>主键。</summary>
        [SugarColumn(IsPrimaryKey = true)]
        public long Id { get; set; }

        /// <summary>内容。</summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>固定返回同一 ConfigId 的连接解析器替身。</summary>
    private sealed class FixedTenantConnectionResolver(string currentConfigId, IReadOnlyCollection<string> configIds)
        : ISqlSugarTenantConnectionResolver
    {
        /// <summary>
        /// 解析当前租户连接配置标识
        /// </summary>
        /// <returns>构造时传入的固定连接配置标识</returns>
        public string ResolveCurrentConfigId() => currentConfigId;

        /// <summary>
        /// 根据租户标识解析连接配置标识
        /// </summary>
        /// <param name="tenantId">租户Id</param>
        /// <param name="tenantName">租户名称</param>
        /// <returns>构造时传入的固定连接配置标识，忽略租户参数</returns>
        public string ResolveConfigId(long? tenantId, string? tenantName = null) => currentConfigId;

        /// <summary>
        /// 获取全部连接配置标识
        /// </summary>
        /// <returns>构造时传入的连接配置标识集合</returns>
        public IReadOnlyCollection<string> GetConfigIds() => configIds;
    }

    /// <summary>无租户上下文替身。</summary>
    private sealed class NoTenant : ICurrentTenant
    {
        /// <summary>
        /// 获取当前租户是否可用，恒为 false
        /// </summary>
        public bool IsAvailable => false;

        /// <summary>
        /// 获取当前租户的唯一标识符，恒为 null
        /// </summary>
        public long? Id => null;

        /// <summary>
        /// 获取当前租户名称，恒为 null
        /// </summary>
        public string? Name => null;

        /// <summary>
        /// 临时更改当前租户信息，返回不做任何切换的空作用域
        /// </summary>
        /// <param name="id">要切换到的租户唯一标识</param>
        /// <param name="name">租户名称</param>
        /// <returns>释放时不做任何事的空作用域</returns>
        public IDisposable Change(long? id, string? name = null) => new NoopScope();

        private sealed class NoopScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    /// <summary>不改写连接配置的配置器替身。</summary>
    private sealed class PassThroughConnectionConfigurator : ISqlSugarConnectionConfigurator
    {
        /// <summary>
        /// 为指定连接作用域应用全局过滤器与 AOP，此处不做任何改写
        /// </summary>
        /// <param name="provider">连接作用域提供器</param>
        public void Configure(SqlSugarScopeProvider provider)
        {
        }

        /// <summary>
        /// 幂等确保租户连接已注册并完成配置，此处直接抛出不支持异常
        /// </summary>
        /// <param name="tenant">SqlSugar 多连接容器</param>
        /// <param name="descriptor">租户连接描述符</param>
        /// <returns>不返回，始终抛出 <see cref="NotSupportedException"/></returns>
        public SqlSugarScopeProvider EnsureTenantConnection(ITenant tenant, SqlSugarTenantConnection descriptor)
            => throw new NotSupportedException("用例不涉及库隔离租户的动态连接注册。");
    }
}
