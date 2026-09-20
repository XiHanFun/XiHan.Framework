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
/// 查询当前工作单元已登记连接标识的测试，跑在真实 SQLite 与真实工作单元上。
/// </summary>
public sealed class EnlistedConfigIdsTests : IDisposable
{
    private const string OuterConfigId = "Outer";
    private const string InnerConfigId = "Inner";

    private readonly string _outerDatabasePath = Path.Combine(Path.GetTempPath(), $"xihan-enlisted-outer-{Guid.NewGuid():N}.db");
    private readonly string _innerDatabasePath = Path.Combine(Path.GetTempPath(), $"xihan-enlisted-inner-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _serviceProvider;
    private readonly SqlSugarScope _scope;
    private readonly SqlSugarClientResolver _resolver;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    /// <summary>
    /// 建立两库、真实工作单元基础设施与客户端解析器。
    /// </summary>
    public EnlistedConfigIdsTests()
    {
        _scope = new SqlSugarScope(
        [
            BuildConfig(OuterConfigId, _outerDatabasePath),
            BuildConfig(InnerConfigId, _innerDatabasePath),
        ]);

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
            new EntityModuleDataSourceResolver(),
            new StubModuleDataSourceConnectionResolver(),
            _unitOfWorkManager,
            new NoTenant(),
            new PassThroughConnectionConfigurator(),
            []);
    }

    [Fact]
    public void 无工作单元时返回空集合()
    {
        Assert.Empty(_resolver.GetEnlistedConfigIds());
    }

    [Fact]
    public void 非事务工作单元返回空集合()
    {
        using var unitOfWork = _unitOfWorkManager.Begin(new XiHanUnitOfWorkOptions(isTransactional: false));

        _ = _resolver.GetCurrentClient();

        Assert.Empty(_resolver.GetEnlistedConfigIds());
    }

    [Fact]
    public void 事务工作单元内解析后返回该连接标识()
    {
        using var unitOfWork = _unitOfWorkManager.Begin(new XiHanUnitOfWorkOptions(isTransactional: true));

        _ = _resolver.GetCurrentClient();

        Assert.Contains(OuterConfigId, _resolver.GetEnlistedConfigIds());
    }

    [Fact]
    public void 解析多个连接后全部返回()
    {
        using var unitOfWork = _unitOfWorkManager.Begin(new XiHanUnitOfWorkOptions(isTransactional: true));

        _ = _resolver.GetClient(OuterConfigId);
        _ = _resolver.GetClient(InnerConfigId);

        var enlisted = _resolver.GetEnlistedConfigIds();

        Assert.Contains(OuterConfigId, enlisted);
        Assert.Contains(InnerConfigId, enlisted);
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

        /// <summary>
        /// 获取配置中出现过的全部模块数据源名
        /// </summary>
        /// <returns>空集合，本用例不涉及模块数据源</returns>
        public IReadOnlyCollection<string> GetModuleDataSourceNames() => [];
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

    /// <summary>
    /// 模块数据源连接解析器桩：本用例不涉及模块数据源，被调用即说明路由走错了分支。
    /// </summary>
    private sealed class StubModuleDataSourceConnectionResolver : IModuleDataSourceConnectionResolver
    {
        public ISqlSugarClient ResolveClient(string moduleDataSource, string parentConfigId) =>
            throw new InvalidOperationException("本用例不应触发模块数据源路由。");
    }
}
