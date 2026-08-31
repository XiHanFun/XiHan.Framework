// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Routing;
using XiHan.Framework.Data.SqlSugar.Tenanting;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Abstracts;

namespace XiHan.Framework.Data.Tests;

/// <summary>
/// 「当前布局有哪些库」的解析测试，跑在真实 SqlSugarScope 上。
/// </summary>
/// <remarks>
/// 布局 = 一个主库加上挂在它下面、已经建连的模块库。库隔离租户开通时只初始化这一套，
/// 不能把平台的全量库重跑一遍，也不能漏掉该租户自带的模块库。
/// </remarks>
public sealed class CurrentLayoutConfigIdsTests : IDisposable
{
    private readonly List<string> _databasePaths = [];
    private readonly SqlSugarScope _scope;

    /// <summary>
    /// 建立「主库 + 一个模块库 + 一条无关连接」三条真实 SQLite 连接
    /// </summary>
    public CurrentLayoutConfigIdsTests()
    {
        _scope = new SqlSugarScope(
        [
            BuildConfig("Default"),
            BuildConfig("Default_Erp"),
            BuildConfig("Other"),
        ]);
    }

    [Fact]
    public void 当前布局含主库与已建连的模块库()
    {
        var resolver = CreateResolver("Default", moduleDataSourceNames: ["Erp"]);

        Assert.Equal(["Default", "Default_Erp"], resolver.GetCurrentLayoutConfigIds());
    }

    [Fact]
    public void 未建连的模块库不进当前布局()
    {
        // Mes 在配置里声明过，但这套布局下没有它对应的连接
        var resolver = CreateResolver("Default", moduleDataSourceNames: ["Erp", "Mes"]);

        Assert.Equal(["Default", "Default_Erp"], resolver.GetCurrentLayoutConfigIds());
    }

    [Fact]
    public void 别的布局的模块库不进当前布局()
    {
        // 当前主库是 Other，Default_Erp 属于另一套布局，不能被算进来
        var resolver = CreateResolver("Other", moduleDataSourceNames: ["Erp"]);

        Assert.Equal(["Other"], resolver.GetCurrentLayoutConfigIds());
    }

    [Fact]
    public void 没有模块库时当前布局只有主库()
    {
        var resolver = CreateResolver("Default", moduleDataSourceNames: []);

        Assert.Equal(["Default"], resolver.GetCurrentLayoutConfigIds());
    }

    /// <summary>
    /// 释放连接与临时库文件
    /// </summary>
    public void Dispose()
    {
        _scope.Dispose();
        foreach (var path in _databasePaths.Where(File.Exists))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 构造客户端解析器：无租户上下文、无租户连接提供器，走静态 ConfigId 解析
    /// </summary>
    /// <param name="currentConfigId">当前主库连接标识</param>
    /// <param name="moduleDataSourceNames">配置中出现过的模块数据源名</param>
    /// <returns>客户端解析器</returns>
    private SqlSugarClientResolver CreateResolver(string currentConfigId, string[] moduleDataSourceNames)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();
        services.AddSingleton<IAmbientUnitOfWork, AmbientUnitOfWork>();
        services.AddSingleton<IUnitOfWorkEventPublisher, NullUnitOfWorkEventPublisher>();
        services.AddTransient<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IUnitOfWorkManager, UnitOfWorkManager>();
        var serviceProvider = services.BuildServiceProvider();

        return new SqlSugarClientResolver(
            _scope,
            new FixedResolver(currentConfigId, moduleDataSourceNames),
            new EntityModuleDataSourceResolver(),
            new ThrowingModuleConnectionResolver(),
            serviceProvider.GetRequiredService<IUnitOfWorkManager>(),
            new NoTenant(),
            new NoopConnectionConfigurator(),
            []);
    }

    private ConnectionConfig BuildConfig(string configId)
    {
        var path = Path.Combine(Path.GetTempPath(), $"xihan-layout-{configId}-{Guid.NewGuid():N}.db");
        _databasePaths.Add(path);

        return new ConnectionConfig
        {
            ConfigId = configId,
            // 关闭连接池，避免用例结束后驱动仍持有临时库文件句柄
            ConnectionString = $"DataSource={path};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
        };
    }

    /// <summary>固定当前连接与模块名清单的租户连接解析器替身。</summary>
    private sealed class FixedResolver(string currentConfigId, string[] moduleDataSourceNames)
        : ISqlSugarTenantConnectionResolver
    {
        public string ResolveCurrentConfigId() => currentConfigId;

        public string ResolveConfigId(long? tenantId, string? tenantName = null) => currentConfigId;

        public IReadOnlyCollection<string> GetConfigIds() => [currentConfigId];

        public IReadOnlyCollection<string> GetModuleDataSourceNames() => moduleDataSourceNames;
    }

    /// <summary>模块连接解析器替身：本用例不按实体路由，被调用即说明走错了分支。</summary>
    private sealed class ThrowingModuleConnectionResolver : IModuleDataSourceConnectionResolver
    {
        public ISqlSugarClient ResolveClient(string moduleDataSource, string parentConfigId) =>
            throw new InvalidOperationException("本用例不应触发模块数据源路由。");
    }

    /// <summary>连接配置器替身：本用例的连接都在构造时给定，不涉及运行时建连。</summary>
    private sealed class NoopConnectionConfigurator : ISqlSugarConnectionConfigurator
    {
        public void Configure(SqlSugarScopeProvider provider)
        {
        }

        public SqlSugarScopeProvider EnsureTenantConnection(ITenant tenant, SqlSugarTenantConnection descriptor) =>
            throw new NotSupportedException("用例不涉及库隔离租户的动态连接注册。");
    }

    /// <summary>无租户上下文替身。</summary>
    private sealed class NoTenant : ICurrentTenant
    {
        public bool IsAvailable => false;

        public long? Id => null;

        public string? Name => null;

        public IDisposable Change(long? id, string? name = null) => new NoopScope();

        private sealed class NoopScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
