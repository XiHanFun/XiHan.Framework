// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Upgrade.Abstractions;
using XiHan.Framework.Upgrade.Enums;
using XiHan.Framework.Upgrade.Models;
using XiHan.Framework.Upgrade.Options;
using XiHan.Framework.Upgrade.Services;

namespace XiHan.Framework.Upgrade.Tests;

/// <summary>
/// 升级引擎测试
/// </summary>
/// <remarks>
/// 引擎是整个模块的编排中枢，测试重点是编排顺序与短路逻辑：
/// 非主节点直接退出、无需升级不抢锁、抢不到锁不改状态、
/// 脚本按版本顺序执行、中途失败立即停止并回滚为失败态、锁一定被释放。
/// 所有协作者都是手写替身，用一个共享调用序列记录真实调用顺序。
/// </remarks>
public class UpgradeEngineTests : IDisposable
{
    private readonly string _rootPath;

    /// <summary>
    /// 构造函数，准备独立的临时脚本目录
    /// </summary>
    public UpgradeEngineTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    /// <summary>
    /// 非主节点时直接退出，不碰版本存储也不抢锁
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenNodeIsNotPrimary_ReturnsWithoutTouchingStore()
    {
        var harness = new EngineHarness();
        harness.Options.AppVersion = "2.0.0";
        harness.Options.NodeName = "node-b";
        harness.Options.PrimaryNodeName = "node-a";
        harness.Store.State.AppVersion = "1.0.0";

        var result = await harness.Build().ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Started);
        Assert.Equal(UpgradeStatus.Normal, result.Status);
        Assert.Equal("当前节点非主节点，等待升级", result.Message);
        Assert.Equal(0, harness.Store.EnsureTablesCount);
        Assert.Equal(0, harness.LockProvider.AcquireCount);
        Assert.Empty(harness.Calls);
    }

    /// <summary>
    /// 主节点名比较忽略大小写
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenPrimaryNodeNameDiffersOnlyByCase_ProceedsAsPrimary()
    {
        var harness = new EngineHarness();
        harness.Options.AppVersion = "1.0.0";
        harness.Options.NodeName = "Node-A";
        harness.Options.PrimaryNodeName = "node-a";
        harness.Store.State.AppVersion = "1.0.0";
        harness.Store.State.DbVersion = "1.0.0";

        var result = await harness.Build().ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal("无需升级", result.Message);
        Assert.Equal(1, harness.Store.EnsureTablesCount);
    }

    /// <summary>
    /// 应用版本与数据库版本都不落后时不需要升级，也不去抢锁
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenNothingToUpgrade_SkipsLockAcquisition()
    {
        var harness = new EngineHarness();
        harness.Options.AppVersion = "1.0.0";
        harness.Store.State.AppVersion = "1.0.0";
        harness.Store.State.DbVersion = "1.0.0";

        var result = await harness.Build().ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Started);
        Assert.Equal(UpgradeStatus.Normal, result.Status);
        Assert.Equal("无需升级", result.Message);
        Assert.Equal(0, harness.LockProvider.AcquireCount);
        Assert.Null(harness.Store.UpgradingNode);
    }

    /// <summary>
    /// 抢不到升级锁时报告升级中，且不修改版本状态
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenLockHeldByOthers_ReportsUpgradingWithoutStateChange()
    {
        var harness = new EngineHarness();
        harness.Options.AppVersion = "2.0.0";
        harness.Store.State.AppVersion = "1.0.0";
        harness.LockProvider.ReturnNull = true;

        var result = await harness.Build().ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Started);
        Assert.Equal(UpgradeStatus.Upgrading, result.Status);
        Assert.Equal("升级锁已被占用", result.Message);
        Assert.Equal(1, harness.LockProvider.AcquireCount);
        Assert.Null(harness.Store.UpgradingNode);
        Assert.Null(harness.Store.CompletedAppVersion);
    }

    /// <summary>
    /// 无租户时锁资源键取配置值，过期时间取配置秒数
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenNoTenant_UsesConfiguredLockKeyAndExpiry()
    {
        var harness = new EngineHarness();
        harness.Options.AppVersion = "2.0.0";
        harness.Options.LockResourceKey = "CustomUpgrade";
        harness.Options.LockExpirySeconds = 42;
        harness.Store.State.AppVersion = "1.0.0";
        harness.Store.State.TenantId = null;

        await harness.Build().ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal("CustomUpgrade", harness.LockProvider.LastResourceKey);
        Assert.Equal(TimeSpan.FromSeconds(42), harness.LockProvider.LastExpiry);
    }

    /// <summary>
    /// 有租户时锁资源键带租户后缀，避免跨租户互锁
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenTenantScoped_AppendsTenantSuffixToLockKey()
    {
        var harness = new EngineHarness();
        harness.Options.AppVersion = "2.0.0";
        harness.Store.State.AppVersion = "1.0.0";
        harness.Store.State.TenantId = 7;

        await harness.Build().ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal("SystemUpgrade:Tenant_7", harness.LockProvider.LastResourceKey);
    }

    /// <summary>
    /// 未配置节点名时用「机器名-实例标识」作为节点名
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenNodeNameNotConfigured_UsesMachineNameAndInstanceId()
    {
        var harness = new EngineHarness { InstanceId = "abc123" };
        var expectedNodeName = $"{Environment.MachineName}-abc123";
        harness.Options.AppVersion = "2.0.0";
        harness.Options.NodeName = null;
        harness.Options.PrimaryNodeName = expectedNodeName;
        harness.Store.State.AppVersion = "1.0.0";

        var result = await harness.Build().ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Started);
        Assert.Equal(expectedNodeName, harness.Store.UpgradingNode);
        Assert.Equal(expectedNodeName, harness.LockProvider.LastNodeName);
    }

    /// <summary>
    /// 完整成功流程的调用顺序固定：建表→读状态→抢锁→置升级中→进维护→写完成→换文件→出维护→放锁→滚动重启
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenAllStepsEnabled_RunsStepsInFixedOrder()
    {
        var harness = new EngineHarness();
        harness.Options.AppVersion = "2.0.0";
        harness.Options.EnableMaintenanceMode = true;
        harness.Options.EnableFileUpdate = true;
        harness.Options.EnableRollingRestart = true;
        harness.Store.State.AppVersion = "1.0.0";
        harness.Store.State.DbVersion = "1.0.0";

        var result = await harness.Build().ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Started);
        Assert.Equal(UpgradeStatus.Completed, result.Status);
        Assert.Equal("升级完成", result.Message);
        Assert.Equal(
            [
                "store:ensure-tables",
                "store:get-or-create",
                "lock:acquire",
                "store:upgrading",
                "maintenance:enter",
                "store:completed",
                "file:apply",
                "maintenance:exit",
                "lock:release",
                "restart"
            ],
            harness.Calls);
        Assert.Empty(harness.MigrationExecutor.ExecutedSql);
        Assert.NotNull(harness.LockProvider.Token);
        Assert.True(harness.LockProvider.Token!.IsReleased);
    }

    /// <summary>
    /// 维护模式关闭时不进出维护模式，其余步骤照常
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenMaintenanceModeDisabled_SkipsMaintenanceSteps()
    {
        var harness = new EngineHarness();
        harness.Options.AppVersion = "2.0.0";
        harness.Options.EnableMaintenanceMode = false;
        harness.Store.State.AppVersion = "1.0.0";

        var result = await harness.Build().ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpgradeStatus.Completed, result.Status);
        Assert.Equal(0, harness.Maintenance.EnterCount);
        Assert.Equal(0, harness.Maintenance.ExitCount);
    }

    /// <summary>
    /// 文件替换与滚动重启默认关闭，不会被意外触发
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithDefaultOptions_DoesNotUpdateFilesOrRestart()
    {
        var harness = new EngineHarness();
        harness.Options.AppVersion = "2.0.0";
        harness.Store.State.AppVersion = "1.0.0";

        var result = await harness.Build().ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpgradeStatus.Completed, result.Status);
        Assert.Equal(0, harness.FileUpdater.ApplyCount);
        Assert.Equal(0, harness.Restart.RestartCount);
    }

    /// <summary>
    /// 脚本按版本升序、同版本内按脚本名升序执行，并逐版本推进数据库版本
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RunsScriptsInVersionOrderAndAdvancesDbVersion()
    {
        var harness = new EngineHarness();
        var secondOfFirstVersion = WriteScript("1.0.0", "02_second.sql", "-- 1.0.0/02");
        var firstOfFirstVersion = WriteScript("1.0.0", "01_first.sql", "-- 1.0.0/01");
        var onlyOfSecondVersion = WriteScript("1.1.0", "01_only.sql", "-- 1.1.0/01");
        harness.ScriptProviders.Add(new FakeUpgradeScriptProvider([onlyOfSecondVersion, secondOfFirstVersion, firstOfFirstVersion]));
        harness.Options.AppVersion = "1.1.0";
        harness.Options.NodeName = "node-a";
        harness.Store.State.AppVersion = "1.0.0";
        harness.Store.State.DbVersion = "0.0.0";

        var result = await harness.Build().ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Started);
        Assert.Equal(UpgradeStatus.Completed, result.Status);
        Assert.Equal(["-- 1.0.0/01", "-- 1.0.0/02", "-- 1.1.0/01"], harness.MigrationExecutor.ExecutedSql);
        Assert.Equal(["1.0.0", "1.1.0"], harness.Store.DbVersionUpdates);
        Assert.Equal("1.1.0", harness.Store.CompletedAppVersion);
        Assert.Equal("1.1.0", harness.Store.CompletedDbVersion);
        Assert.Equal(3, harness.Store.Histories.Count);
        Assert.All(harness.Store.Histories, history =>
        {
            Assert.True(history.Success);
            Assert.Equal("node-a", history.NodeName);
            Assert.Null(history.ErrorMessage);
        });
    }

    /// <summary>
    /// 只执行版本高于当前数据库版本的脚本
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SkipsScriptsNotNewerThanCurrentDbVersion()
    {
        var harness = new EngineHarness();
        var applied = WriteScript("1.0.0", "01_applied.sql", "-- applied");
        var pending = WriteScript("1.1.0", "01_pending.sql", "-- pending");
        harness.ScriptProviders.Add(new FakeUpgradeScriptProvider([applied, pending]));
        harness.Options.AppVersion = "1.1.0";
        harness.Store.State.AppVersion = "1.0.0";
        harness.Store.State.DbVersion = "1.0.0";

        await harness.Build().ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["-- pending"], harness.MigrationExecutor.ExecutedSql);
        Assert.Equal(["1.1.0"], harness.Store.DbVersionUpdates);
    }

    /// <summary>
    /// 迁移历史里已成功执行过的脚本被跳过，保证幂等
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SkipsScriptsAlreadyRecordedInHistory()
    {
        var harness = new EngineHarness();
        var first = WriteScript("1.0.0", "01_first.sql", "-- first");
        var second = WriteScript("1.0.0", "02_second.sql", "-- second");
        harness.ScriptProviders.Add(new FakeUpgradeScriptProvider([first, second]));
        harness.Store.ExecutedKeys.Add("1.0.0|01_first.sql");
        harness.Options.AppVersion = "1.0.0";
        harness.Store.State.AppVersion = "0.9.0";

        await harness.Build().ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["-- second"], harness.MigrationExecutor.ExecutedSql);
        var history = Assert.Single(harness.Store.Histories);
        Assert.Equal("02_second.sql", history.ScriptName);
    }

    /// <summary>
    /// 多个脚本提供者的脚本会被合并后统一排序执行
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MergesScriptsFromAllProviders()
    {
        var harness = new EngineHarness();
        var fromSecond = WriteScript("1.1.0", "01_b.sql", "-- b");
        var fromFirst = WriteScript("1.0.0", "01_a.sql", "-- a");
        harness.ScriptProviders.Add(new FakeUpgradeScriptProvider([fromFirst]));
        harness.ScriptProviders.Add(new FakeUpgradeScriptProvider([fromSecond]));
        harness.Options.AppVersion = "1.1.0";
        harness.Store.State.AppVersion = "1.0.0";

        await harness.Build().ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["-- a", "-- b"], harness.MigrationExecutor.ExecutedSql);
    }

    /// <summary>
    /// 某个脚本失败时立即停止后续脚本，落失败历史、置失败态、退维护模式并释放锁
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenScriptFails_StopsAndMarksFailed()
    {
        var harness = new EngineHarness();
        var first = WriteScript("1.0.0", "01_a.sql", "-- a");
        var failing = WriteScript("1.0.0", "02_b.sql", "-- b");
        var never = WriteScript("1.1.0", "01_c.sql", "-- c");
        harness.ScriptProviders.Add(new FakeUpgradeScriptProvider([first, failing, never]));
        harness.MigrationExecutor.FailWhen = sql => sql == "-- b";
        harness.Options.AppVersion = "1.1.0";
        harness.Store.State.AppVersion = "1.0.0";

        var result = await harness.Build().ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Started);
        Assert.Equal(UpgradeStatus.Failed, result.Status);
        Assert.StartsWith("升级失败: ", result.Message);
        Assert.Equal(["-- a", "-- b"], harness.MigrationExecutor.ExecutedSql);
        Assert.Empty(harness.Store.DbVersionUpdates);
        Assert.Equal(1, harness.Store.FailedCount);
        Assert.Null(harness.Store.CompletedAppVersion);
        Assert.Equal(1, harness.Maintenance.ExitCount);
        Assert.NotNull(harness.LockProvider.Token);
        Assert.True(harness.LockProvider.Token!.IsReleased);

        var failedHistory = harness.Store.Histories[^1];
        Assert.False(failedHistory.Success);
        Assert.Equal("02_b.sql", failedHistory.ScriptName);
        Assert.Equal("1.0.0", failedHistory.Version);
        Assert.False(string.IsNullOrWhiteSpace(failedHistory.ErrorMessage));
    }

    /// <summary>
    /// 启用多租户隔离时逐租户执行并汇总为完成
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenMultiTenantIsolationEnabled_RunsPerTenant()
    {
        var harness = new EngineHarness
        {
            TenantProvider = new FakeUpgradeTenantProvider([new BasicTenantInfo(1, "t1"), new BasicTenantInfo(2, "t2")])
        };
        harness.Options.EnableMultiTenantIsolation = true;
        harness.Options.AppVersion = "1.0.0";
        harness.Store.State.AppVersion = "1.0.0";
        harness.Store.State.DbVersion = "1.0.0";

        var result = await harness.Build().ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Started);
        Assert.Equal(UpgradeStatus.Completed, result.Status);
        Assert.Equal("多租户升级完成", result.Message);
        Assert.Equal(1, harness.TenantProvider.CallCount);
        Assert.Equal(2, harness.Store.EnsureTablesCount);
    }

    /// <summary>
    /// 多租户模式下某个租户失败会立即中止，不再处理后续租户
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenTenantUpgradeFails_StopsRemainingTenants()
    {
        var harness = new EngineHarness
        {
            TenantProvider = new FakeUpgradeTenantProvider([new BasicTenantInfo(1, "t1"), new BasicTenantInfo(2, "t2")])
        };
        var script = WriteScript("1.0.0", "01_a.sql", "-- a");
        harness.ScriptProviders.Add(new FakeUpgradeScriptProvider([script]));
        harness.MigrationExecutor.FailWhen = _ => true;
        harness.Options.EnableMultiTenantIsolation = true;
        harness.Options.AppVersion = "1.1.0";
        harness.Store.State.AppVersion = "1.0.0";

        var result = await harness.Build().ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpgradeStatus.Failed, result.Status);
        Assert.Equal(1, harness.Store.EnsureTablesCount);
    }

    /// <summary>
    /// 迁移历史带上版本状态里的租户标识，便于按租户回溯
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WritesTenantIdIntoMigrationHistory()
    {
        var harness = new EngineHarness();
        var script = WriteScript("1.0.0", "01_a.sql", "-- a");
        harness.ScriptProviders.Add(new FakeUpgradeScriptProvider([script]));
        harness.Options.AppVersion = "1.0.0";
        harness.Store.State.AppVersion = "0.9.0";
        harness.Store.State.TenantId = 88;

        await harness.Build().ExecuteAsync(TestContext.Current.CancellationToken);

        var history = Assert.Single(harness.Store.Histories);
        Assert.NotNull(history.TenantId);
        Assert.Equal(88L, history.TenantId!.Value);
        Assert.Equal("1.0.0", history.Version);
        Assert.Equal("01_a.sql", history.ScriptName);
    }

    /// <summary>
    /// 清理临时目录
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 写入脚本文件并返回对应的脚本描述
    /// </summary>
    /// <param name="version">版本</param>
    /// <param name="scriptName">脚本名</param>
    /// <param name="content">脚本内容</param>
    /// <returns>脚本描述</returns>
    private UpgradeScript WriteScript(string version, string scriptName, string content)
    {
        var directory = Path.Combine(_rootPath, version);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, scriptName);
        File.WriteAllText(path, content);
        return new UpgradeScript(version, scriptName, path);
    }

    /// <summary>
    /// 升级引擎测试脚手架，统一装配替身并共享调用序列
    /// </summary>
    private sealed class EngineHarness
    {
        public EngineHarness()
        {
            Store = new FakeUpgradeVersionStore(Calls);
            LockProvider = new FakeUpgradeLockProvider(Calls);
            Maintenance = new FakeUpgradeMaintenanceModeManager(Calls);
            FileUpdater = new FakeUpgradeFileUpdater(Calls);
            Restart = new FakeRollingRestartCoordinator(Calls);
            MigrationExecutor = new FakeUpgradeMigrationExecutor(Calls);
            TenantProvider = new FakeUpgradeTenantProvider([new BasicTenantInfo(null)]);
        }

        public List<string> Calls { get; } = [];

        public XiHanUpgradeOptions Options { get; } = new();

        public FakeUpgradeVersionStore Store { get; }

        public FakeUpgradeLockProvider LockProvider { get; }

        public FakeUpgradeMaintenanceModeManager Maintenance { get; }

        public FakeUpgradeFileUpdater FileUpdater { get; }

        public FakeRollingRestartCoordinator Restart { get; }

        public FakeUpgradeMigrationExecutor MigrationExecutor { get; }

        public FakeUpgradeTenantProvider TenantProvider { get; init; }

        public List<IUpgradeScriptProvider> ScriptProviders { get; } = [];

        public string InstanceId { get; init; } = "instance-1";

        public UpgradeEngine Build()
        {
            return new UpgradeEngine(
                Store,
                ScriptProviders,
                LockProvider,
                Maintenance,
                FileUpdater,
                Restart,
                TenantProvider,
                MigrationExecutor,
                new EmptyServiceProvider(),
                new FakeApplicationInfoAccessor(InstanceId),
                new OptionsWrapper<XiHanUpgradeOptions>(Options),
                NullLogger<UpgradeEngine>.Instance);
        }
    }

    /// <summary>
    /// 什么都解析不出来的服务提供器，用于验证「没有 ICurrentTenant 也能跑」
    /// </summary>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }

    /// <summary>
    /// 版本存储替身
    /// </summary>
    private sealed class FakeUpgradeVersionStore : IUpgradeVersionStore
    {
        private readonly List<string> _calls;

        public FakeUpgradeVersionStore(List<string> calls)
        {
            _calls = calls;
        }

        public UpgradeVersionState State { get; } = new() { Id = 1, AppVersion = "0.0.0", DbVersion = "0.0.0" };

        public int EnsureTablesCount { get; private set; }

        public string? UpgradingNode { get; private set; }

        public DateTimeOffset? UpgradingStartTime { get; private set; }

        public string? CompletedAppVersion { get; private set; }

        public string? CompletedDbVersion { get; private set; }

        public int FailedCount { get; private set; }

        public List<string> DbVersionUpdates { get; } = [];

        public List<UpgradeMigrationHistory> Histories { get; } = [];

        public HashSet<string> ExecutedKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

        public UpgradeMigrationHistory? LatestHistory { get; set; }

        public Task EnsureTablesAsync(CancellationToken cancellationToken = default)
        {
            EnsureTablesCount++;
            _calls.Add("store:ensure-tables");
            return Task.CompletedTask;
        }

        public Task<UpgradeVersionState> GetOrCreateAsync(string currentAppVersion, string minSupportVersion, CancellationToken cancellationToken = default)
        {
            _calls.Add("store:get-or-create");
            return Task.FromResult(State);
        }

        public Task<UpgradeMigrationHistory?> GetLatestHistoryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LatestHistory);
        }

        public Task SetUpgradingAsync(UpgradeVersionState version, string nodeName, DateTimeOffset startTime, CancellationToken cancellationToken = default)
        {
            _calls.Add("store:upgrading");
            UpgradingNode = nodeName;
            UpgradingStartTime = startTime;
            version.IsUpgrading = true;
            version.UpgradeNode = nodeName;
            version.UpgradeStartTime = startTime;
            return Task.CompletedTask;
        }

        public Task SetUpgradeCompletedAsync(UpgradeVersionState version, string appVersion, string dbVersion, CancellationToken cancellationToken = default)
        {
            _calls.Add("store:completed");
            CompletedAppVersion = appVersion;
            CompletedDbVersion = dbVersion;
            version.IsUpgrading = false;
            version.AppVersion = appVersion;
            version.DbVersion = dbVersion;
            return Task.CompletedTask;
        }

        public Task SetUpgradeFailedAsync(UpgradeVersionState version, CancellationToken cancellationToken = default)
        {
            _calls.Add("store:failed");
            FailedCount++;
            version.IsUpgrading = false;
            return Task.CompletedTask;
        }

        public Task UpdateDbVersionAsync(UpgradeVersionState version, string dbVersion, CancellationToken cancellationToken = default)
        {
            _calls.Add("store:db-version");
            DbVersionUpdates.Add(dbVersion);
            version.DbVersion = dbVersion;
            return Task.CompletedTask;
        }

        public Task AddMigrationHistoryAsync(UpgradeMigrationHistory history, CancellationToken cancellationToken = default)
        {
            Histories.Add(history);
            return Task.CompletedTask;
        }

        public Task<bool> HasMigrationHistoryAsync(string version, string scriptName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExecutedKeys.Contains($"{version}|{scriptName}"));
        }
    }

    /// <summary>
    /// 升级锁提供者替身
    /// </summary>
    private sealed class FakeUpgradeLockProvider : IUpgradeLockProvider
    {
        private readonly List<string> _calls;

        public FakeUpgradeLockProvider(List<string> calls)
        {
            _calls = calls;
        }

        public bool ReturnNull { get; set; }

        public int AcquireCount { get; private set; }

        public string? LastResourceKey { get; private set; }

        public string? LastNodeName { get; private set; }

        public TimeSpan LastExpiry { get; private set; }

        public FakeUpgradeLockToken? Token { get; private set; }

        public Task<IUpgradeLockToken?> TryAcquireLockAsync(string resourceKey, TimeSpan expiry, string nodeName, CancellationToken cancellationToken = default)
        {
            AcquireCount++;
            LastResourceKey = resourceKey;
            LastNodeName = nodeName;
            LastExpiry = expiry;
            _calls.Add("lock:acquire");

            if (ReturnNull)
            {
                return Task.FromResult<IUpgradeLockToken?>(null);
            }

            Token = new FakeUpgradeLockToken(resourceKey, _calls);
            return Task.FromResult<IUpgradeLockToken?>(Token);
        }
    }

    /// <summary>
    /// 升级锁令牌替身，重复释放只记一次以便断言调用顺序
    /// </summary>
    private sealed class FakeUpgradeLockToken : IUpgradeLockToken
    {
        private readonly List<string> _calls;

        public FakeUpgradeLockToken(string resourceKey, List<string> calls)
        {
            ResourceKey = resourceKey;
            _calls = calls;
        }

        public string ResourceKey { get; }

        public string LockId { get; } = Guid.NewGuid().ToString("N");

        public bool IsReleased { get; private set; }

        public int ReleaseCount { get; private set; }

        public Task ReleaseAsync()
        {
            ReleaseCount++;
            if (!IsReleased)
            {
                IsReleased = true;
                _calls.Add("lock:release");
            }

            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await ReleaseAsync();
        }
    }

    /// <summary>
    /// 维护模式管理器替身
    /// </summary>
    private sealed class FakeUpgradeMaintenanceModeManager : IUpgradeMaintenanceModeManager
    {
        private readonly List<string> _calls;

        public FakeUpgradeMaintenanceModeManager(List<string> calls)
        {
            _calls = calls;
        }

        public int EnterCount { get; private set; }

        public int ExitCount { get; private set; }

        public Task EnterAsync(CancellationToken cancellationToken = default)
        {
            EnterCount++;
            _calls.Add("maintenance:enter");
            return Task.CompletedTask;
        }

        public Task ExitAsync(CancellationToken cancellationToken = default)
        {
            ExitCount++;
            _calls.Add("maintenance:exit");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 程序文件更新器替身
    /// </summary>
    private sealed class FakeUpgradeFileUpdater : IUpgradeFileUpdater
    {
        private readonly List<string> _calls;

        public FakeUpgradeFileUpdater(List<string> calls)
        {
            _calls = calls;
        }

        public int ApplyCount { get; private set; }

        public Task ApplyAsync(CancellationToken cancellationToken = default)
        {
            ApplyCount++;
            _calls.Add("file:apply");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 滚动重启协调器替身
    /// </summary>
    private sealed class FakeRollingRestartCoordinator : IRollingRestartCoordinator
    {
        private readonly List<string> _calls;

        public FakeRollingRestartCoordinator(List<string> calls)
        {
            _calls = calls;
        }

        public int RestartCount { get; private set; }

        public Task RestartAsync(CancellationToken cancellationToken = default)
        {
            RestartCount++;
            _calls.Add("restart");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 迁移执行器替身，可按脚本内容触发失败
    /// </summary>
    private sealed class FakeUpgradeMigrationExecutor : IUpgradeMigrationExecutor
    {
        private readonly List<string> _calls;

        public FakeUpgradeMigrationExecutor(List<string> calls)
        {
            _calls = calls;
        }

        public List<string> ExecutedSql { get; } = [];

        public Func<string, bool>? FailWhen { get; set; }

        public Task ExecuteAsync(string sql, CancellationToken cancellationToken = default)
        {
            ExecutedSql.Add(sql);
            _calls.Add("migration:execute");

            if (FailWhen is not null && FailWhen(sql))
            {
                throw new InvalidOperationException("模拟迁移脚本执行失败");
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 脚本提供者替身
    /// </summary>
    private sealed class FakeUpgradeScriptProvider : IUpgradeScriptProvider
    {
        private readonly IReadOnlyList<UpgradeScript> _scripts;

        public FakeUpgradeScriptProvider(IReadOnlyList<UpgradeScript> scripts)
        {
            _scripts = scripts;
        }

        public Task<IReadOnlyList<UpgradeScript>> GetScriptsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_scripts);
        }
    }

    /// <summary>
    /// 租户提供者替身
    /// </summary>
    private sealed class FakeUpgradeTenantProvider : IUpgradeTenantProvider
    {
        private readonly IReadOnlyList<BasicTenantInfo> _tenants;

        public FakeUpgradeTenantProvider(IReadOnlyList<BasicTenantInfo> tenants)
        {
            _tenants = tenants;
        }

        public int CallCount { get; private set; }

        public IReadOnlyList<BasicTenantInfo> GetTenants()
        {
            CallCount++;
            return _tenants;
        }
    }
}
