// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.InMemory;
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
/// 数据库实现自己的行为。
/// </summary>
/// <remarks>
/// 租约锁的接管与超时、选项读取、特性清单与领导权轮询、SQLite 上的计数与分页过滤、批量分批、长 JSON 往返、宿主停止后的不可达。
/// </remarks>
public sealed class DatabaseWorkflowTests
{
    [Fact]
    public async Task Lock_TakesOverExpiredLease_AndWaitsForLiveOne()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync([new($"{WorkflowOptions.ConfigurationSection}:LockTimeoutSeconds", "2")]);
        var instanceLock = host.Services.GetRequiredService<IWorkflowInstanceLock>();
        Assert.IsType<DatabaseWorkflowInstanceLock>(instanceLock);
        var now = DateTimeOffset.UtcNow;

        // 死节点留下的过期行立即被接管；还有 300ms 到期的行等到期后接管；远未到期的行等到超时
        await SeedLockAsync(host, "inst-dead", "dead-node:1", now.AddSeconds(-5));
        await SeedLockAsync(host, "inst-soon", "dead-node:2", now.AddMilliseconds(300));
        await SeedLockAsync(host, "inst-live", "live-node:3", now.AddMinutes(5));
        var dead = await instanceLock.AcquireAsync("inst-dead", CancellationToken.None);
        Assert.True(dead.IsSuccess);
        var holder = await HolderAsync(host, "inst-dead");
        Assert.StartsWith(host.Services.GetRequiredService<DatabaseWorkflowOptions>().HolderId, holder, StringComparison.Ordinal);

        var started = DateTimeOffset.UtcNow;
        var soon = await instanceLock.AcquireAsync("inst-soon", CancellationToken.None);
        Assert.True(soon.IsSuccess);
        Assert.True(DateTimeOffset.UtcNow - started >= TimeSpan.FromMilliseconds(200), "The lock must wait for the live lease to expire.");

        var blocked = await instanceLock.AcquireAsync("inst-live", CancellationToken.None);
        Assert.Equal(WorkflowErrors.LockTimeout, blocked.Error?.Code);
        Assert.Equal("live-node:3", await HolderAsync(host, "inst-live"));

        // 释放删自己的行；重复释放无害；别人的行不动
        await dead.ValueOrThrow().DisposeAsync();
        await dead.ValueOrThrow().DisposeAsync();
        Assert.Null(await HolderAsync(host, "inst-dead"));
        await soon.ValueOrThrow().DisposeAsync();
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
        var instanceLock = host.Services.GetRequiredService<IWorkflowInstanceLock>();

        var first = (await instanceLock.AcquireAsync("inst-renew", CancellationToken.None)).ValueOrThrow();
        // 等待超过租约时长：持有者在后台续约，第二个获取者仍拿不到
        var second = await instanceLock.AcquireAsync("inst-renew", CancellationToken.None);
        Assert.Equal(WorkflowErrors.LockTimeout, second.Error?.Code);
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
        }).Build();

        var options = DatabaseWorkflowOptions.FromConfiguration(configuration);
        Assert.Equal((TimeSpan.FromMilliseconds(500), 9, TimeSpan.FromSeconds(30), "node-7"), (options.PollInterval, options.BatchSize, options.LockLease, options.HolderId));

        var defaults = new DatabaseWorkflowOptions();
        Assert.Equal((TimeSpan.FromSeconds(1), 100, TimeSpan.FromSeconds(60)), (defaults.PollInterval, defaults.BatchSize, defaults.LockLease));
        Assert.StartsWith($"{Environment.MachineName}:{Environment.ProcessId}:", defaults.HolderId, StringComparison.Ordinal);
        Assert.Equal(8, defaults.HolderId.Split(':')[^1].Length);
        Assert.Throws<ArgumentOutOfRangeException>(static () => new DatabaseWorkflowOptions { PollInterval = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new DatabaseWorkflowOptions { BatchSize = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new DatabaseWorkflowOptions { LockLease = TimeSpan.Zero });
        Assert.Throws<ArgumentException>(static () => new DatabaseWorkflowOptions { HolderId = " " });
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
        Assert.Same(host.Services.GetRequiredService<DatabaseWorkflowDefinitionStore>(), host.Services.GetRequiredService<IWorkflowDefinitionStore>());
        Assert.Same(host.Services.GetRequiredService<DatabaseWorkflowInstanceStore>(), host.Services.GetRequiredService<IWorkflowInstanceStore>());
        Assert.Same(host.Services.GetRequiredService<DatabaseWorkflowBookmarkStore>(), host.Services.GetRequiredService<IWorkflowBookmarkStore>());
        var loop = host.Services.GetRequiredService<DueBookmarkPollingLoop>();
        Assert.True(loop.IsRunning);
        await WaitUntilAsync(() => ValueTask.FromResult(loop.IsLeader));
        Assert.Equal(host.Services.GetRequiredService<DatabaseWorkflowOptions>().HolderId, (await HolderAsync(host, PollerLeadership.Resource))!.Split(':')[..^1].Aggregate(static (a, b) => $"{a}:{b}"));

        // 第二个节点同一张表拿不到领导权；本节点停止轮询并释放后才拿得到
        var other = new PollerLeadership(host.Services, host.App, new DatabaseWorkflowOptions { HolderId = "node-2", LockLease = TimeSpan.FromSeconds(1) }, TimeProvider.System, host.Services.GetRequiredService<ILoggerFactory>());
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

        var store = host.Services.GetRequiredService<IWorkflowInstanceStore>();
        await WaitUntilAsync(async () => (await store.FindAsync(instanceId, CancellationToken.None)).ValueOrThrow().ValueOrThrow().Status == WorkflowInstanceStatus.Completed);

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
        var store = host.Services.GetRequiredService<IWorkflowInstanceStore>();
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
                TenantId = index < 10 ? "t1" : "t2",
                StarterId = $"user-{index % 3}",
                CreatedAt = now.AddMinutes(-index),
                EndedAt = index % 5 == 0 ? now.AddMinutes(-index).AddSeconds(30) : null,
            };
            instances.Add(instance);
        }

        Assert.True((await store.SaveAsync(instances, CancellationToken.None)).IsSuccess);
        Assert.Equal(25, (await store.CountAsync(new WorkflowInstanceQuery(), CancellationToken.None)).ValueOrThrow());
        Assert.Equal(10, (await store.CountAsync(new WorkflowInstanceQuery { Statuses = [WorkflowInstanceStatus.Completed, WorkflowInstanceStatus.Faulted] }, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(10, (await store.CountAsync(new WorkflowInstanceQuery { Status = WorkflowInstanceStatus.Completed, Statuses = [WorkflowInstanceStatus.Faulted], Skip = 100, Limit = 1 }, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(5, (await store.CountAsync(new WorkflowInstanceQuery { TenantId = "t1", DefinitionName = new WorkflowName("even") }, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(9, (await store.CountAsync(new WorkflowInstanceQuery { StarterId = "user-0" }, CancellationToken.None)).ValueOrThrow());
        // 创建时间在 [now-10min, now-5min) 的是 index 6..10；已结束且结束早于 now-9min 的是 index 10、15、20
        Assert.Equal(5, (await store.CountAsync(new WorkflowInstanceQuery { CreatedFrom = now.AddMinutes(-10), CreatedTo = now.AddMinutes(-5) }, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(3, (await store.CountAsync(new WorkflowInstanceQuery { EndedBefore = now.AddMinutes(-9) }, CancellationToken.None)).ValueOrThrow());

        // 分页按创建时间降序：第 2 页（每页 10）是 index 10..19
        var page = (await store.ListAsync(new WorkflowInstanceQuery { Skip = 10, Limit = 10 }, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(instances.Skip(10).Take(10).Select(static instance => instance.Id), page.Select(static instance => instance.Id));
        var running = (await store.ListAsync(new WorkflowInstanceQuery { Status = WorkflowInstanceStatus.Running, Skip = 12, Limit = 10 }, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(instances.Where(static instance => instance.Status == WorkflowInstanceStatus.Running).Skip(12).Select(static instance => instance.Id), running.Select(static instance => instance.Id));
        Assert.Empty((await store.ListAsync(new WorkflowInstanceQuery { Skip = 25 }, CancellationToken.None)).ValueOrThrow());
    }

    [Fact]
    public async Task Batches_BeyondThePageSize_AreSplit_AndCascade()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        var store = host.Services.GetRequiredService<IWorkflowInstanceStore>();
        var bookmarks = host.Services.GetRequiredService<IWorkflowBookmarkStore>();
        var instances = Enumerable.Range(0, 1005).Select(static index => new WorkflowInstance { Id = WorkflowIdentifiers.NewId(), DefinitionId = "def", DefinitionName = new WorkflowName("bulk"), CreatedAt = DateTimeOffset.UtcNow }).ToList();
        var ids = instances.Select(static instance => instance.Id).ToList();

        Assert.True((await store.SaveAsync(instances, CancellationToken.None)).IsSuccess);
        Assert.True((await store.SaveNodeInstancesAsync(instances.Select(static instance => new WorkflowNodeInstance { Id = WorkflowIdentifiers.NewId(), InstanceId = instance.Id, NodeId = "n", ActivityType = ActivityTypes.Log, StartedAt = instance.CreatedAt }).ToList(), CancellationToken.None)).IsSuccess);
        Assert.True((await bookmarks.SaveAsync(instances.Select(static instance => new WorkflowBookmark { Id = WorkflowIdentifiers.NewId(), InstanceId = instance.Id, NodeId = "n", NodeInstanceId = "ni", Kind = BookmarkKinds.Signal, Key = "go", CreatedAt = instance.CreatedAt }).ToList(), CancellationToken.None)).IsSuccess);

        Assert.Equal(1005, (await store.FindManyAsync([.. ids, "missing"], CancellationToken.None)).ValueOrThrow().Count);
        Assert.Equal(1005, (await store.CountAsync(new WorkflowInstanceQuery { DefinitionName = new WorkflowName("bulk") }, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(1000, (await store.ListAsync(new WorkflowInstanceQuery { Limit = 1000 }, CancellationToken.None)).ValueOrThrow().Count);
        Assert.Equal(1005, (await bookmarks.BySignalAsync("go", null, CancellationToken.None)).ValueOrThrow().Count);

        // 整体替换 1005 条：状态变了、条数不变
        foreach (var instance in instances)
        {
            instance.Status = WorkflowInstanceStatus.Completed;
        }

        Assert.True((await store.SaveAsync(instances, CancellationToken.None)).IsSuccess);
        Assert.Equal(1005, (await store.CountAsync(new WorkflowInstanceQuery { Status = WorkflowInstanceStatus.Completed }, CancellationToken.None)).ValueOrThrow());

        Assert.True((await bookmarks.DeleteByInstanceAsync(ids, CancellationToken.None)).IsSuccess);
        Assert.Empty((await bookmarks.BySignalAsync("go", null, CancellationToken.None)).ValueOrThrow());
        Assert.True((await store.DeleteAsync(ids, CancellationToken.None)).IsSuccess);
        Assert.Equal(0, (await store.CountAsync(new WorkflowInstanceQuery(), CancellationToken.None)).ValueOrThrow());
        Assert.Empty((await store.NodeInstancesAsync(ids[0], CancellationToken.None)).ValueOrThrow());
        Assert.Empty((await store.NodeInstancesAsync(ids[^1], CancellationToken.None)).ValueOrThrow());
    }

    [Fact]
    public async Task Documents_RoundTripLargeJson_AndReportUnavailableAfterStop()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        var store = host.Services.GetRequiredService<IWorkflowInstanceStore>();
        var definitions = host.Services.GetRequiredService<IWorkflowDefinitionStore>();
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

        Assert.True((await store.SaveAsync([instance], CancellationToken.None)).IsSuccess);
        Assert.True((await definitions.SaveAsync([definition], CancellationToken.None)).IsSuccess);
        var stored = (await store.FindAsync(instance.Id, CancellationToken.None)).ValueOrThrow().ValueOrThrow();
        Assert.Equal(text, stored.Variables["blob"]!.GetValue<string>());
        Assert.Equal(instance.CreatedAt, stored.CreatedAt);
        Assert.Equal(text, (await definitions.FindAsync(definition.Id, CancellationToken.None)).ValueOrThrow().ValueOrThrow().Description);

        await host.App.StopAsync(CancellationToken.None);
        var unavailable = await store.FindAsync(instance.Id, CancellationToken.None);
        Assert.Equal(WorkflowErrors.StoreUnavailable, unavailable.Error?.Code);
        Assert.Equal(WorkflowErrors.StoreUnavailable, (await definitions.SaveAsync([definition], CancellationToken.None)).Error?.Code);
        Assert.Equal(WorkflowErrors.StoreUnavailable, (await host.Services.GetRequiredService<IWorkflowInstanceLock>().AcquireAsync("x", CancellationToken.None)).Error?.Code);
    }

    /// <summary>
    /// 直接写一行租约，模拟别的节点留下的锁。
    /// </summary>
    private static async Task SeedLockAsync(TestHost host, string id, string holder, DateTimeOffset expiresAt)
    {
        var seeded = await host.InvokeAsync(async context =>
        {
            var repository = context.Services.GetRequiredService<IRepository<WorkflowLockRecord, string>>();
            (await repository.AddAsync(new WorkflowLockRecord { Id = id, Holder = holder, ExpiresAt = expiresAt }, context.CancellationToken)).ThrowIfFailure();
        });
        seeded.ThrowIfFailure();
    }

    /// <summary>
    /// 读租约行的持有者。
    /// </summary>
    /// <remarks>
    /// 没有行为 <see langword="null"/>。
    /// </remarks>
    private static async Task<string?> HolderAsync(TestHost host, string id)
    {
        string? holder = null;
        (await host.InvokeAsync(async context =>
        {
            var found = await context.Services.GetRequiredService<IRepository<WorkflowLockRecord, string>>().FindAsync(id, context.CancellationToken);
            holder = found.TryGetValue(out var row) ? row.Holder : null;
        })).ThrowIfFailure();
        return holder;
    }

    /// <summary>
    /// 轮询直到条件成立，最多 10 秒。
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
