// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Clients;
using XiHan.Framework.Data.SqlSugar.Initializers;
using XiHan.Framework.Data.SqlSugar.Options;
using XiHan.Framework.Data.SqlSugar.Routing;
using XiHan.Framework.Data.SqlSugar.Seeders;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Data.Tests;

/// <summary>
/// 数据库初始化器的遍历口径测试。
/// </summary>
/// <remarks>
/// 锁死一条接线契约：初始化器遍历库时必须走 <see cref="ISqlSugarClientResolver.GetAllConfigIds"/>，
/// 而不是自己去数 <c>ConnectionConfigs</c> 的顶层条目。模块库挂在父连接下面、ConfigId 由框架派生，
/// 只枚举顶层配置会把它们整批漏掉，且既不建库也不建表、全程无异常——是静默失败。
/// </remarks>
public sealed class DbInitializerConnectionEnumerationTests
{
    [Fact]
    public async Task 初始化遍历的是全量连接标识而不是顶层配置()
    {
        // 两份名单第一位故意不同：走全量名单先碰 Default_Erp，走顶层配置先碰 Default。
        // 任一库初始化失败会中断后续库（既有的 fail-fast 设计），所以只看第一个被请求的即可判别遍历源。
        var resolver = new RecordingClientResolver(["Default_Erp", "Default"]);
        var initializer = CreateInitializer(resolver, topLevelConfigIds: ["Default"]);

        await Assert.ThrowsAsync<StubClientException>(initializer.InitializeAsync);

        Assert.Equal("Default_Erp", resolver.RequestedConfigIds.FirstOrDefault());
    }

    [Fact]
    public async Task 顶层配置里没有的模块库照样进初始化()
    {
        // 模块库的 ConfigId 只存在于全量名单，顶层配置里压根没有它
        var resolver = new RecordingClientResolver(["Default_Erp"]);
        var initializer = CreateInitializer(resolver, topLevelConfigIds: ["Default"]);

        await Assert.ThrowsAsync<StubClientException>(initializer.InitializeAsync);

        Assert.Equal(["Default_Erp"], resolver.RequestedConfigIds);
    }

    [Fact]
    public async Task 全量连接标识为空时回退默认连接()
    {
        var resolver = new RecordingClientResolver([]);
        var initializer = CreateInitializer(resolver, topLevelConfigIds: []);

        await Assert.ThrowsAsync<StubClientException>(initializer.InitializeAsync);

        Assert.Equal(["Default"], resolver.RequestedConfigIds);
    }

    [Fact]
    public async Task 初始化开关关闭时一个库都不碰()
    {
        var resolver = new RecordingClientResolver(["Default", "Default_Erp"]);
        var initializer = CreateInitializer(resolver, topLevelConfigIds: ["Default"], enableDbInitialization: false);

        await initializer.InitializeAsync();

        Assert.Empty(resolver.RequestedConfigIds);
    }

    [Fact]
    public async Task 单租户初始化走的是当前布局而不是全量库()
    {
        // 开通一个库隔离租户时只该建这个租户的库，不该把平台的全量库重跑一遍
        var resolver = new RecordingClientResolver(
            allConfigIds: ["Default", "Default_Erp"],
            layoutConfigIds: ["Tenant_1001", "Tenant_1001_Erp"]);
        var initializer = CreateInitializer(resolver, topLevelConfigIds: ["Default"]);

        await Assert.ThrowsAsync<StubClientException>(initializer.InitializeCurrentLayoutAsync);

        Assert.Equal("Tenant_1001", resolver.RequestedConfigIds.FirstOrDefault());
    }

    [Fact]
    public async Task 单租户初始化开关关闭时一个库都不碰()
    {
        var resolver = new RecordingClientResolver(
            allConfigIds: ["Default"],
            layoutConfigIds: ["Tenant_1001", "Tenant_1001_Erp"]);
        var initializer = CreateInitializer(resolver, topLevelConfigIds: ["Default"], enableDbInitialization: false);

        await initializer.InitializeCurrentLayoutAsync();

        Assert.Empty(resolver.RequestedConfigIds);
    }

    /// <summary>
    /// 构造初始化器：客户端解析器用桩，其余依赖用真实实现
    /// </summary>
    /// <param name="resolver">记录被请求连接标识的桩</param>
    /// <param name="topLevelConfigIds">原始配置里的顶层连接标识</param>
    /// <param name="enableDbInitialization">是否开启数据库初始化</param>
    /// <returns>数据库初始化器</returns>
    private static DbInitializer CreateInitializer(
        RecordingClientResolver resolver,
        string[] topLevelConfigIds,
        bool enableDbInitialization = true)
    {
        var options = new XiHanSqlSugarCoreOptions
        {
            DefaultConfigId = "Default",
            EnableDbInitialization = enableDbInitialization,
            ConnectionConfigs = [.. topLevelConfigIds.Select(configId => new SqlSugarConnectionConfigOptions { ConfigId = configId })]
        };

        var wrappedOptions = Options.Create(options);

        return new DbInitializer(
            resolver,
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<DbInitializer>.Instance,
            wrappedOptions,
            new NoTenant(),
            new DbEntityTypeProvider(wrappedOptions, new EntityModuleDataSourceResolver()),
            new DataSeederSelector(wrappedOptions));
    }

    /// <summary>
    /// 桩客户端解析器：记录被请求的连接标识，取客户端时立即抛出以中断真实建库
    /// </summary>
    private sealed class RecordingClientResolver : ISqlSugarClientResolver
    {
        private readonly string[] _allConfigIds;
        private readonly string[] _layoutConfigIds;
        private readonly List<string> _requested = [];

        public RecordingClientResolver(string[] allConfigIds, string[]? layoutConfigIds = null)
        {
            _allConfigIds = allConfigIds;
            _layoutConfigIds = layoutConfigIds ?? allConfigIds;
        }

        /// <summary>
        /// 依次被请求过的连接标识
        /// </summary>
        public IReadOnlyList<string> RequestedConfigIds => _requested;

        public IReadOnlyCollection<string> GetAllConfigIds() => _allConfigIds;

        public IReadOnlyList<string> GetCurrentLayoutConfigIds() => _layoutConfigIds;

        public ISqlSugarClient GetClient(string configId)
        {
            _requested.Add(configId);
            throw new StubClientException(configId);
        }

        public ISqlSugarClient GetCurrentClient() => throw new StubClientException("current");

        public ISqlSugarClient GetClientForEntity(Type entityType) => throw new StubClientException(entityType.Name);

        public IEnumerable<ISqlSugarClient> GetAllClients() => _allConfigIds.Select(GetClient);

        public ITenant AsTenant() => throw new NotSupportedException("用例不涉及多连接容器。");
    }

    /// <summary>
    /// 桩解析器取客户端时抛出的异常，携带被请求的连接标识
    /// </summary>
    private sealed class StubClientException : Exception
    {
        public StubClientException(string configId)
            : base($"STUB:{configId}")
        {
        }
    }

    /// <summary>
    /// 无租户上下文的当前租户实现
    /// </summary>
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
