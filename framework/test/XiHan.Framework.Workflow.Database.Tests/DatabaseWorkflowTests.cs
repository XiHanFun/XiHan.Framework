// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Database.Entities;
using XiHan.Framework.Workflow.Database.Locking;
using XiHan.Framework.Workflow.Database.Polling;
using XiHan.Framework.Workflow.Database.Storage;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 数据库实现自己的行为
/// </summary>
/// <remarks>
/// 租约锁的接管与超时、选项读取、特性清单与领导权轮询、SQLite 上的计数与分页过滤、批量分批、长 JSON 往返、宿主停止后的不可达
/// </remarks>
public sealed class DatabaseWorkflowTests
{
    [Fact]
    public async Task Lock_TakesOverExpiredLease_AndWaitsForLiveOne()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync([new($"{WorkflowOptions.ConfigurationSection}:LockTimeoutSeconds", "2")]);
        Assert.IsType<DatabaseWorkflowInstanceLock>(host.Services.GetRequiredService<IWorkflowInstanceLock>());
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var instanceLock = CreateLock(host, clock);
        var now = clock.GetUtcNow();

        // 死节点留下的过期行立即被接管；还有 300ms 到期的行等到期后接管；远未到期的行等到超时
        await SeedLockAsync(host, "inst-dead", "dead-node:1", now.AddSeconds(-5));
        await SeedLockAsync(host, "inst-soon", "dead-node:2", now.AddMilliseconds(300));
        await SeedLockAsync(host, "inst-live", "live-node:3", now.AddMinutes(5));
        var dead = await instanceLock.AcquireAsync("inst-dead", CancellationToken.None);
        Assert.True(dead.IsSuccess);
        var holder = await HolderAsync(host, "inst-dead");
        Assert.StartsWith(host.Services.GetRequiredService<DatabaseWorkflowOptions>().HolderId, holder, StringComparison.Ordinal);

        // 时钟不动，行就不会到期：重试几轮也拿不到；推进过到期时刻才接管
        var soon = instanceLock.AcquireAsync("inst-soon", CancellationToken.None).AsTask();
        await Task.Delay(200);
        Assert.False(soon.IsCompleted, "The lock must wait for the live lease to expire.");
        clock.Advance(TimeSpan.FromMilliseconds(301));
        Assert.True((await soon.WaitAsync(TimeSpan.FromSeconds(10))).IsSuccess);

        // 等待上限按时钟计：推进过上限即超时，别人的行不动
        var live = instanceLock.AcquireAsync("inst-live", CancellationToken.None).AsTask();
        clock.Advance(TimeSpan.FromSeconds(2.001));
        var blocked = await live.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(WorkflowErrors.LockTimeout, blocked.Error?.Code);
        Assert.Equal("live-node:3", await HolderAsync(host, "inst-live"));

        // 释放删自己的行；重复释放无害；别人的行不动
        await dead.ValueOrThrow().DisposeAsync();
        await dead.ValueOrThrow().DisposeAsync();
        Assert.Null(await HolderAsync(host, "inst-dead"));
        await (await soon).ValueOrThrow().DisposeAsync();
        Assert.Null(await HolderAsync(host, "inst-soon"));
        Assert.Equal("live-node:3", await HolderAsync(host, "inst-live"));
    }

    [Fact]
    public async Task Lock_RenewsWhileHeld_SoShortLeasesStayExclusive()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync(
        [
            new($"{DatabaseWorkflowOptions.ConfigurationSection}:LockLeaseSeconds", "0.3"),
            new($"{WorkflowOptions.ConfigurationSection}:LockTimeoutSeconds", "0.8"),
        ]);
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var instanceLock = CreateLock(host, clock);
        var step = TimeSpan.FromMilliseconds(50);

        var first = (await instanceLock.AcquireAsync("inst-renew", CancellationToken.None)).ValueOrThrow();
        var second = instanceLock.AcquireAsync("inst-renew", CancellationToken.None).AsTask();
        // 推进时间超过租约时长与等待上限：每一步之前等持有者的续约把到期时刻推到这一步之外，第二个获取者始终拿不到
        while (!second.IsCompleted)
        {
            await WaitUntilAsync(async () => second.IsCompleted || await ExpiresAtAsync(host, "inst-renew") - clock.GetUtcNow() > step);
            clock.Advance(step);
            await Task.Yield();
        }

        var outcome = await second;
        if (outcome.TryGetValue(out var leaked))
        {
            await leaked.DisposeAsync();
        }

        Assert.Equal(WorkflowErrors.LockTimeout, outcome.Error?.Code);
        await first.DisposeAsync();
        var third = await instanceLock.AcquireAsync("inst-renew", CancellationToken.None);
        Assert.True(third.IsSuccess);
        await third.ValueOrThrow().DisposeAsync();
    }

    [Fact]
    public void Options_ReadFromConfiguration_AndValidate()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{DatabaseWorkflowOptions.ConfigurationSection}:PollIntervalSeconds"] = "0.5",
            [$"{DatabaseWorkflowOptions.ConfigurationSection}:BatchSize"] = "9",
            [$"{DatabaseWorkflowOptions.ConfigurationSection}:LockLeaseSeconds"] = "30",
            [$"{DatabaseWorkflowOptions.ConfigurationSection}:HolderId"] = "node-7",
            [$"{DatabaseWorkflowOptions.ConfigurationSection}:HealthProbeIntervalSeconds"] = "20",
            [$"{DatabaseWorkflowOptions.ConfigurationSection}:LockConnection"] = "leases",
        }).Build();

        var options = DatabaseWorkflowOptions.FromConfiguration(configuration);
        Assert.Equal((TimeSpan.FromMilliseconds(500), 9, TimeSpan.FromSeconds(30), "node-7", TimeSpan.FromSeconds(20), "leases"), (options.PollInterval, options.BatchSize, options.LockLease, options.HolderId, options.HealthProbeInterval, options.LockConnection));
        // 租约表的连接空白视为未配置，与 HolderId 一致
        Assert.Null(DatabaseWorkflowOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [$"{DatabaseWorkflowOptions.ConfigurationSection}:LockConnection"] = " " }).Build()).LockConnection);

        var defaults = new DatabaseWorkflowOptions();
        Assert.Equal((TimeSpan.FromSeconds(1), 100, TimeSpan.FromSeconds(60)), (defaults.PollInterval, defaults.BatchSize, defaults.LockLease));
        Assert.StartsWith($"{Environment.MachineName}:{Environment.ProcessId}:", defaults.HolderId, StringComparison.Ordinal);
        Assert.Equal(8, defaults.HolderId.Split(':')[^1].Length);
        Assert.Throws<ArgumentOutOfRangeException>(static () => new DatabaseWorkflowOptions { PollInterval = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new DatabaseWorkflowOptions { BatchSize = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new DatabaseWorkflowOptions { LockLease = TimeSpan.Zero });
        Assert.Throws<ArgumentException>(static () => new DatabaseWorkflowOptions { HolderId = " " });
        Assert.Null(defaults.LockConnection);
        Assert.Throws<ArgumentException>(static () => new DatabaseWorkflowOptions { LockConnection = " " });
    }

    [Fact]
    public async Task LockConnection_PutsTheLeaseTableInItsOwnDatabase()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        await using (var held = (await host.Services.GetRequiredService<IWorkflowInstanceLock>().AcquireAsync("instance-1", CancellationToken.None)).ValueOrThrow())
        {
            // 租约表只建在租约库上，持有中的租约行也在那里；业务库里没有这张表
            using var leases = new SqlSugar.SqlSugarClient(SqliteDatabase.Config("leases-read", SqliteDatabase.Connection(database.LeasePath)));
            using var main = new SqlSugar.SqlSugarClient(SqliteDatabase.Config("main-read", database.ConnectionString));
            Assert.Equal(["instance-1"], leases.Queryable<WorkflowLockRecord>().Select(static row => row.Id).ToList());
            Assert.False(main.DbMaintenance.IsAnyTable(main.EntityMaintenance.GetTableName<WorkflowLockRecord>(), false));
            Assert.True(main.DbMaintenance.IsAnyTable(main.EntityMaintenance.GetTableName<PlatformWorkflowInstanceRecord>(), false));
        }

        // 内存持久化没有连接：配置了租约表的连接即在装配时失败，不静默落在唯一的存储上
        var exception = await Assert.ThrowsAnyAsync<Exception>(static () => TestHost.StartAsync(static b => b.UseFeature<InMemoryPersistenceFeature>().UseFeature<DatabaseWorkflowFeature>(),
            [new($"{DatabaseWorkflowOptions.ConfigurationSection}:LockConnection", "leases")]));
        var codes = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is FrameworkException framework)
            {
                codes.Add(framework.Code);
            }
        }

        Assert.Contains(PersistenceErrors.NotSupported, codes);
    }

    [Theory]
    [InlineData("PollIntervalSeconds", "soon")]
    [InlineData("PollIntervalSeconds", "0")]
    [InlineData("BatchSize", "ten")]
    [InlineData("BatchSize", "0")]
    [InlineData("LockLeaseSeconds", "-30")]
    [InlineData("HealthProbeIntervalSeconds", "Infinity")]
    public void Options_InvalidValues_Throw_InsteadOfFallingBackToDefaults(string key, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [$"{DatabaseWorkflowOptions.ConfigurationSection}:{key}"] = value }).Build();

        var thrown = Assert.ThrowsAny<ArgumentException>(() => DatabaseWorkflowOptions.FromConfiguration(configuration));
        Assert.Contains($"{DatabaseWorkflowOptions.ConfigurationSection}:{key}", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Feature_IsExplicitProvider_AndOnlyTheLeaderPolls()
    {
        var feature = new DatabaseWorkflowFeature();
        Assert.Equal(("xihan.workflow.database", ManifestRole.Provider, ManifestKind.Provider), (feature.Manifest.Id, feature.Manifest.Role, feature.Manifest.Kind));
        Assert.Equal([WorkflowCapabilities.Execution], feature.Manifest.Provides.Select(static capability => capability.Id));
        Assert.Contains(feature.Manifest.Requires, static requirement => requirement.Id == "persistence.relational");
        Assert.Equal(["workflow.database.poller"], feature.Manifest.Contributes.Health);

        // 对任何 persistence.relational@1 都能装配：这里用内存持久化
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryPersistenceFeature>().UseFeature<DatabaseWorkflowFeature>(),
        [
            new($"{DatabaseWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "0.05"),
            new($"{DatabaseWorkflowOptions.ConfigurationSection}:LockLeaseSeconds", "1"),
        ]);
        Assert.Equal(["workflow.execution"], host.App.Graph["xihan.workflow.database"].SelectedCapabilities);
        Assert.Contains(feature.Manifest.Requires, static requirement => requirement.Id == "multitenancy.context" && requirement.Optional);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            // 存储与事务关系按作用域：在调用方的工作单元里读写
            Assert.Same(scope.ServiceProvider.GetRequiredService<DatabaseWorkflowDefinitionStore>(), scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionStore>());
            Assert.Same(scope.ServiceProvider.GetRequiredService<DatabaseWorkflowInstanceStore>(), scope.ServiceProvider.GetRequiredService<IWorkflowInstanceStore>());
            Assert.Same(scope.ServiceProvider.GetRequiredService<DatabaseWorkflowBookmarkStore>(), scope.ServiceProvider.GetRequiredService<IWorkflowBookmarkStore>());
            Assert.Same(scope.ServiceProvider.GetRequiredService<DatabaseWorkflowTransaction>(), scope.ServiceProvider.GetRequiredService<IWorkflowTransaction>());
            Assert.False(scope.ServiceProvider.GetRequiredService<IWorkflowTransaction>().IsActive);
        }

        var loop = host.Services.GetRequiredService<DueBookmarkPollingLoop>();
        Assert.True(loop.IsRunning);
        await WaitUntilAsync(() => ValueTask.FromResult(loop.IsLeader));
        Assert.Equal(host.Services.GetRequiredService<DatabaseWorkflowOptions>().HolderId, (await HolderAsync(host, PollerLeadership.Resource))!.Split(':')[..^1].Aggregate(static (a, b) => $"{a}:{b}"));

        // 第二个节点同一张表拿不到领导权；本节点停止轮询并释放后才拿得到
        var other = new PollerLeadership(host.Services, new DatabaseWorkflowOptions { HolderId = "node-2", LockLease = TimeSpan.FromSeconds(1) }, TimeProvider.System, host.Services.GetRequiredService<ILoggerFactory>());
        Assert.False(await other.TryHoldAsync(CancellationToken.None));

        // 到期的延时由持有领导权的后台轮询恢复，不用手工 Poll
        var definition = WorkflowDefinitionBuilder.Create("poll-delay")
            .AddStart().AddDelay("wait", TimeSpan.FromMilliseconds(20)).AddEnd()
            .AddTransition("start", "wait").AddTransition("wait", "end")
            .Build();
        string instanceId;
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionCatalog>();
            var created = (await catalog.CreateAsync(definition, CancellationToken.None)).ValueOrThrow();
            (await catalog.PublishAsync(created.Id, CancellationToken.None)).ValueOrThrow();
            instanceId = (await scope.ServiceProvider.GetRequiredService<IWorkflowEngine>().StartAsync(new WorkflowStartRequest { Name = "poll-delay" }, CancellationToken.None)).ValueOrThrow().Id;
        }

        await WaitUntilAsync(async () => await Partitions.InAsync(host, null, async s => (await s.Instances.FindAsync(null, instanceId, CancellationToken.None)).ValueOrThrow().ValueOrThrow().Status == WorkflowInstanceStatus.Completed));

        await loop.StopAsync(CancellationToken.None);
        Assert.False(loop.IsRunning);
        Assert.False(loop.IsLeader);
        Assert.Null(await HolderAsync(host, PollerLeadership.Resource));
        Assert.True(await other.TryHoldAsync(CancellationToken.None));
        Assert.True(other.IsLeader);
        await other.ReleaseAsync(CancellationToken.None);
        Assert.Null(await HolderAsync(host, PollerLeadership.Resource));
    }

    [Fact]
    public async Task Instances_CountAndPagedFilters_OnSqlite()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        var now = DateTimeOffset.UtcNow;
        var instances = new List<WorkflowInstance>();
        for (var index = 0; index < 25; index++)
        {
            var instance = new WorkflowInstance
            {
                Id = WorkflowIdentifiers.NewId(),
                DefinitionId = "def",
                DefinitionName = new WorkflowName(index % 2 == 0 ? "even" : "odd"),
                Status = index % 5 == 0 ? WorkflowInstanceStatus.Completed : index % 5 == 1 ? WorkflowInstanceStatus.Faulted : WorkflowInstanceStatus.Running,
                StarterId = $"user-{index % 3}",
                CreatedAt = now.AddMinutes(-index),
                EndedAt = index % 5 == 0 ? now.AddMinutes(-index).AddSeconds(30) : null,
            };
            instances.Add(instance);
        }

        await Partitions.InAsync(host, null, async s => Assert.True((await s.Instances.SaveAsync(null, instances, CancellationToken.None)).IsSuccess));
        await Partitions.InAsync(host, null, async s =>
        {
            var store = s.Instances;
            Assert.Equal(25, (await store.CountAsync(null, new WorkflowInstanceQuery(), CancellationToken.None)).ValueOrThrow());
            Assert.Equal(10, (await store.CountAsync(null, new WorkflowInstanceQuery { Statuses = [WorkflowInstanceStatus.Completed, WorkflowInstanceStatus.Faulted] }, CancellationToken.None)).ValueOrThrow());
            Assert.Equal(10, (await store.CountAsync(null, new WorkflowInstanceQuery { Status = WorkflowInstanceStatus.Completed, Statuses = [WorkflowInstanceStatus.Faulted], Skip = 100, Limit = 1 }, CancellationToken.None)).ValueOrThrow());
            Assert.Equal(13, (await store.CountAsync(null, new WorkflowInstanceQuery { DefinitionName = new WorkflowName("even") }, CancellationToken.None)).ValueOrThrow());
            Assert.Equal(9, (await store.CountAsync(null, new WorkflowInstanceQuery { StarterId = "user-0" }, CancellationToken.None)).ValueOrThrow());
            // 创建时间在 [now-10min, now-5min) 的是 index 6..10；已结束且结束早于 now-9min 的是 index 10、15、20
            Assert.Equal(5, (await store.CountAsync(null, new WorkflowInstanceQuery { CreatedFrom = now.AddMinutes(-10), CreatedTo = now.AddMinutes(-5) }, CancellationToken.None)).ValueOrThrow());
            Assert.Equal(3, (await store.CountAsync(null, new WorkflowInstanceQuery { EndedBefore = now.AddMinutes(-9) }, CancellationToken.None)).ValueOrThrow());

            // 分页按创建时间降序：第 2 页（每页 10）是 index 10..19
            var page = (await store.ListAsync(null, new WorkflowInstanceQuery { Skip = 10, Limit = 10 }, CancellationToken.None)).ValueOrThrow();
            Assert.Equal(instances.Skip(10).Take(10).Select(static instance => instance.Id), page.Select(static instance => instance.Id));
            var running = (await store.ListAsync(null, new WorkflowInstanceQuery { Status = WorkflowInstanceStatus.Running, Skip = 12, Limit = 10 }, CancellationToken.None)).ValueOrThrow();
            Assert.Equal(instances.Where(static instance => instance.Status == WorkflowInstanceStatus.Running).Skip(12).Select(static instance => instance.Id), running.Select(static instance => instance.Id));
            Assert.Empty((await store.ListAsync(null, new WorkflowInstanceQuery { Skip = 25 }, CancellationToken.None)).ValueOrThrow());
        });
    }

    [Fact]
    public async Task Batches_BeyondThePageSize_AreSplit_AndCascade()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        var instances = Enumerable.Range(0, 1005).Select(static index => new WorkflowInstance { Id = WorkflowIdentifiers.NewId(), DefinitionId = "def", DefinitionName = new WorkflowName("bulk"), CreatedAt = DateTimeOffset.UtcNow }).ToList();
        var ids = instances.Select(static instance => instance.Id).ToList();

        await Partitions.InAsync(host, null, async s =>
        {
            var store = s.Instances;
            var bookmarks = s.Bookmarks;
            Assert.True((await store.SaveAsync(null, instances, CancellationToken.None)).IsSuccess);
            Assert.True((await store.SaveNodeInstancesAsync(null, instances.Select(static instance => new WorkflowNodeInstance { Id = WorkflowIdentifiers.NewId(), InstanceId = instance.Id, NodeId = "n", ActivityType = ActivityTypes.Log, StartedAt = instance.CreatedAt }).ToList(), CancellationToken.None)).IsSuccess);
            Assert.True((await bookmarks.SaveAsync(null, instances.Select(static instance => new WorkflowBookmark { Id = WorkflowIdentifiers.NewId(), InstanceId = instance.Id, NodeId = "n", NodeInstanceId = "ni", Kind = BookmarkKinds.Signal, Key = "go", CreatedAt = instance.CreatedAt }).ToList(), CancellationToken.None)).IsSuccess);

            Assert.Equal(1005, (await store.FindManyAsync(null, [.. ids, "missing"], CancellationToken.None)).ValueOrThrow().Count);
            Assert.Equal(1005, (await store.CountAsync(null, new WorkflowInstanceQuery { DefinitionName = new WorkflowName("bulk") }, CancellationToken.None)).ValueOrThrow());
            Assert.Equal(1000, (await store.ListAsync(null, new WorkflowInstanceQuery { Limit = 1000 }, CancellationToken.None)).ValueOrThrow().Count);
            Assert.Equal(1005, (await bookmarks.BySignalAsync(null, "go", null, CancellationToken.None)).ValueOrThrow().Count);

            // 整体替换 1005 条：状态变了、条数不变
            foreach (var instance in instances)
            {
                instance.Status = WorkflowInstanceStatus.Completed;
            }

            Assert.True((await store.SaveAsync(null, instances, CancellationToken.None)).IsSuccess);
            Assert.Equal(1005, (await store.CountAsync(null, new WorkflowInstanceQuery { Status = WorkflowInstanceStatus.Completed }, CancellationToken.None)).ValueOrThrow());

            Assert.True((await bookmarks.DeleteByInstanceAsync(null, ids, CancellationToken.None)).IsSuccess);
            Assert.Empty((await bookmarks.BySignalAsync(null, "go", null, CancellationToken.None)).ValueOrThrow());
            Assert.True((await store.DeleteAsync(null, ids, CancellationToken.None)).IsSuccess);
            Assert.Equal(0, (await store.CountAsync(null, new WorkflowInstanceQuery(), CancellationToken.None)).ValueOrThrow());
            Assert.Empty((await store.NodeInstancesAsync(null, ids[0], CancellationToken.None)).ValueOrThrow());
            Assert.Empty((await store.NodeInstancesAsync(null, ids[^1], CancellationToken.None)).ValueOrThrow());
        });
    }

    [Fact]
    public async Task Documents_RoundTripLargeJson_AndReportUnavailableAfterStop()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        var text = new string('x', 12_000) + "中文";
        var instance = new WorkflowInstance { Id = WorkflowIdentifiers.NewId(), DefinitionId = "def", DefinitionName = new WorkflowName("big"), CreatedAt = DateTimeOffset.UtcNow };
        instance.Variables["blob"] = text;
        instance.Variables["n"] = 1;
        var definition = new WorkflowDefinition
        {
            Id = WorkflowIdentifiers.NewId(),
            Name = new WorkflowName("big"),
            Nodes = [new WorkflowNode { Id = "start", ActivityType = ActivityTypes.Start }],
            Description = text,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await Partitions.InAsync(host, null, async s =>
        {
            Assert.True((await s.Instances.SaveAsync(null, [instance], CancellationToken.None)).IsSuccess);
            Assert.True((await s.Definitions.SaveAsync(null, [definition], CancellationToken.None)).IsSuccess);
        });
        await Partitions.InAsync(host, null, async s =>
        {
            var stored = (await s.Instances.FindAsync(null, instance.Id, CancellationToken.None)).ValueOrThrow().ValueOrThrow();
            Assert.Equal(text, stored.Variables["blob"]!.GetValue<string>());
            Assert.Equal(instance.CreatedAt, stored.CreatedAt);
            Assert.Equal(text, (await s.Definitions.FindAsync(null, definition.Id, CancellationToken.None)).ValueOrThrow().ValueOrThrow().Description);
        });

        // 宿主停止后管道不接受调用：引擎在调用之外使用时自己开调用，开不了即存储不可达；租约锁走自己的工作单元，不受应用状态影响
        await host.App.StopAsync(CancellationToken.None);
        await using var scope = host.Services.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();
        Assert.Equal(WorkflowErrors.StoreUnavailable, (await engine.FindInstanceAsync(instance.Id, CancellationToken.None)).Error?.Code);
        Assert.Equal(WorkflowErrors.StoreUnavailable, (await scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionCatalog>().CreateAsync(definition, CancellationToken.None)).Error?.Code);
    }

    /// <summary>
    /// 直接写一行租约，模拟别的节点留下的锁
    /// </summary>
    private static async Task SeedLockAsync(TestHost host, string id, string holder, DateTimeOffset expiresAt)
    {
        await InLeaseScopeAsync(host, async repository => (await repository.AddAsync(new WorkflowLockRecord { Id = id, Holder = holder, ExpiresAt = expiresAt }, CancellationToken.None)).ThrowIfFailure());
    }

    /// <summary>
    /// 按手动时钟构造的实例锁
    /// </summary>
    /// <remarks>
    /// 选项与宿主里登记的相同，只有时钟换掉
    /// </remarks>
    private static DatabaseWorkflowInstanceLock CreateLock(TestHost host, TimeProvider clock)
        => new(host.Services, host.Services.GetRequiredService<WorkflowOptions>(), host.Services.GetRequiredService<DatabaseWorkflowOptions>(), clock, host.Services.GetRequiredService<ILoggerFactory>());

    /// <summary>
    /// 读租约行的到期时刻
    /// </summary>
    private static async Task<DateTimeOffset> ExpiresAtAsync(TestHost host, string id)
    {
        DateTimeOffset expiresAt = default;
        await InLeaseScopeAsync(host, async repository => expiresAt = (await repository.FindAsync(id, CancellationToken.None)).ValueOrThrow().ExpiresAt);
        return expiresAt;
    }

    /// <summary>
    /// 读租约行的持有者
    /// </summary>
    /// <remarks>
    /// 没有行为 <see langword="null"/>
    /// </remarks>
    private static async Task<string?> HolderAsync(TestHost host, string id)
    {
        string? holder = null;
        await InLeaseScopeAsync(host, async repository => holder = (await repository.FindAsync(id, CancellationToken.None)).TryGetValue(out var row) ? row.Holder : null);
        return holder;
    }

    /// <summary>
    /// 与租约步骤一样，在调用之外的新作用域里开可写工作单元读写租约表，执行完提交
    /// </summary>
    private static async Task InLeaseScopeAsync(TestHost host, Func<IRepository<WorkflowLockRecord, string>, Task> action)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        // 绑定租约表的连接（未配置为缺省落点），与租约步骤一样只在租约库上开事务
        await unitOfWork.BeginAsync(new UnitOfWorkOptions { Connection = host.Services.GetRequiredService<DatabaseWorkflowOptions>().LockConnection }, CancellationToken.None);
        await action(scope.ServiceProvider.GetRequiredService<IRepository<WorkflowLockRecord, string>>());
        (await unitOfWork.CommitAsync(CancellationToken.None)).ThrowIfFailure();
    }

    /// <summary>
    /// 轮询直到条件成立，最多 10 秒
    /// </summary>
    private static async Task WaitUntilAsync(Func<ValueTask<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!await condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The condition did not become true in time.");
            }

            await Task.Delay(20);
        }
    }
}
