// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Upgrade.Models;
using XiHan.Framework.Upgrade.Services;

namespace XiHan.Framework.Upgrade.Tests;

/// <summary>
/// 内存升级版本存储测试
/// </summary>
/// <remarks>
/// 该实现的状态字典是静态的（进程级共享），因此每个用例都用独占的租户标识建仓，
/// 避免用例之间互相污染；这也顺带覆盖了「按租户隔离版本状态与迁移历史」的契约。
/// </remarks>
public class InMemoryUpgradeVersionStoreTests
{
    private static long _tenantSeed = 900_000_000L;

    /// <summary>
    /// 首次获取会建仓，数据库版本从 0.0.0 起跑
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_WhenFirstCall_CreatesStateWithZeroDbVersion()
    {
        var tenantId = NextTenantId();
        var store = CreateStore(tenantId);

        var state = await store.GetOrCreateAsync("1.1.0", "1.0.0", TestContext.Current.CancellationToken);

        Assert.True(state.Id > 0);
        Assert.NotNull(state.TenantId);
        Assert.Equal(tenantId, state.TenantId!.Value);
        Assert.Equal("1.1.0", state.AppVersion);
        Assert.Equal("0.0.0", state.DbVersion);
        Assert.Equal("1.0.0", state.MinSupportVersion);
        Assert.False(state.IsUpgrading);
        Assert.Null(state.UpgradeNode);
        Assert.Null(state.UpgradeStartTime);
    }

    /// <summary>
    /// 再次获取沿用已建仓记录，不会被新传入的版本覆盖
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_WhenCalledAgain_KeepsRecordedVersions()
    {
        var store = CreateStore(NextTenantId());
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = await store.GetOrCreateAsync("1.0.0", "0.9.0", cancellationToken);

        var second = await store.GetOrCreateAsync("2.0.0", "1.5.0", cancellationToken);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("1.0.0", second.AppVersion);
        Assert.Equal("0.9.0", second.MinSupportVersion);
    }

    /// <summary>
    /// 返回的是快照副本，调用方原地改写不会污染存储
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_ReturnsDetachedSnapshot()
    {
        var store = CreateStore(NextTenantId());
        var cancellationToken = TestContext.Current.CancellationToken;
        var state = await store.GetOrCreateAsync("1.0.0", "0.9.0", cancellationToken);

        state.DbVersion = "9.9.9";
        state.IsUpgrading = true;
        var reread = await store.GetOrCreateAsync("1.0.0", "0.9.0", cancellationToken);

        Assert.NotSame(state, reread);
        Assert.Equal("0.0.0", reread.DbVersion);
        Assert.False(reread.IsUpgrading);
    }

    /// <summary>
    /// 空白版本值被规范化为 0.0.0，两侧空格被裁掉
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_NormalizesBlankAndPaddedVersions()
    {
        var store = CreateStore(NextTenantId());

        var state = await store.GetOrCreateAsync("  1.2.3  ", "   ", TestContext.Current.CancellationToken);

        Assert.Equal("1.2.3", state.AppVersion);
        Assert.Equal("0.0.0", state.MinSupportVersion);
    }

    /// <summary>
    /// 不同租户的版本状态互相隔离
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_IsolatesStatePerTenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var firstTenantId = NextTenantId();
        var secondTenantId = NextTenantId();
        var firstStore = CreateStore(firstTenantId);
        var secondStore = CreateStore(secondTenantId);

        var firstState = await firstStore.GetOrCreateAsync("1.0.0", "0.9.0", cancellationToken);
        await firstStore.UpdateDbVersionAsync(firstState, "2.0.0", cancellationToken);
        var secondState = await secondStore.GetOrCreateAsync("1.0.0", "0.9.0", cancellationToken);

        Assert.Equal("2.0.0", (await firstStore.GetOrCreateAsync("1.0.0", "0.9.0", cancellationToken)).DbVersion);
        Assert.Equal("0.0.0", secondState.DbVersion);
        Assert.Equal(secondTenantId, secondState.TenantId!.Value);
    }

    /// <summary>
    /// 设置升级中会同时写回存储与调用方传入的状态实例
    /// </summary>
    [Fact]
    public async Task SetUpgradingAsync_MarksUpgradingAndWritesBack()
    {
        var store = CreateStore(NextTenantId());
        var cancellationToken = TestContext.Current.CancellationToken;
        var state = await store.GetOrCreateAsync("1.0.0", "0.9.0", cancellationToken);
        var startTime = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

        await store.SetUpgradingAsync(state, "node-a", startTime, cancellationToken);

        Assert.True(state.IsUpgrading);
        Assert.Equal("node-a", state.UpgradeNode);
        Assert.Equal(startTime, state.UpgradeStartTime);

        var reread = await store.GetOrCreateAsync("1.0.0", "0.9.0", cancellationToken);
        Assert.True(reread.IsUpgrading);
        Assert.Equal("node-a", reread.UpgradeNode);
        Assert.Equal(startTime, reread.UpgradeStartTime);
    }

    /// <summary>
    /// 设置升级完成会清掉升级中标记并落地新的应用/数据库版本
    /// </summary>
    [Fact]
    public async Task SetUpgradeCompletedAsync_ClearsUpgradingAndPersistsVersions()
    {
        var store = CreateStore(NextTenantId());
        var cancellationToken = TestContext.Current.CancellationToken;
        var state = await store.GetOrCreateAsync("1.0.0", "0.9.0", cancellationToken);
        await store.SetUpgradingAsync(state, "node-a", DateTimeOffset.UtcNow, cancellationToken);

        await store.SetUpgradeCompletedAsync(state, " 1.1.0 ", " 1.1.0 ", cancellationToken);

        var reread = await store.GetOrCreateAsync("1.0.0", "0.9.0", cancellationToken);
        Assert.False(reread.IsUpgrading);
        Assert.Equal("1.1.0", reread.AppVersion);
        Assert.Equal("1.1.0", reread.DbVersion);
    }

    /// <summary>
    /// 设置升级失败只清升级中标记，保留节点与开始时间用于排查
    /// </summary>
    [Fact]
    public async Task SetUpgradeFailedAsync_ClearsUpgradingButKeepsDiagnosticFields()
    {
        var store = CreateStore(NextTenantId());
        var cancellationToken = TestContext.Current.CancellationToken;
        var state = await store.GetOrCreateAsync("1.0.0", "0.9.0", cancellationToken);
        var startTime = DateTimeOffset.UtcNow;
        await store.SetUpgradingAsync(state, "node-a", startTime, cancellationToken);

        await store.SetUpgradeFailedAsync(state, cancellationToken);

        var reread = await store.GetOrCreateAsync("1.0.0", "0.9.0", cancellationToken);
        Assert.False(reread.IsUpgrading);
        Assert.Equal("node-a", reread.UpgradeNode);
        Assert.Equal(startTime, reread.UpgradeStartTime);
    }

    /// <summary>
    /// 更新数据库版本会规范化取值并持久化
    /// </summary>
    [Fact]
    public async Task UpdateDbVersionAsync_PersistsNormalizedValue()
    {
        var store = CreateStore(NextTenantId());
        var cancellationToken = TestContext.Current.CancellationToken;
        var state = await store.GetOrCreateAsync("1.0.0", "0.9.0", cancellationToken);

        await store.UpdateDbVersionAsync(state, "  1.2.0  ", cancellationToken);

        Assert.Equal("1.2.0", state.DbVersion);
        Assert.Equal("1.2.0", (await store.GetOrCreateAsync("1.0.0", "0.9.0", cancellationToken)).DbVersion);
    }

    /// <summary>
    /// 传入空的版本状态时抛空引用参数异常
    /// </summary>
    [Fact]
    public async Task WriteMethods_WhenVersionNull_ThrowArgumentNull()
    {
        var store = CreateStore(NextTenantId());
        var cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await store.SetUpgradingAsync(null!, "node-a", DateTimeOffset.UtcNow, cancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await store.SetUpgradeCompletedAsync(null!, "1.0.0", "1.0.0", cancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await store.SetUpgradeFailedAsync(null!, cancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await store.UpdateDbVersionAsync(null!, "1.0.0", cancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await store.AddMigrationHistoryAsync(null!, cancellationToken));
    }

    /// <summary>
    /// 没有迁移历史时返回 null
    /// </summary>
    [Fact]
    public async Task GetLatestHistoryAsync_WhenNoHistory_ReturnsNull()
    {
        var store = CreateStore(NextTenantId());

        var history = await store.GetLatestHistoryAsync(TestContext.Current.CancellationToken);

        Assert.Null(history);
    }

    /// <summary>
    /// 最新历史按执行时间取，而不是按写入顺序
    /// </summary>
    [Fact]
    public async Task GetLatestHistoryAsync_ReturnsMostRecentByExecutedTime()
    {
        var tenantId = NextTenantId();
        var store = CreateStore(tenantId);
        var cancellationToken = TestContext.Current.CancellationToken;
        var now = DateTimeOffset.UtcNow;

        await store.AddMigrationHistoryAsync(NewHistory(tenantId, "1.1.0", "new.sql", now, false), cancellationToken);
        await store.AddMigrationHistoryAsync(NewHistory(tenantId, "1.0.0", "old.sql", now.AddHours(-1), true), cancellationToken);

        var latest = await store.GetLatestHistoryAsync(cancellationToken);

        Assert.NotNull(latest);
        Assert.Equal("new.sql", latest!.ScriptName);
        Assert.False(latest.Success);
    }

    /// <summary>
    /// 写入的是历史快照，写入后再改原对象不影响存储
    /// </summary>
    [Fact]
    public async Task AddMigrationHistoryAsync_StoresSnapshotNotReference()
    {
        var tenantId = NextTenantId();
        var store = CreateStore(tenantId);
        var cancellationToken = TestContext.Current.CancellationToken;
        var history = NewHistory(tenantId, "1.0.0", "01_init.sql", DateTimeOffset.UtcNow, true);

        await store.AddMigrationHistoryAsync(history, cancellationToken);
        history.ScriptName = "tampered.sql";
        history.Success = false;

        Assert.True(await store.HasMigrationHistoryAsync("1.0.0", "01_init.sql", cancellationToken));
        var latest = await store.GetLatestHistoryAsync(cancellationToken);
        Assert.NotNull(latest);
        Assert.Equal("01_init.sql", latest!.ScriptName);
    }

    /// <summary>
    /// 已成功执行过的脚本被判定为已执行，比较忽略大小写
    /// </summary>
    [Fact]
    public async Task HasMigrationHistoryAsync_WhenSucceeded_ReturnsTrueIgnoringCase()
    {
        var tenantId = NextTenantId();
        var store = CreateStore(tenantId);
        var cancellationToken = TestContext.Current.CancellationToken;
        await store.AddMigrationHistoryAsync(NewHistory(tenantId, "1.0.0", "01_Init.sql", DateTimeOffset.UtcNow, true), cancellationToken);

        Assert.True(await store.HasMigrationHistoryAsync("1.0.0", "01_init.SQL", cancellationToken));
        Assert.False(await store.HasMigrationHistoryAsync("1.1.0", "01_Init.sql", cancellationToken));
        Assert.False(await store.HasMigrationHistoryAsync("1.0.0", "02_other.sql", cancellationToken));
    }

    /// <summary>
    /// 只落了失败记录的脚本不算已执行，重跑升级时必须再执行一次
    /// </summary>
    [Fact]
    public async Task HasMigrationHistoryAsync_WhenOnlyFailedRecord_ReturnsFalse()
    {
        var tenantId = NextTenantId();
        var store = CreateStore(tenantId);
        var cancellationToken = TestContext.Current.CancellationToken;
        await store.AddMigrationHistoryAsync(NewHistory(tenantId, "1.0.0", "01_init.sql", DateTimeOffset.UtcNow, false), cancellationToken);

        Assert.False(await store.HasMigrationHistoryAsync("1.0.0", "01_init.sql", cancellationToken));
    }

    /// <summary>
    /// 版本或脚本名为空白时直接判定为未执行
    /// </summary>
    /// <param name="version">版本</param>
    /// <param name="scriptName">脚本名</param>
    [Theory]
    [InlineData("", "a.sql")]
    [InlineData("   ", "a.sql")]
    [InlineData("1.0.0", "")]
    [InlineData("1.0.0", "   ")]
    public async Task HasMigrationHistoryAsync_WhenArgumentsBlank_ReturnsFalse(string version, string scriptName)
    {
        var store = CreateStore(NextTenantId());

        Assert.False(await store.HasMigrationHistoryAsync(version, scriptName, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 迁移历史按租户隔离，别的租户的记录不算数
    /// </summary>
    [Fact]
    public async Task HasMigrationHistoryAsync_IsolatesHistoryPerTenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var firstTenantId = NextTenantId();
        var secondTenantId = NextTenantId();
        var firstStore = CreateStore(firstTenantId);
        var secondStore = CreateStore(secondTenantId);
        await firstStore.AddMigrationHistoryAsync(NewHistory(firstTenantId, "1.0.0", "a.sql", DateTimeOffset.UtcNow, true), cancellationToken);

        Assert.True(await firstStore.HasMigrationHistoryAsync("1.0.0", "a.sql", cancellationToken));
        Assert.False(await secondStore.HasMigrationHistoryAsync("1.0.0", "a.sql", cancellationToken));
        Assert.Null(await secondStore.GetLatestHistoryAsync(cancellationToken));
    }

    /// <summary>
    /// 内存实现无需建表，调用直接完成
    /// </summary>
    [Fact]
    public async Task EnsureTablesAsync_Completes()
    {
        var store = CreateStore(NextTenantId());

        await store.EnsureTablesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 取消令牌已取消时各读写方法立即抛出
    /// </summary>
    [Fact]
    public async Task Methods_WhenTokenCancelled_Throw()
    {
        var store = CreateStore(NextTenantId());
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var cancelled = cancellationTokenSource.Token;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await store.EnsureTablesAsync(cancelled));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await store.GetOrCreateAsync("1.0.0", "0.9.0", cancelled));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await store.GetLatestHistoryAsync(cancelled));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await store.HasMigrationHistoryAsync("1.0.0", "a.sql", cancelled));
    }

    /// <summary>
    /// 取下一个独占租户标识
    /// </summary>
    /// <returns>租户标识</returns>
    private static long NextTenantId()
    {
        return Interlocked.Increment(ref _tenantSeed);
    }

    /// <summary>
    /// 创建绑定到指定租户的版本存储
    /// </summary>
    /// <param name="tenantId">租户标识</param>
    /// <returns>版本存储</returns>
    private static InMemoryUpgradeVersionStore CreateStore(long tenantId)
    {
        return new InMemoryUpgradeVersionStore(new FakeCurrentTenant(tenantId, $"tenant-{tenantId}"));
    }

    /// <summary>
    /// 构造迁移历史
    /// </summary>
    /// <param name="tenantId">租户标识</param>
    /// <param name="version">版本</param>
    /// <param name="scriptName">脚本名</param>
    /// <param name="executedTime">执行时间</param>
    /// <param name="success">是否成功</param>
    /// <returns>迁移历史</returns>
    private static UpgradeMigrationHistory NewHistory(long tenantId, string version, string scriptName, DateTimeOffset executedTime, bool success)
    {
        return new UpgradeMigrationHistory
        {
            TenantId = tenantId,
            Version = version,
            ScriptName = scriptName,
            ExecutedTime = executedTime,
            Success = success,
            NodeName = "node-a"
        };
    }
}
