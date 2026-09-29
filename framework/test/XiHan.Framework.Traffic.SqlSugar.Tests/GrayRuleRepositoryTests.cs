// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Traffic.Extensions.DependencyInjection;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Enums;
using XiHan.Framework.Traffic.GrayRouting.Implementations;
using XiHan.Framework.Traffic.GrayRouting.Models;
using XiHan.Framework.Traffic.SqlSugar.Entities;
using XiHan.Framework.Traffic.SqlSugar.Extensions.DependencyInjection;
using XiHan.Framework.Traffic.SqlSugar.Options;
using XiHan.Framework.Traffic.SqlSugar.Repositories;
using XiHan.Framework.Uow;
using XiHan.Framework.Uow.Abstracts;

namespace XiHan.Framework.Traffic.SqlSugar.Tests;

/// <summary>
/// 灰度规则仓储测试
/// </summary>
public class GrayRuleRepositoryTests
{
    /// <summary>
    /// 返回的启用规则运行时类型是 GrayRule 且保留目标版本
    /// </summary>
    [Fact]
    public async Task 返回的启用规则运行时类型是GrayRule且保留目标版本()
    {
        using var context = new GrayRuleTestContext();
        context.Client.Insertable(new SysGrayRule("rule-1")
        {
            RuleName = "百分比灰度",
            RuleType = (int)GrayRuleType.Percentage,
            IsEnabled = true,
            Priority = 1,
            TargetVersion = "2.0.0",
            CreatedTime = DateTimeOffset.UtcNow
        }).ExecuteCommand();

        await context.Repository.RefreshAsync();
        var rules = await context.Repository.GetEnabledRulesAsync();

        var rule = Assert.Single(rules);
        Assert.IsType<GrayRule>(rule);
        Assert.Equal("2.0.0", ((GrayRule)rule).TargetVersion);
    }

    /// <summary>
    /// 只返回启用的规则
    /// </summary>
    [Fact]
    public async Task 只返回启用的规则()
    {
        using var context = new GrayRuleTestContext();
        context.Client.Insertable(new SysGrayRule("rule-enabled") { RuleName = "启用", IsEnabled = true, CreatedTime = DateTimeOffset.UtcNow }).ExecuteCommand();
        context.Client.Insertable(new SysGrayRule("rule-disabled") { RuleName = "禁用", IsEnabled = false, CreatedTime = DateTimeOffset.UtcNow }).ExecuteCommand();

        await context.Repository.RefreshAsync();
        var rules = await context.Repository.GetEnabledRulesAsync();

        Assert.Single(rules);
        Assert.Equal("rule-enabled", rules[0].RuleId);
    }

    /// <summary>
    /// 按标识能查到禁用的规则
    /// </summary>
    [Fact]
    public async Task 按标识能查到禁用的规则()
    {
        using var context = new GrayRuleTestContext();
        context.Client.Insertable(new SysGrayRule("rule-disabled") { RuleName = "禁用", IsEnabled = false, CreatedTime = DateTimeOffset.UtcNow }).ExecuteCommand();

        await context.Repository.RefreshAsync();
        var rule = await context.Repository.GetRuleByIdAsync("rule-disabled");

        Assert.NotNull(rule);
    }

    /// <summary>
    /// 查不存在的标识返回 null
    /// </summary>
    [Fact]
    public async Task 查不存在的标识返回null()
    {
        using var context = new GrayRuleTestContext();

        var rule = await context.Repository.GetRuleByIdAsync("not-exist");

        Assert.Null(rule);
    }

    /// <summary>
    /// 显式刷新后能立即读到新写入的规则
    /// </summary>
    [Fact]
    public async Task 显式刷新后能立即读到新写入的规则()
    {
        using var context = new GrayRuleTestContext(refreshInterval: TimeSpan.FromMinutes(10));
        await context.Repository.RefreshAsync();

        context.Client.Insertable(new SysGrayRule("rule-new") { RuleName = "新规则", IsEnabled = true, CreatedTime = DateTimeOffset.UtcNow }).ExecuteCommand();
        await context.Repository.RefreshAsync();

        var rules = await context.Repository.GetEnabledRulesAsync();

        Assert.Contains(rules, item => item.RuleId == "rule-new");
    }

    /// <summary>
    /// 未到期时不重新查库，读到的是旧缓存
    /// </summary>
    [Fact]
    public async Task 未到期时不重新查库读到的是旧缓存()
    {
        using var context = new GrayRuleTestContext(refreshInterval: TimeSpan.FromMinutes(10));
        await context.Repository.RefreshAsync();

        context.Client.Insertable(new SysGrayRule("rule-new") { RuleName = "新规则", IsEnabled = true, CreatedTime = DateTimeOffset.UtcNow }).ExecuteCommand();
        var rules = await context.Repository.GetEnabledRulesAsync();

        Assert.DoesNotContain(rules, item => item.RuleId == "rule-new");
    }

    /// <summary>
    /// 到期后自动重新查库
    /// </summary>
    [Fact]
    public async Task 到期后自动重新查库()
    {
        using var context = new GrayRuleTestContext(refreshInterval: TimeSpan.FromMilliseconds(1));
        await context.Repository.RefreshAsync();

        context.Client.Insertable(new SysGrayRule("rule-new") { RuleName = "新规则", IsEnabled = true, CreatedTime = DateTimeOffset.UtcNow }).ExecuteCommand();
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        var rules = await context.Repository.GetEnabledRulesAsync();

        Assert.Contains(rules, item => item.RuleId == "rule-new");
    }

    /// <summary>
    /// 在租户上下文中刷新时以宿主上下文取客户端，调用后恢复原租户
    /// </summary>
    [Fact]
    public async Task 在租户上下文中刷新时以宿主上下文取客户端()
    {
        using var context = new GrayRuleTestContext();

        using (context.Tenant.Change(42))
        {
            await context.Repository.RefreshAsync();

            Assert.Equal(42L, context.Tenant.Id);
        }

        Assert.NotEmpty(context.Resolver.ObservedTenantIds);
        Assert.All(context.Resolver.ObservedTenantIds, tenantId => Assert.Null(tenantId));
    }

    /// <summary>
    /// 注册扩展以 SqlSugar 仓储顶替内存实现
    /// </summary>
    [Fact]
    public void 注册扩展顶替内存实现()
    {
        var services = new ServiceCollection();
        services.AddGrayRouting();

        services.AddXiHanTrafficSqlSugar();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IGrayRuleRepository));
        Assert.Equal(typeof(SqlSugarGrayRuleRepository), descriptor.ImplementationType);
    }
}

/// <summary>
/// 灰度规则仓储测试夹具
/// </summary>
internal sealed class GrayRuleTestContext : IDisposable
{
    private readonly string _databaseFile;
    private readonly ServiceProvider _serviceProvider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="refreshInterval">缓存刷新间隔，默认 30 秒</param>
    /// <param name="timeProvider">时间提供器，默认系统时间</param>
    public GrayRuleTestContext(TimeSpan? refreshInterval = null, TimeProvider? timeProvider = null)
    {
        _databaseFile = Path.Combine(Path.GetTempPath(), $"xihan_gray_rule_{Guid.NewGuid():N}.db");

        Client = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"DataSource={_databaseFile};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true
        });

        Client.CodeFirst.InitTables(typeof(SysGrayRule));

        Tenant = new CurrentTenant(AsyncLocalCurrentTenantAccessor.Instance);
        Resolver = new StubClientResolver(Client, Tenant);

        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();
        services.AddSingleton<ISqlSugarClientResolver>(Resolver);
        services.AddSingleton<ICurrentTenant>(Tenant);
        services.AddSingleton<IAmbientUnitOfWork, AmbientUnitOfWork>();
        services.AddSingleton<IUnitOfWorkEventPublisher, NullUnitOfWorkEventPublisher>();
        services.AddTransient<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IUnitOfWorkManager, UnitOfWorkManager>();
        _serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        Repository = new SqlSugarGrayRuleRepository(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new XiHanTrafficSqlSugarOptions { RefreshInterval = refreshInterval ?? TimeSpan.FromSeconds(30) }),
            NullLogger<SqlSugarGrayRuleRepository>.Instance,
            timeProvider ?? TimeProvider.System);
    }

    /// <summary>
    /// SQLite 客户端
    /// </summary>
    public SqlSugarClient Client { get; }

    /// <summary>
    /// 被测仓储
    /// </summary>
    public SqlSugarGrayRuleRepository Repository { get; }

    /// <summary>
    /// 当前租户
    /// </summary>
    public ICurrentTenant Tenant { get; }

    /// <summary>
    /// 记录解析时租户的客户端解析器
    /// </summary>
    public StubClientResolver Resolver { get; }

    /// <summary>
    /// 释放服务容器、客户端并删除临时库文件
    /// </summary>
    public void Dispose()
    {
        _serviceProvider.Dispose();
        Client.Dispose();

        if (File.Exists(_databaseFile))
        {
            File.Delete(_databaseFile);
        }
    }
}

/// <summary>
/// 测试用客户端解析器，固定返回同一个客户端
/// </summary>
internal sealed class StubClientResolver : ISqlSugarClientResolver
{
    private readonly ISqlSugarClient _client;
    private readonly ICurrentTenant _currentTenant;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="client">固定返回的客户端</param>
    /// <param name="currentTenant">当前租户</param>
    public StubClientResolver(ISqlSugarClient client, ICurrentTenant currentTenant)
    {
        _client = client;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// 每次解析实体客户端时的租户标识
    /// </summary>
    public List<long?> ObservedTenantIds { get; } = [];

    /// <summary>
    /// 获取当前客户端
    /// </summary>
    public ISqlSugarClient GetCurrentClient()
    {
        return _client;
    }

    /// <summary>
    /// 获取实体对应的客户端
    /// </summary>
    /// <param name="entityType">实体类型</param>
    public ISqlSugarClient GetClientForEntity(Type entityType)
    {
        ObservedTenantIds.Add(_currentTenant.Id);
        return _client;
    }

    /// <summary>
    /// 按连接配置标识获取客户端
    /// </summary>
    /// <param name="configId">连接配置标识</param>
    public ISqlSugarClient GetClient(string configId)
    {
        return _client;
    }

    /// <summary>
    /// 获取全部连接配置标识
    /// </summary>
    public IReadOnlyCollection<string> GetAllConfigIds()
    {
        return ["Default"];
    }

    /// <summary>
    /// 获取当前布局的全部连接配置标识
    /// </summary>
    public IReadOnlyList<string> GetCurrentLayoutConfigIds()
    {
        return ["Default"];
    }

    /// <summary>
    /// 获取当前工作单元已登记的连接配置标识
    /// </summary>
    public IReadOnlyList<string> GetEnlistedConfigIds()
    {
        return [];
    }

    /// <summary>
    /// 获取所有库的客户端
    /// </summary>
    public IEnumerable<ISqlSugarClient> GetAllClients()
    {
        return [_client];
    }

    /// <summary>
    /// 获取底层多租户接口
    /// </summary>
    /// <exception cref="NotSupportedException">始终抛出</exception>
    public ITenant AsTenant()
    {
        throw new NotSupportedException("测试桩不支持多租户切换。");
    }
}
