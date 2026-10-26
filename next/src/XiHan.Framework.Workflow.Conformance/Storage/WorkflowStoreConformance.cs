// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using Xunit;

namespace XiHan.Framework.Workflow.Conformance.Storage;

/// <summary>
/// 存储与实例锁的一致性套件（RFC-0021 §存储契约）：查询、排序、批量、级联、快照语义与锁的互斥。Provider 继承并实现 <see cref="StartAsync"/>；锁等待上限应配置在 1 秒以内。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class WorkflowStoreConformance
{
    /// <summary>
    /// 启动装配了被测存储与锁的夹具。
    /// </summary>
    protected abstract Task<WorkflowStoreHost> StartAsync();

    /// <summary>
    /// 用例 1：定义按 ID、名字 + 版本、最新已发布查得到；最大版本与列表排序正确；删除后找不到；批量找忽略不存在的。
    /// </summary>
    [Fact]
    public async Task Definitions_SaveFindListAndDelete()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var v1 = Definition("conf-def", 1, WorkflowDefinitionStatus.Published);
        var v2 = Definition("conf-def", 2, WorkflowDefinitionStatus.Draft);
        var other = Definition("conf-other", 1, WorkflowDefinitionStatus.Published);
        Assert.True((await host.Definitions.SaveAsync([v1, v2, other], CancellationToken.None).ConfigureAwait(false)).IsSuccess);

        var found = (await host.Definitions.FindAsync(v2.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow();
        Assert.Equal((v2.Name, 2, WorkflowDefinitionStatus.Draft), (found.Name, found.Version, found.Status));
        Assert.Equal(1, (await host.Definitions.FindAsync(new WorkflowName("conf-def"), 1, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().Version);
        Assert.Equal(v1.Id, (await host.Definitions.FindLatestPublishedAsync("conf-def", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().Id);
        Assert.Equal(2, (await host.Definitions.MaxVersionAsync("conf-def", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal(0, (await host.Definitions.MaxVersionAsync("conf-none", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        Assert.False((await host.Definitions.FindAsync("missing", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);

        var listed = (await host.Definitions.ListAsync(null, null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal([(v2.Id, 2), (v1.Id, 1), (other.Id, 1)], listed.Select(static definition => (definition.Id, definition.Version)));
        Assert.Equal([v1.Id, other.Id], (await host.Definitions.ListAsync(null, WorkflowDefinitionStatus.Published, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static definition => definition.Id));
        Assert.Equal([v2.Id, v1.Id], (await host.Definitions.ListAsync(new WorkflowName("conf-def"), null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static definition => definition.Id));

        var many = (await host.Definitions.FindManyAsync([v1.Id, "missing", other.Id], CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(2, many.Count);

        Assert.True((await host.Definitions.DeleteAsync([v2.Id, "missing"], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.False((await host.Definitions.FindAsync(v2.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
        Assert.Equal(1, (await host.Definitions.MaxVersionAsync("conf-def", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
    }

    /// <summary>
    /// 用例 2：保存后再改原对象不影响存下的；返回的对象再改也不影响（快照语义）。
    /// </summary>
    [Fact]
    public async Task Stores_KeepSnapshots_NotReferences()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var instance = Instance("conf-snap", "inst-snap");
        instance.Variables["x"] = 1;
        Assert.True((await host.Instances.SaveAsync([instance], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        instance.Variables["x"] = 2;
        instance.Status = WorkflowInstanceStatus.Completed;

        var stored = (await host.Instances.FindAsync("inst-snap", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow();
        Assert.Equal(1, stored.Variables["x"]!.GetValue<int>());
        Assert.Equal(WorkflowInstanceStatus.Running, stored.Status);
        stored.Variables["x"] = 3;
        Assert.Equal(1, (await host.Instances.FindAsync("inst-snap", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().Variables["x"]!.GetValue<int>());

        var bookmark = Bookmark("inst-snap", "node-inst", BookmarkKinds.Signal, "sig", null);
        bookmark.Payload["p"] = "a";
        Assert.True((await host.Bookmarks.SaveAsync([bookmark], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        bookmark.Payload["p"] = "b";
        Assert.Equal("a", (await host.Bookmarks.FindAsync(bookmark.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().Payload["p"]!.GetValue<string>());
    }

    /// <summary>
    /// 用例 3：实例列表按创建时间降序、过滤项与上限生效；节点实例按开始时间升序、同刻按写入顺序；删实例级联节点实例。
    /// </summary>
    [Fact]
    public async Task Instances_ListFiltersAndOrders_NodeInstancesOrdered_DeleteCascades()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var a = Instance("conf-list", "inst-a", now.AddMinutes(-2));
        var b = Instance("conf-list", "inst-b", now.AddMinutes(-1));
        b.Status = WorkflowInstanceStatus.Completed;
        b.CorrelationId = "doc-1";
        b.EndedAt = now.AddSeconds(-30);
        var c = Instance("conf-list-2", "inst-c", now);
        c.ParentInstanceId = "inst-a";
        c.TenantId = "t1";
        c.StarterId = "starter-c";
        Assert.True((await host.Instances.SaveAsync([a, b, c], CancellationToken.None).ConfigureAwait(false)).IsSuccess);

        Assert.Equal(["inst-c", "inst-b", "inst-a"], (await host.Instances.ListAsync(new WorkflowInstanceQuery(), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static instance => instance.Id));
        Assert.Equal(["inst-c", "inst-b"], (await host.Instances.ListAsync(new WorkflowInstanceQuery { Limit = 2 }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static instance => instance.Id));
        Assert.Equal(["inst-b"], (await host.Instances.ListAsync(new WorkflowInstanceQuery { Status = WorkflowInstanceStatus.Completed }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static instance => instance.Id));
        Assert.Equal(["inst-b", "inst-a"], (await host.Instances.ListAsync(new WorkflowInstanceQuery { DefinitionName = new WorkflowName("conf-list") }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static instance => instance.Id));
        Assert.Equal(["inst-b"], (await host.Instances.ListAsync(new WorkflowInstanceQuery { CorrelationId = "doc-1" }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static instance => instance.Id));
        Assert.Equal(["inst-c"], (await host.Instances.ListAsync(new WorkflowInstanceQuery { ParentInstanceId = "inst-a" }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static instance => instance.Id));
        Assert.Equal(["inst-c"], (await host.Instances.ListAsync(new WorkflowInstanceQuery { TenantId = "t1" }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static instance => instance.Id));
        // 分页、多状态、发起人、时间范围、结束时间与计数
        Assert.Equal(["inst-b", "inst-a"], (await host.Instances.ListAsync(new WorkflowInstanceQuery { Skip = 1 }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static instance => instance.Id));
        Assert.Equal(["inst-c", "inst-b"], (await host.Instances.ListAsync(new WorkflowInstanceQuery { Statuses = [WorkflowInstanceStatus.Completed, WorkflowInstanceStatus.Running], Skip = 0, Limit = 2 }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static instance => instance.Id));
        Assert.Equal(["inst-c"], (await host.Instances.ListAsync(new WorkflowInstanceQuery { StarterId = "starter-c" }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static instance => instance.Id));
        Assert.Equal(["inst-b", "inst-a"], (await host.Instances.ListAsync(new WorkflowInstanceQuery { CreatedFrom = now.AddMinutes(-2), CreatedTo = now }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static instance => instance.Id));
        Assert.Equal(["inst-b"], (await host.Instances.ListAsync(new WorkflowInstanceQuery { EndedBefore = now }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static instance => instance.Id));
        Assert.Equal(3, (await host.Instances.CountAsync(new WorkflowInstanceQuery { Skip = 2, Limit = 1 }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal(1, (await host.Instances.CountAsync(new WorkflowInstanceQuery { Status = WorkflowInstanceStatus.Completed }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal(2, (await host.Instances.FindManyAsync(["inst-a", "inst-c", "missing"], CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Count);

        var n1 = NodeInstance("inst-a", "n1", now.AddSeconds(1));
        var n2 = NodeInstance("inst-a", "n2", now);
        var n3 = NodeInstance("inst-a", "n3", now);
        var other = NodeInstance("inst-b", "n4", now);
        Assert.True((await host.Instances.SaveNodeInstancesAsync([n1, n2, n3, other], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.Equal([n2.Id, n3.Id, n1.Id], (await host.Instances.NodeInstancesAsync("inst-a", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static node => node.Id));
        Assert.Equal("n1", (await host.Instances.FindNodeInstanceAsync(n1.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().NodeId);

        // 整体替换：状态变了，顺序不变
        n2.Status = WorkflowNodeInstanceStatus.Completed;
        Assert.True((await host.Instances.SaveNodeInstancesAsync([n2], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.Equal(WorkflowNodeInstanceStatus.Completed, (await host.Instances.FindNodeInstanceAsync(n2.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().Status);
        Assert.Equal([n2.Id, n3.Id, n1.Id], (await host.Instances.NodeInstancesAsync("inst-a", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static node => node.Id));

        Assert.True((await host.Instances.DeleteAsync(["inst-a"], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.False((await host.Instances.FindAsync("inst-a", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
        Assert.Empty((await host.Instances.NodeInstancesAsync("inst-a", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        Assert.False((await host.Instances.FindNodeInstanceAsync(n1.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
        Assert.Single((await host.Instances.NodeInstancesAsync("inst-b", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
    }

    /// <summary>
    /// 用例 4：书签按实例、到期、种类与键、信号查询，排序与上限正确；按 ID 与按实例删除。
    /// </summary>
    [Fact]
    public async Task Bookmarks_Queries_Order_AndDelete()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var task1 = Bookmark("inst-1", "ni-1", BookmarkKinds.UserTask, "alice", null, now.AddSeconds(-3));
        var task2 = Bookmark("inst-2", "ni-2", BookmarkKinds.UserTask, "alice", null, now.AddSeconds(-2));
        var timerLate = Bookmark("inst-1", "ni-3", BookmarkKinds.Timer, null, now.AddMinutes(5), now.AddSeconds(-1));
        var timerDue = Bookmark("inst-2", "ni-4", BookmarkKinds.Timer, null, now.AddSeconds(-10), now);
        var retryDue = Bookmark("inst-1", "ni-5", BookmarkKinds.Retry, null, now.AddSeconds(-5), now);
        var signalAny = Bookmark("inst-3", "ni-6", BookmarkKinds.Signal, "go", null, now);
        var signalDoc = Bookmark("inst-4", "ni-7", BookmarkKinds.Signal, "go", null, now);
        signalDoc.CorrelationId = "doc-1";
        var signalOther = Bookmark("inst-5", "ni-8", BookmarkKinds.Signal, "go", null, now);
        signalOther.CorrelationId = "doc-2";
        Assert.True((await host.Bookmarks.SaveAsync([task1, task2, timerLate, timerDue, retryDue, signalAny, signalDoc, signalOther], CancellationToken.None).ConfigureAwait(false)).IsSuccess);

        Assert.Equal([task1.Id, timerLate.Id, retryDue.Id], (await host.Bookmarks.ByInstanceAsync("inst-1", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static bookmark => bookmark.Id));
        Assert.Equal([timerDue.Id, retryDue.Id], (await host.Bookmarks.DueAsync(now, 10, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static bookmark => bookmark.Id));
        Assert.Equal([timerDue.Id], (await host.Bookmarks.DueAsync(now, 1, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static bookmark => bookmark.Id));
        Assert.Equal([task1.Id, task2.Id], (await host.Bookmarks.ByKindAndKeyAsync(BookmarkKinds.UserTask, "alice", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static bookmark => bookmark.Id));
        Assert.Equal([signalAny.Id, signalDoc.Id, signalOther.Id], (await host.Bookmarks.BySignalAsync("go", null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static bookmark => bookmark.Id));
        Assert.Equal([signalAny.Id, signalDoc.Id], (await host.Bookmarks.BySignalAsync("go", "doc-1", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static bookmark => bookmark.Id));
        Assert.Equal(2, (await host.Bookmarks.FindManyAsync([task1.Id, "missing", task2.Id], CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Count);

        Assert.True((await host.Bookmarks.DeleteAsync([task1.Id, "missing"], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.False((await host.Bookmarks.FindAsync(task1.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
        Assert.True((await host.Bookmarks.DeleteByInstanceAsync(["inst-1", "inst-2"], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.Empty((await host.Bookmarks.ByInstanceAsync("inst-1", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        Assert.Empty((await host.Bookmarks.DueAsync(now, 10, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal(3, (await host.Bookmarks.BySignalAsync("go", null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Count);
    }

    /// <summary>
    /// 用例 5：实例锁互斥：持有期间第二个获取者等到超时得 <c>WORKFLOW_TIMEOUT_001</c>；释放后能获取；不同实例互不影响。
    /// </summary>
    [Fact]
    public async Task Lock_IsExclusivePerInstance_AndTimesOut()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var first = (await host.Lock.AcquireAsync("inst-lock", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        var other = (await host.Lock.AcquireAsync("inst-other", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        var blocked = await host.Lock.AcquireAsync("inst-lock", CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(WorkflowErrors.LockTimeout, blocked.Error?.Code);

        await first.DisposeAsync().ConfigureAwait(false);
        await other.DisposeAsync().ConfigureAwait(false);
        var second = await host.Lock.AcquireAsync("inst-lock", CancellationToken.None).ConfigureAwait(false);
        Assert.True(second.IsSuccess);
        await second.ValueOrThrow().DisposeAsync().ConfigureAwait(false);
        // 重复释放无害
        await first.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 一个最小的定义。
    /// </summary>
    protected static WorkflowDefinition Definition(string name, int version, WorkflowDefinitionStatus status) => new()
    {
        Id = WorkflowIdentifiers.NewId(),
        Name = new WorkflowName(name),
        Version = version,
        Status = status,
        Nodes = [new WorkflowNode { Id = "start", ActivityType = ActivityTypes.Start }],
        CreatedAt = DateTimeOffset.UtcNow,
        Properties = new JsonObject { ["tag"] = version },
    };

    /// <summary>
    /// 一个运行中的实例。
    /// </summary>
    protected static WorkflowInstance Instance(string definitionName, string id, DateTimeOffset? createdAt = null) => new()
    {
        Id = id,
        DefinitionId = "def-" + definitionName,
        DefinitionName = new WorkflowName(definitionName),
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
        StartedAt = createdAt ?? DateTimeOffset.UtcNow,
    };

    /// <summary>
    /// 一个节点实例。
    /// </summary>
    protected static WorkflowNodeInstance NodeInstance(string instanceId, string nodeId, DateTimeOffset startedAt) => new()
    {
        Id = WorkflowIdentifiers.NewId(),
        InstanceId = instanceId,
        NodeId = nodeId,
        ActivityType = ActivityTypes.Log,
        StartedAt = startedAt,
    };

    /// <summary>
    /// 一个书签。
    /// </summary>
    protected static WorkflowBookmark Bookmark(string instanceId, string nodeInstanceId, string kind, string? key, DateTimeOffset? dueAt, DateTimeOffset? createdAt = null) => new()
    {
        Id = WorkflowIdentifiers.NewId(),
        InstanceId = instanceId,
        NodeId = "node",
        NodeInstanceId = nodeInstanceId,
        Kind = kind,
        Key = key,
        DueAt = dueAt,
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
    };
}
