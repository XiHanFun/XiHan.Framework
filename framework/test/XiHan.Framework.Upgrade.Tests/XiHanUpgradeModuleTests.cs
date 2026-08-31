// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Upgrade.Abstractions;
using XiHan.Framework.Upgrade.Enums;
using XiHan.Framework.Upgrade.Models;
using XiHan.Framework.Upgrade.Options;
using XiHan.Framework.Upgrade.Services;
using XiHan.Framework.Upgrade.Tests.Fakes;

namespace XiHan.Framework.Upgrade.Tests;

/// <summary>
/// 曦寒升级模块测试
/// </summary>
/// <remarks>
/// 模块层的契约有三条：依赖多租户抽象模块、服务配置阶段完成全部注册、
/// 应用初始化后按开关决定是否做一次升级自检（且缺依赖时安静跳过而不是启动失败）。
/// </remarks>
public class XiHanUpgradeModuleTests
{
    /// <summary>
    /// 模块继承自框架模块基类
    /// </summary>
    [Fact]
    public void Module_DerivesFromXiHanModule()
    {
        Assert.True(typeof(XiHanUpgradeModule).IsSubclassOf(typeof(XiHanModule)));
    }

    /// <summary>
    /// 模块声明依赖多租户抽象模块
    /// </summary>
    [Fact]
    public void Module_DependsOnMultiTenancyAbstractionsModule()
    {
        var dependedTypes = typeof(XiHanUpgradeModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .SelectMany(attribute => attribute.GetDependedTypes())
            .ToArray();

        Assert.Contains(typeof(XiHanMultiTenancyAbstractionsModule), dependedTypes);
    }

    /// <summary>
    /// 服务配置阶段完成注册并绑定配置节
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersUpgradeServicesAndBindsOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:Upgrade:LockResourceKey"] = "ModuleUpgrade"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IApplicationInfoAccessor>(new FakeApplicationInfoAccessor());

        new XiHanUpgradeModule().ConfigureServices(new ServiceConfigurationContext(services));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Equal("ModuleUpgrade", scope.ServiceProvider.GetRequiredService<IOptions<XiHanUpgradeOptions>>().Value.LockResourceKey);
        Assert.IsType<UpgradeStatusService>(scope.ServiceProvider.GetRequiredService<IUpgradeStatusService>());
        Assert.IsType<UpgradeCoordinator>(scope.ServiceProvider.GetRequiredService<IUpgradeCoordinator>());
    }

    /// <summary>
    /// 关闭启动自检时初始化阶段不调用状态服务
    /// </summary>
    [Fact]
    public async Task OnPostApplicationInitializationAsync_WhenAutoCheckDisabled_SkipsInitialization()
    {
        var statusService = new RecordingStatusService();
        using var provider = BuildProvider(statusService, autoCheck: false, registerVersionStore: true);

        await new XiHanUpgradeModule().OnPostApplicationInitializationAsync(new ApplicationInitializationContext(provider));

        Assert.Equal(0, statusService.EnsureInitializedCount);
    }

    /// <summary>
    /// 开启启动自检时初始化阶段做一次升级自检
    /// </summary>
    [Fact]
    public async Task OnPostApplicationInitializationAsync_WhenAutoCheckEnabled_EnsuresInitialized()
    {
        var statusService = new RecordingStatusService();
        using var provider = BuildProvider(statusService, autoCheck: true, registerVersionStore: true);

        await new XiHanUpgradeModule().OnPostApplicationInitializationAsync(new ApplicationInitializationContext(provider));

        Assert.Equal(1, statusService.EnsureInitializedCount);
    }

    /// <summary>
    /// 未注册版本存储时安静跳过，不让应用启动失败
    /// </summary>
    [Fact]
    public async Task OnPostApplicationInitializationAsync_WhenVersionStoreMissing_SkipsInitialization()
    {
        var statusService = new RecordingStatusService();
        using var provider = BuildProvider(statusService, autoCheck: true, registerVersionStore: false);

        await new XiHanUpgradeModule().OnPostApplicationInitializationAsync(new ApplicationInitializationContext(provider));

        Assert.Equal(0, statusService.EnsureInitializedCount);
    }

    /// <summary>
    /// 构建初始化阶段所需的最小服务提供器
    /// </summary>
    /// <param name="statusService">状态服务替身</param>
    /// <param name="autoCheck">是否开启启动自检</param>
    /// <param name="registerVersionStore">是否注册版本存储</param>
    /// <returns>服务提供器</returns>
    private static ServiceProvider BuildProvider(
        IUpgradeStatusService statusService,
        bool autoCheck,
        bool registerVersionStore,
        IUpgradeEngine? engine = null)
    {
        var services = new ServiceCollection();
        services.Configure<XiHanUpgradeOptions>(upgradeOptions => upgradeOptions.EnableAutoCheckOnStartup = autoCheck);
        services.AddSingleton(statusService);

        if (registerVersionStore)
        {
            services.AddSingleton<IUpgradeVersionStore>(new StubVersionStore());
        }

        if (engine is not null)
        {
            services.AddSingleton(engine);
        }

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 开启启动自检时必须真的执行升级，而不是只建一条版本记录
    /// </summary>
    /// <remarks>
    /// 回归锚点：修复前这里只调 <c>EnsureInitializedAsync</c>，而它只做 <c>GetOrCreateAsync</c>；
    /// <c>IUpgradeEngine</c> 全仓无任何调用方，于是迁移脚本写下去就从未执行过——
    /// 表现为部署后新增字段一律 42703，且没有任何报错提示。
    /// </remarks>
    [Fact]
    public async Task OnPostApplicationInitializationAsync_WhenAutoCheckEnabled_ExecutesUpgrade()
    {
        var engine = new RecordingUpgradeEngine(UpgradeStatus.Completed);
        using var provider = BuildProvider(new RecordingStatusService(), autoCheck: true, registerVersionStore: true, engine);

        await new XiHanUpgradeModule().OnPostApplicationInitializationAsync(new ApplicationInitializationContext(provider));

        Assert.Equal(1, engine.ExecuteCount);
    }

    /// <summary>
    /// 关闭启动自检时不执行升级
    /// </summary>
    [Fact]
    public async Task OnPostApplicationInitializationAsync_WhenAutoCheckDisabled_DoesNotExecuteUpgrade()
    {
        var engine = new RecordingUpgradeEngine(UpgradeStatus.Completed);
        using var provider = BuildProvider(new RecordingStatusService(), autoCheck: false, registerVersionStore: true, engine);

        await new XiHanUpgradeModule().OnPostApplicationInitializationAsync(new ApplicationInitializationContext(provider));

        Assert.Equal(0, engine.ExecuteCount);
    }

    /// <summary>
    /// 升级失败必须中断启动，不能带着半迁移的结构对外服务
    /// </summary>
    [Fact]
    public async Task OnPostApplicationInitializationAsync_WhenUpgradeFails_Throws()
    {
        var engine = new RecordingUpgradeEngine(UpgradeStatus.Failed);
        using var provider = BuildProvider(new RecordingStatusService(), autoCheck: true, registerVersionStore: true, engine);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new XiHanUpgradeModule().OnPostApplicationInitializationAsync(new ApplicationInitializationContext(provider)));

        Assert.Contains("中断启动", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 未注册引擎时安静跳过，不让应用启动失败
    /// </summary>
    [Fact]
    public async Task OnPostApplicationInitializationAsync_WhenEngineMissing_SkipsUpgrade()
    {
        var statusService = new RecordingStatusService();
        using var provider = BuildProvider(statusService, autoCheck: true, registerVersionStore: true);

        await new XiHanUpgradeModule().OnPostApplicationInitializationAsync(new ApplicationInitializationContext(provider));

        Assert.Equal(1, statusService.EnsureInitializedCount);
    }

    /// <summary>
    /// 记录执行次数的升级引擎替身
    /// </summary>
    private sealed class RecordingUpgradeEngine : IUpgradeEngine
    {
        private readonly UpgradeStatus _status;

        public RecordingUpgradeEngine(UpgradeStatus status)
        {
            _status = status;
        }

        public int ExecuteCount { get; private set; }

        public Task<UpgradeStartResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            return Task.FromResult(new UpgradeStartResult
            {
                Started = _status != UpgradeStatus.Failed,
                Status = _status,
                Message = _status == UpgradeStatus.Failed ? "脚本执行失败" : "升级完成"
            });
        }
    }

    /// <summary>
    /// 记录初始化调用次数的状态服务替身
    /// </summary>
    private sealed class RecordingStatusService : IUpgradeStatusService
    {
        public int EnsureInitializedCount { get; private set; }

        public Task EnsureInitializedAsync()
        {
            EnsureInitializedCount++;
            return Task.CompletedTask;
        }

        public Task<UpgradeVersionSnapshot> GetVersionSnapshotAsync(string? clientVersion = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UpgradeVersionSnapshot());
        }

        public Task<UpgradeStatus> GetUpgradeStatusAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(UpgradeStatus.Normal);
        }
    }

    /// <summary>
    /// 空转的版本存储替身
    /// </summary>
    private sealed class StubVersionStore : IUpgradeVersionStore
    {
        public Task EnsureTablesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<UpgradeVersionState> GetOrCreateAsync(string currentAppVersion, string minSupportVersion, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UpgradeVersionState());
        }

        public Task<UpgradeMigrationHistory?> GetLatestHistoryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<UpgradeMigrationHistory?>(null);
        }

        public Task SetUpgradingAsync(UpgradeVersionState version, string nodeName, DateTimeOffset startTime, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SetUpgradeCompletedAsync(UpgradeVersionState version, string appVersion, string dbVersion, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SetUpgradeFailedAsync(UpgradeVersionState version, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateDbVersionAsync(UpgradeVersionState version, string dbVersion, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task AddMigrationHistoryAsync(UpgradeMigrationHistory history, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<bool> HasMigrationHistoryAsync(string version, string scriptName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }
}
