// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Upgrade.Abstractions;
using XiHan.Framework.Upgrade.Enums;
using XiHan.Framework.Upgrade.Models;
using XiHan.Framework.Upgrade.Options;
using XiHan.Framework.Upgrade.Services;

namespace XiHan.Framework.Upgrade.Tests;

/// <summary>
/// 升级状态服务测试
/// </summary>
/// <remarks>
/// 该服务对外给出「要不要升级 / 客户端还兼不兼容 / 现在是什么状态」三个判断，
/// 重点覆盖状态机的四个出口（升级中 / 失败 / 完成 / 正常）与强制升级判定边界。
/// </remarks>
public class UpgradeStatusServiceTests
{
    /// <summary>
    /// 关闭启动自检时不触碰版本存储
    /// </summary>
    [Fact]
    public async Task EnsureInitializedAsync_WhenAutoCheckDisabled_DoesNotTouchStore()
    {
        var store = new FakeVersionStore();
        var service = CreateService(new XiHanUpgradeOptions { AppVersion = "1.0.0", EnableAutoCheckOnStartup = false }, store);

        await service.EnsureInitializedAsync();

        Assert.Equal(0, store.GetOrCreateCount);
    }

    /// <summary>
    /// 开启启动自检时按配置的版本建仓
    /// </summary>
    [Fact]
    public async Task EnsureInitializedAsync_WhenAutoCheckEnabled_CreatesVersionRecord()
    {
        var store = new FakeVersionStore();
        var service = CreateService(
            new XiHanUpgradeOptions { AppVersion = "1.2.0", MinSupportVersion = "1.0.0", EnableAutoCheckOnStartup = true },
            store);

        await service.EnsureInitializedAsync();

        Assert.Equal(1, store.GetOrCreateCount);
        Assert.Equal("1.2.0", store.LastAppVersion);
        Assert.Equal("1.0.0", store.LastMinSupportVersion);
    }

    /// <summary>
    /// 版本齐平时快照报告无需升级且完全兼容
    /// </summary>
    [Fact]
    public async Task GetVersionSnapshotAsync_WhenUpToDate_ReportsNoUpgrade()
    {
        var store = new FakeVersionStore();
        store.State.AppVersion = "1.0.0";
        store.State.DbVersion = "1.0.0";
        var service = CreateService(NewOptions(), store);

        var snapshot = await service.GetVersionSnapshotAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal("1.0.0", snapshot.CurrentAppVersion);
        Assert.Equal("1.0.0", snapshot.CurrentDbVersion);
        Assert.Equal("0.9.0", snapshot.MinSupportVersion);
        Assert.Equal("1.0.0", snapshot.RecordedAppVersion);
        Assert.False(snapshot.NeedUpgrade);
        Assert.False(snapshot.ForceUpgrade);
        Assert.True(snapshot.IsCompatible);
        Assert.False(snapshot.IsUpgrading);
    }

    /// <summary>
    /// 存在更高版本的脚本时报告需要升级
    /// </summary>
    [Fact]
    public async Task GetVersionSnapshotAsync_WhenScriptVersionAhead_ReportsNeedUpgrade()
    {
        var store = new FakeVersionStore();
        store.State.AppVersion = "1.0.0";
        store.State.DbVersion = "1.0.0";
        var service = CreateService(NewOptions(), store, new FakeScriptProvider([new UpgradeScript("1.1.0", "a.sql", "a.sql")]));

        var snapshot = await service.GetVersionSnapshotAsync(null, TestContext.Current.CancellationToken);

        Assert.True(snapshot.NeedUpgrade);
    }

    /// <summary>
    /// 已记录的应用版本落后于当前应用版本时报告需要升级
    /// </summary>
    [Fact]
    public async Task GetVersionSnapshotAsync_WhenRecordedAppVersionBehind_ReportsNeedUpgrade()
    {
        var store = new FakeVersionStore();
        store.State.AppVersion = "0.9.0";
        store.State.DbVersion = "1.0.0";
        var service = CreateService(NewOptions(), store);

        var snapshot = await service.GetVersionSnapshotAsync(null, TestContext.Current.CancellationToken);

        Assert.True(snapshot.NeedUpgrade);
        Assert.Equal("0.9.0", snapshot.RecordedAppVersion);
    }

    /// <summary>
    /// 最新脚本版本按语义序选取，1.0.10 高于 1.0.9
    /// </summary>
    /// <remarks>
    /// 若按字典序取最大值会得到 1.0.9，从而漏判升级，这是最容易踩的一处。
    /// </remarks>
    [Fact]
    public async Task GetVersionSnapshotAsync_PicksLatestScriptVersionSemantically()
    {
        var store = new FakeVersionStore();
        store.State.AppVersion = "1.0.0";
        store.State.DbVersion = "1.0.9";
        var service = CreateService(
            NewOptions(),
            store,
            new FakeScriptProvider([new UpgradeScript("1.0.9", "a.sql", "a.sql")]),
            new FakeScriptProvider([new UpgradeScript("1.0.10", "b.sql", "b.sql")]));

        var snapshot = await service.GetVersionSnapshotAsync(null, TestContext.Current.CancellationToken);

        Assert.True(snapshot.NeedUpgrade);
    }

    /// <summary>
    /// 客户端版本低于最小支持版本时强制升级且判定为不兼容
    /// </summary>
    [Fact]
    public async Task GetVersionSnapshotAsync_WhenClientVersionBelowMinSupport_ForcesUpgrade()
    {
        var store = new FakeVersionStore();
        store.State.AppVersion = "1.0.0";
        store.State.DbVersion = "1.0.0";
        var service = CreateService(NewOptions(), store);

        var snapshot = await service.GetVersionSnapshotAsync("0.8.0", TestContext.Current.CancellationToken);

        Assert.True(snapshot.ForceUpgrade);
        Assert.False(snapshot.IsCompatible);
    }

    /// <summary>
    /// 客户端版本达到最小支持版本时不强制升级
    /// </summary>
    /// <param name="clientVersion">客户端版本</param>
    [Theory]
    [InlineData("0.9.0")]
    [InlineData("1.0.0")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GetVersionSnapshotAsync_WhenClientVersionSupportedOrMissing_DoesNotForceUpgrade(string? clientVersion)
    {
        var store = new FakeVersionStore();
        store.State.AppVersion = "1.0.0";
        store.State.DbVersion = "1.0.0";
        var service = CreateService(NewOptions(), store);

        var snapshot = await service.GetVersionSnapshotAsync(clientVersion, TestContext.Current.CancellationToken);

        Assert.False(snapshot.ForceUpgrade);
        Assert.True(snapshot.IsCompatible);
    }

    /// <summary>
    /// 升级节点与开始时间原样透出，便于排查是哪台机器在升级
    /// </summary>
    [Fact]
    public async Task GetVersionSnapshotAsync_MapsUpgradeNodeAndStartTime()
    {
        var startTime = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);
        var store = new FakeVersionStore();
        store.State.AppVersion = "1.0.0";
        store.State.DbVersion = "1.0.0";
        store.State.IsUpgrading = true;
        store.State.UpgradeNode = "node-a";
        store.State.UpgradeStartTime = startTime;
        var service = CreateService(NewOptions(), store);

        var snapshot = await service.GetVersionSnapshotAsync(null, TestContext.Current.CancellationToken);

        Assert.True(snapshot.IsUpgrading);
        Assert.Equal("node-a", snapshot.UpgradeNode);
        Assert.Equal(startTime, snapshot.UpgradeStartTime);
        Assert.Equal(UpgradeStatus.Upgrading, snapshot.Status);
    }

    /// <summary>
    /// 升级中标记优先级最高，即使最后一条历史是失败的
    /// </summary>
    [Fact]
    public async Task GetUpgradeStatusAsync_WhenUpgrading_ReturnsUpgrading()
    {
        var store = new FakeVersionStore();
        store.State.IsUpgrading = true;
        store.LatestHistory = new UpgradeMigrationHistory { Success = false };
        var service = CreateService(NewOptions(), store);

        var status = await service.GetUpgradeStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpgradeStatus.Upgrading, status);
    }

    /// <summary>
    /// 最后一条迁移历史失败时报告失败
    /// </summary>
    [Fact]
    public async Task GetUpgradeStatusAsync_WhenLastHistoryFailed_ReturnsFailed()
    {
        var store = new FakeVersionStore();
        store.State.AppVersion = "1.0.0";
        store.State.DbVersion = "1.0.0";
        store.LatestHistory = new UpgradeMigrationHistory { Success = false, ScriptName = "a.sql" };
        var service = CreateService(NewOptions(), store);

        var status = await service.GetUpgradeStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpgradeStatus.Failed, status);
    }

    /// <summary>
    /// 无需升级且有成功历史时报告完成
    /// </summary>
    [Fact]
    public async Task GetUpgradeStatusAsync_WhenNoUpgradeNeededAndHistorySucceeded_ReturnsCompleted()
    {
        var store = new FakeVersionStore();
        store.State.AppVersion = "1.0.0";
        store.State.DbVersion = "1.0.0";
        store.LatestHistory = new UpgradeMigrationHistory { Success = true, ScriptName = "a.sql" };
        var service = CreateService(NewOptions(), store);

        var status = await service.GetUpgradeStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpgradeStatus.Completed, status);
    }

    /// <summary>
    /// 无需升级且曾经发起过升级（有开始时间）时报告完成
    /// </summary>
    [Fact]
    public async Task GetUpgradeStatusAsync_WhenNoUpgradeNeededAndUpgradeStartTimeRecorded_ReturnsCompleted()
    {
        var store = new FakeVersionStore();
        store.State.AppVersion = "1.0.0";
        store.State.DbVersion = "1.0.0";
        store.State.UpgradeStartTime = DateTimeOffset.UtcNow;
        var service = CreateService(NewOptions(), store);

        var status = await service.GetUpgradeStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpgradeStatus.Completed, status);
    }

    /// <summary>
    /// 从未升级过且无需升级时报告正常
    /// </summary>
    [Fact]
    public async Task GetUpgradeStatusAsync_WhenNeverUpgraded_ReturnsNormal()
    {
        var store = new FakeVersionStore();
        store.State.AppVersion = "1.0.0";
        store.State.DbVersion = "1.0.0";
        var service = CreateService(NewOptions(), store);

        var status = await service.GetUpgradeStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpgradeStatus.Normal, status);
    }

    /// <summary>
    /// 还需要升级时不报告完成，仍是正常（待升级）状态
    /// </summary>
    [Fact]
    public async Task GetUpgradeStatusAsync_WhenUpgradeStillNeeded_ReturnsNormal()
    {
        var store = new FakeVersionStore();
        store.State.AppVersion = "0.9.0";
        store.State.DbVersion = "1.0.0";
        store.LatestHistory = new UpgradeMigrationHistory { Success = true, ScriptName = "a.sql" };
        var service = CreateService(NewOptions(), store);

        var status = await service.GetUpgradeStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpgradeStatus.Normal, status);
    }

    /// <summary>
    /// 快照里的状态与单独查询状态保持一致
    /// </summary>
    [Fact]
    public async Task GetVersionSnapshotAsync_StatusMatchesGetUpgradeStatusAsync()
    {
        var store = new FakeVersionStore();
        store.State.AppVersion = "1.0.0";
        store.State.DbVersion = "1.0.0";
        store.LatestHistory = new UpgradeMigrationHistory { Success = false, ScriptName = "a.sql" };
        var service = CreateService(NewOptions(), store);
        var cancellationToken = TestContext.Current.CancellationToken;

        var snapshot = await service.GetVersionSnapshotAsync(null, cancellationToken);
        var status = await service.GetUpgradeStatusAsync(cancellationToken);

        Assert.Equal(status, snapshot.Status);
        Assert.Equal(UpgradeStatus.Failed, status);
    }

    /// <summary>
    /// 构造默认测试选项：应用版本 1.0.0，最小支持版本 0.9.0
    /// </summary>
    /// <returns>升级选项</returns>
    private static XiHanUpgradeOptions NewOptions()
    {
        return new XiHanUpgradeOptions { AppVersion = "1.0.0", MinSupportVersion = "0.9.0" };
    }

    /// <summary>
    /// 创建升级状态服务
    /// </summary>
    /// <param name="options">升级选项</param>
    /// <param name="store">版本存储替身</param>
    /// <param name="scriptProviders">脚本提供者替身</param>
    /// <returns>升级状态服务</returns>
    private static UpgradeStatusService CreateService(
        XiHanUpgradeOptions options,
        FakeVersionStore store,
        params IUpgradeScriptProvider[] scriptProviders)
    {
        return new UpgradeStatusService(store, scriptProviders, new OptionsWrapper<XiHanUpgradeOptions>(options));
    }

    /// <summary>
    /// 版本存储替身
    /// </summary>
    private sealed class FakeVersionStore : IUpgradeVersionStore
    {
        public UpgradeVersionState State { get; } = new() { Id = 1, AppVersion = "1.0.0", DbVersion = "1.0.0" };

        public UpgradeMigrationHistory? LatestHistory { get; set; }

        public int GetOrCreateCount { get; private set; }

        public string? LastAppVersion { get; private set; }

        public string? LastMinSupportVersion { get; private set; }

        public Task EnsureTablesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<UpgradeVersionState> GetOrCreateAsync(string currentAppVersion, string minSupportVersion, CancellationToken cancellationToken = default)
        {
            GetOrCreateCount++;
            LastAppVersion = currentAppVersion;
            LastMinSupportVersion = minSupportVersion;
            return Task.FromResult(State);
        }

        public Task<UpgradeMigrationHistory?> GetLatestHistoryAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LatestHistory);
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

    /// <summary>
    /// 脚本提供者替身
    /// </summary>
    private sealed class FakeScriptProvider : IUpgradeScriptProvider
    {
        private readonly IReadOnlyList<UpgradeScript> _scripts;

        public FakeScriptProvider(IReadOnlyList<UpgradeScript> scripts)
        {
            _scripts = scripts;
        }

        public Task<IReadOnlyList<UpgradeScript>> GetScriptsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_scripts);
        }
    }
}
