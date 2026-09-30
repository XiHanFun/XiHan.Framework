// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Routing;
using XiHan.Framework.Data.SqlSugar.Tenanting;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.SqlSugar.BackgroundJobs;
using XiHan.Framework.Tasks.SqlSugar.Clients;
using XiHan.Framework.Tasks.SqlSugar.Entities;
using XiHan.Framework.Tasks.SqlSugar.Options;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Abstracts;
using XiHan.Framework.Uow.Options;

namespace XiHan.Framework.Tasks.SqlSugar.Tests;

/// <summary>
/// 后台作业入队参与环境工作单元事务的测试
/// </summary>
/// <remarks>
/// 使用真实的工作单元管理器与 <see cref="SqlSugarClientResolver"/>，落在 SQLite 文件库上，
/// 提交与回滚的结果都用全新连接读取。
/// </remarks>
public sealed class BackgroundJobEnqueueTransactionTests : IDisposable
{
    private const string ConfigId = "Default";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"xihan_tasks_tx_{Guid.NewGuid():N}.db");
    private readonly SqlSugarScope _scope;
    private readonly ServiceProvider _serviceProvider;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly SqlSugarBackgroundJobStore _store;

    /// <summary>
    /// 构造函数，建立文件库、真实工作单元基础设施与客户端解析器
    /// </summary>
    public BackgroundJobEnqueueTransactionTests()
    {
        _scope = new SqlSugarScope(BuildConfig(_databasePath));
        _scope.GetConnectionScope(ConfigId).CodeFirst.InitTables(typeof(SysBackgroundJob));

        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();
        services.AddSingleton<IAmbientUnitOfWork, AmbientUnitOfWork>();
        services.AddSingleton<IUnitOfWorkEventPublisher, NullUnitOfWorkEventPublisher>();
        services.AddTransient<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IUnitOfWorkManager, UnitOfWorkManager>();
        services.AddSingleton(_scope);
        services.AddSingleton<ISqlSugarTenantConnectionResolver>(new FixedConnectionResolver());
        services.AddSingleton<IEntityModuleDataSourceResolver, EntityModuleDataSourceResolver>();
        services.AddSingleton<IModuleDataSourceConnectionResolver, UnusedModuleConnectionResolver>();
        services.AddSingleton<ISqlSugarConnectionConfigurator, PassThroughConfigurator>();
        services.AddSingleton<ICurrentTenant>(new CurrentTenant(AsyncLocalCurrentTenantAccessor.Instance));
        services.AddScoped<ISqlSugarClientResolver, SqlSugarClientResolver>();
        _serviceProvider = services.BuildServiceProvider();
        _unitOfWorkManager = _serviceProvider.GetRequiredService<IUnitOfWorkManager>();

        var accessor = new TasksHostClientAccessor(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _serviceProvider.GetRequiredService<ICurrentTenant>());
        _store = new SqlSugarBackgroundJobStore(
            accessor,
            new FakeClock(TasksTestContext.BaseTime),
            Microsoft.Extensions.Options.Options.Create(new XiHanTasksSqlSugarOptions()));
    }

    /// <summary>
    /// 事务型工作单元内入队后回滚，查不到作业
    /// </summary>
    [Fact]
    public async Task 事务型工作单元内入队后回滚查不到作业()
    {
        using (_unitOfWorkManager.Begin(new XiHanUnitOfWorkOptions(isTransactional: true)))
        {
            await _store.InsertAsync(NewJob());

            // 不 Complete，随 Dispose 回滚
        }

        Assert.Equal(0, CountWithFreshConnection());
    }

    /// <summary>
    /// 事务型工作单元提交后查得到作业
    /// </summary>
    [Fact]
    public async Task 事务型工作单元提交后查得到作业()
    {
        var job = NewJob();

        using (var unitOfWork = _unitOfWorkManager.Begin(new XiHanUnitOfWorkOptions(isTransactional: true)))
        {
            await _store.InsertAsync(job);
            await unitOfWork.CompleteAsync();
        }

        using var probe = new SqlSugarClient(BuildConfig(_databasePath));
        var stored = await probe.Queryable<SysBackgroundJob>().SingleAsync(item => item.BasicId == job.Id);
        Assert.Equal(job.JobName, stored.JobName);
    }

    /// <summary>
    /// 释放资源并删除临时库文件
    /// </summary>
    public void Dispose()
    {
        _serviceProvider.Dispose();
        _scope.Dispose();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private static ConnectionConfig BuildConfig(string path)
    {
        return new ConnectionConfig
        {
            ConfigId = ConfigId,
            // 关闭连接池，用例结束后驱动不再持有临时库文件句柄
            ConnectionString = $"DataSource={path};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        };
    }

    private static BackgroundJobInfo NewJob()
    {
        return new BackgroundJobInfo
        {
            Id = Guid.NewGuid(),
            JobName = "Order.Close",
            JobArgs = "{\"orderId\":1}",
            CreationTime = TasksTestContext.BaseTime.AddMinutes(-30),
            NextTryTime = TasksTestContext.BaseTime.AddMinutes(-1)
        };
    }

    /// <summary>
    /// 用全新连接统计作业数，确保读到的是已提交状态
    /// </summary>
    private int CountWithFreshConnection()
    {
        using var probe = new SqlSugarClient(BuildConfig(_databasePath));
        return probe.Queryable<SysBackgroundJob>().Count();
    }

    /// <summary>
    /// 固定返回同一连接配置标识的租户连接解析器
    /// </summary>
    private sealed class FixedConnectionResolver : ISqlSugarTenantConnectionResolver
    {
        /// <summary>
        /// 解析当前租户连接配置标识
        /// </summary>
        /// <returns>固定的连接配置标识</returns>
        public string ResolveCurrentConfigId() => ConfigId;

        /// <summary>
        /// 根据租户标识解析连接配置标识
        /// </summary>
        /// <param name="tenantId">租户标识</param>
        /// <param name="tenantName">租户名称</param>
        /// <returns>固定的连接配置标识</returns>
        public string ResolveConfigId(long? tenantId, string? tenantName = null) => ConfigId;

        /// <summary>
        /// 获取全部连接配置标识
        /// </summary>
        /// <returns>连接配置标识集合</returns>
        public IReadOnlyCollection<string> GetConfigIds() => [ConfigId];

        /// <summary>
        /// 获取配置中出现过的全部模块数据源名
        /// </summary>
        /// <returns>空集合</returns>
        public IReadOnlyCollection<string> GetModuleDataSourceNames() => [];
    }

    /// <summary>
    /// 模块数据源连接解析器桩，被调用即说明路由走错了分支
    /// </summary>
    private sealed class UnusedModuleConnectionResolver : IModuleDataSourceConnectionResolver
    {
        /// <summary>
        /// 解析模块数据源客户端
        /// </summary>
        /// <param name="moduleDataSource">模块数据源名</param>
        /// <param name="parentConfigId">父连接配置标识</param>
        /// <returns>不返回，始终抛出</returns>
        public ISqlSugarClient ResolveClient(string moduleDataSource, string parentConfigId) =>
            throw new InvalidOperationException("本用例不应触发模块数据源路由。");
    }

    /// <summary>
    /// 不改写连接配置的配置器
    /// </summary>
    private sealed class PassThroughConfigurator : ISqlSugarConnectionConfigurator
    {
        /// <summary>
        /// 为指定连接作用域应用全局过滤器与 AOP，此处不做任何改写
        /// </summary>
        /// <param name="provider">连接作用域提供器</param>
        public void Configure(SqlSugarScopeProvider provider)
        {
        }

        /// <summary>
        /// 幂等确保租户连接已注册并完成配置，此处不支持
        /// </summary>
        /// <param name="tenant">SqlSugar 多连接容器</param>
        /// <param name="descriptor">租户连接描述符</param>
        /// <returns>不返回，始终抛出</returns>
        public SqlSugarScopeProvider EnsureTenantConnection(ITenant tenant, SqlSugarTenantConnection descriptor) =>
            throw new NotSupportedException("本用例不涉及库隔离租户的动态连接注册。");
    }
}
