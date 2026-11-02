// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Tasks;
using Xunit;

namespace XiHan.Framework.Workflow.Conformance.Storage;

/// <summary>
/// 存储与实例锁的一致性套件（RFC-0021 §存储契约）。
/// </summary>
/// <remarks>
/// 查询、排序、批量、级联、快照语义、分区隔离与锁的互斥。Provider 继承并实现 <see cref="StartAsync"/>（返回装配了被测 Provider 的已启动宿主）；锁等待上限应配置在 1 秒以内。存储在 <see cref="WorkflowStoreHost.InPartitionAsync{T}"/> 开的调用里使用。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class WorkflowStoreConformance
{
    /// <summary>
    /// 启动装配了被测存储与锁的夹具。
    /// </summary>
    protected abstract Task<WorkflowStoreHost> StartAsync();

    /// <summary>
    /// 用例 1：定义按 ID、名字 + 版本、最新已发布查得到。
    /// </summary>
    /// <remarks>
    /// 最大版本与列表排序正确；删除后找不到；批量找忽略不存在的。
    /// </remarks>
    [Fact]
    public async Task Definitions_SaveFindListAndDelete()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var v1 = Definition("conf-def", 1, WorkflowDefinitionStatus.Published);
        var v2 = Definition("conf-def", 2, WorkflowDefinitionStatus.Draft);
        var other = Definition("conf-other", 1, WorkflowDefinitionStatus.Published);
        await host.InPartitionAsync(null, async s => Assert.True((await s.Definitions.SaveAsync(null, [v1, v2, other], CancellationToken.None).ConfigureAwait(false)).IsSuccess)).ConfigureAwait(false);

        await host.InPartitionAsync(null, async s =>
        {
            var found = (await s.Definitions.FindAsync(null, v2.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow();
            Assert.Equal((v2.Name, 2, WorkflowDefinitionStatus.Draft), (found.Name, found.Version, found.Status));
            Assert.Equal(1, (await s.Definitions.FindAsync(null, new WorkflowName("conf-def"), 1, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().Version);
            Assert.Equal(v1.Id, (await s.Definitions.FindLatestPublishedAsync(null, "conf-def", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().Id);
            Assert.Equal(2, (await s.Definitions.MaxVersionAsync(null, "conf-def", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Equal(0, (await s.Definitions.MaxVersionAsync(null, "conf-none", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.False((await s.Definitions.FindAsync(null, "missing", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);

            var listed = (await s.Definitions.ListAsync(null, null, null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            Assert.Equal([(v2.Id, 2), (v1.Id, 1), (other.Id, 1)], listed.Select(static definition => (definition.Id, definition.Version)));
            Assert.Equal([v1.Id, other.Id], (await s.Definitions.ListAsync(null, null, WorkflowDefinitionStatus.Published, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static definition => definition.Id));
            Assert.Equal([v2.Id, v1.Id], (await s.Definitions.ListAsync(null, new WorkflowName("conf-def"), null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static definition => definition.Id));
            Assert.Equal(2, (await s.Definitions.FindManyAsync(null, [v1.Id, "missing", other.Id], CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Count);
            Assert.True((await s.Definitions.DeleteAsync(null, [v2.Id, "missing"], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        }).ConfigureAwait(false);

        await host.InPartitionAsync(null, async s =>
        {
            Assert.False((await s.Definitions.FindAsync(null, v2.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
            Assert.Equal(1, (await s.Definitions.MaxVersionAsync(null, "conf-def", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 2：保存后再改原对象不影响存下的。
    /// </summary>
    /// <remarks>
    /// 返回的对象再改也不影响（快照语义）。
    /// </remarks>
    [Fact]
    public async Task Stores_KeepSnapshots_NotReferences()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var instance = Instance("conf-snap", "inst-snap");
        instance.Variables["x"] = 1;
        var bookmark = Bookmark("inst-snap", "node-inst", BookmarkKinds.Signal, "sig", null);
        bookmark.Payload["p"] = "a";
        await host.InPartitionAsync(null, async s =>
        {
            Assert.True((await s.Instances.SaveAsync(null, [instance], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            Assert.True((await s.Bookmarks.SaveAsync(null, [bookmark], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        }).ConfigureAwait(false);
        instance.Variables["x"] = 2;
        instance.Status = WorkflowInstanceStatus.Completed;
        bookmark.Payload["p"] = "b";

        await host.InPartitionAsync(null, async s =>
        {
            var stored = (await s.Instances.FindAsync(null, "inst-snap", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow();
            Assert.Equal(1, stored.Variables["x"]!.GetValue<int>());
            Assert.Equal(WorkflowInstanceStatus.Running, stored.Status);
            stored.Variables["x"] = 3;
            Assert.Equal(1, (await s.Instances.FindAsync(null, "inst-snap", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().Variables["x"]!.GetValue<int>());
            Assert.Equal("a", (await s.Bookmarks.FindAsync(null, bookmark.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().Payload["p"]!.GetValue<string>());
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 3：实例列表按创建时间降序、过滤项与上限生效。
    /// </summary>
    /// <remarks>
    /// 关联 ID 集合（重复只算一个、空集合不匹配）、只取顶层、关键字（实例标题——显示名，没有取定义名——或关联 ID；<c>%</c> 与 <c>_</c> 按字面，不跨字段）也是过滤项（ADR-0096、ADR-0099）。节点实例按开始时间升序、同刻按写入顺序；删实例级联节点实例。
    /// </remarks>
    [Fact]
    public async Task Instances_ListFiltersAndOrders_NodeInstancesOrdered_DeleteCascades()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var a = Instance("conf-list", "inst-a", now.AddMinutes(-2));
        a.DisplayName = "Expense 50% Claim";
        var b = Instance("conf-list", "inst-b", now.AddMinutes(-1));
        b.Status = WorkflowInstanceStatus.Completed;
        b.CorrelationId = "doc-1";
        b.EndedAt = now.AddSeconds(-30);
        var c = Instance("conf-list-2", "inst-c", now);
        c.ParentInstanceId = "inst-a";
        c.StarterId = "starter-c";
        c.CorrelationId = "doc-1";
        c.DisplayName = "Sub_Order";
        var n1 = NodeInstance("inst-a", "n1", now.AddSeconds(1));
        var n2 = NodeInstance("inst-a", "n2", now);
        var n3 = NodeInstance("inst-a", "n3", now);
        var other = NodeInstance("inst-b", "n4", now);
        await host.InPartitionAsync(null, async s => Assert.True((await s.Instances.SaveAsync(null, [a, b, c], CancellationToken.None).ConfigureAwait(false)).IsSuccess)).ConfigureAwait(false);

        await host.InPartitionAsync(null, async s =>
        {
            Assert.Equal(["inst-c", "inst-b", "inst-a"], await IdsAsync(s, new WorkflowInstanceQuery()).ConfigureAwait(false));
            Assert.Equal(["inst-c", "inst-b"], await IdsAsync(s, new WorkflowInstanceQuery { Limit = 2 }).ConfigureAwait(false));
            Assert.Equal(["inst-b"], await IdsAsync(s, new WorkflowInstanceQuery { Status = WorkflowInstanceStatus.Completed }).ConfigureAwait(false));
            Assert.Equal(["inst-b", "inst-a"], await IdsAsync(s, new WorkflowInstanceQuery { DefinitionName = new WorkflowName("conf-list") }).ConfigureAwait(false));
            Assert.Equal(["inst-c", "inst-b"], await IdsAsync(s, new WorkflowInstanceQuery { CorrelationIds = ["doc-1"] }).ConfigureAwait(false));
            // 关联 ID 集合：重复只算一个、不存在的不影响；空集合不匹配任何实例；只取顶层排除子流程
            Assert.Equal(["inst-c", "inst-b"], await IdsAsync(s, new WorkflowInstanceQuery { CorrelationIds = ["doc-1", "doc-1", "doc-none"] }).ConfigureAwait(false));
            Assert.Equal(["inst-b"], await IdsAsync(s, new WorkflowInstanceQuery { CorrelationIds = ["doc-1"], TopLevelOnly = true }).ConfigureAwait(false));
            Assert.Empty(await IdsAsync(s, new WorkflowInstanceQuery { CorrelationIds = [] }).ConfigureAwait(false));
            Assert.Equal(0, (await s.Instances.CountAsync(null, new WorkflowInstanceQuery { CorrelationIds = [] }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Equal(["inst-b", "inst-a"], await IdsAsync(s, new WorkflowInstanceQuery { TopLevelOnly = true }).ConfigureAwait(false));
            Assert.Equal(2, (await s.Instances.CountAsync(null, new WorkflowInstanceQuery { TopLevelOnly = true, Limit = 1 }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Empty(await IdsAsync(s, new WorkflowInstanceQuery { TopLevelOnly = true, ParentInstanceId = "inst-a" }).ConfigureAwait(false));
            // 关键字：实例标题（显示名，没有显示名取定义名）或关联 ID；不区分大小写，% 与 _ 按字面
            Assert.Equal(["inst-a"], await IdsAsync(s, new WorkflowInstanceQuery { Keyword = " CLAIM " }).ConfigureAwait(false));
            Assert.Equal(["inst-b"], await IdsAsync(s, new WorkflowInstanceQuery { Keyword = "conf-list" }).ConfigureAwait(false));
            Assert.Equal(["inst-c"], await IdsAsync(s, new WorkflowInstanceQuery { Keyword = "b_o" }).ConfigureAwait(false));
            Assert.Equal(["inst-a"], await IdsAsync(s, new WorkflowInstanceQuery { Keyword = "50% c" }).ConfigureAwait(false));
            Assert.Empty(await IdsAsync(s, new WorkflowInstanceQuery { Keyword = "50_" }).ConfigureAwait(false));
            // 全角与半角是不同的字符（ADR-0099 第四节）
            Assert.Empty(await IdsAsync(s, new WorkflowInstanceQuery { Keyword = "50％" }).ConfigureAwait(false));
            Assert.Empty(await IdsAsync(s, new WorkflowInstanceQuery { Keyword = "sub＿order" }).ConfigureAwait(false));
            Assert.Equal(1, (await s.Instances.CountAsync(null, new WorkflowInstanceQuery { Keyword = "sub_", StarterId = "starter-c" }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            // 关联 ID 也被关键字匹配（同 4.x，ADR-0099）：不区分大小写、按包含；与只取顶层取交集；标题与关联 ID 不跨字段
            Assert.Equal(["inst-c", "inst-b"], await IdsAsync(s, new WorkflowInstanceQuery { Keyword = " DOC-1 " }).ConfigureAwait(false));
            Assert.Equal(["inst-b"], await IdsAsync(s, new WorkflowInstanceQuery { Keyword = "c-1", TopLevelOnly = true }).ConfigureAwait(false));
            Assert.Equal(2, (await s.Instances.CountAsync(null, new WorkflowInstanceQuery { Keyword = "doc" }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Empty(await IdsAsync(s, new WorkflowInstanceQuery { Keyword = "listdoc" }).ConfigureAwait(false));
            Assert.Empty(await IdsAsync(s, new WorkflowInstanceQuery { Keyword = "order doc" }).ConfigureAwait(false));
            Assert.Equal(["inst-c"], await IdsAsync(s, new WorkflowInstanceQuery { ParentInstanceId = "inst-a" }).ConfigureAwait(false));
            // 分页、多状态、发起人、时间范围、结束时间与计数
            Assert.Equal(["inst-b", "inst-a"], await IdsAsync(s, new WorkflowInstanceQuery { Skip = 1 }).ConfigureAwait(false));
            Assert.Equal(["inst-c", "inst-b"], await IdsAsync(s, new WorkflowInstanceQuery { Statuses = [WorkflowInstanceStatus.Completed, WorkflowInstanceStatus.Running], Skip = 0, Limit = 2 }).ConfigureAwait(false));
            Assert.Equal(["inst-c"], await IdsAsync(s, new WorkflowInstanceQuery { StarterId = "starter-c" }).ConfigureAwait(false));
            Assert.Equal(["inst-b", "inst-a"], await IdsAsync(s, new WorkflowInstanceQuery { CreatedFrom = now.AddMinutes(-2), CreatedTo = now }).ConfigureAwait(false));
            Assert.Equal(["inst-b"], await IdsAsync(s, new WorkflowInstanceQuery { EndedBefore = now }).ConfigureAwait(false));
            Assert.Equal(3, (await s.Instances.CountAsync(null, new WorkflowInstanceQuery { Skip = 2, Limit = 1 }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Equal(1, (await s.Instances.CountAsync(null, new WorkflowInstanceQuery { Status = WorkflowInstanceStatus.Completed }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Equal(2, (await s.Instances.FindManyAsync(null, ["inst-a", "inst-c", "missing"], CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Count);
            Assert.True((await s.Instances.SaveNodeInstancesAsync(null, [n1, n2, n3, other], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        }).ConfigureAwait(false);

        await host.InPartitionAsync(null, async s =>
        {
            Assert.Equal([n2.Id, n3.Id, n1.Id], (await s.Instances.NodeInstancesAsync(null, "inst-a", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static node => node.Id));
            Assert.Equal("n1", (await s.Instances.FindNodeInstanceAsync(null, n1.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().NodeId);

            // 整体替换：状态变了，顺序不变
            n2.Status = WorkflowNodeInstanceStatus.Completed;
            Assert.True((await s.Instances.SaveNodeInstancesAsync(null, [n2], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            Assert.Equal(WorkflowNodeInstanceStatus.Completed, (await s.Instances.FindNodeInstanceAsync(null, n2.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().Status);
            Assert.Equal([n2.Id, n3.Id, n1.Id], (await s.Instances.NodeInstancesAsync(null, "inst-a", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static node => node.Id));
            Assert.True((await s.Instances.DeleteAsync(null, ["inst-a"], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        }).ConfigureAwait(false);

        await host.InPartitionAsync(null, async s =>
        {
            Assert.False((await s.Instances.FindAsync(null, "inst-a", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
            Assert.Empty((await s.Instances.NodeInstancesAsync(null, "inst-a", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.False((await s.Instances.FindNodeInstanceAsync(null, n1.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
            Assert.Single((await s.Instances.NodeInstancesAsync(null, "inst-b", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 4：书签按实例、到期、种类与键、信号查询，排序与上限正确。
    /// </summary>
    /// <remarks>
    /// 按 ID 与按实例删除。
    /// </remarks>
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
        await host.InPartitionAsync(null, async s => Assert.True((await s.Bookmarks.SaveAsync(null, [task1, task2, timerLate, timerDue, retryDue, signalAny, signalDoc, signalOther], CancellationToken.None).ConfigureAwait(false)).IsSuccess)).ConfigureAwait(false);

        await host.InPartitionAsync(null, async s =>
        {
            Assert.Equal([task1.Id, timerLate.Id, retryDue.Id], (await s.Bookmarks.ByInstanceAsync(null, "inst-1", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static bookmark => bookmark.Id));
            Assert.Equal([timerDue.Id, retryDue.Id], (await s.Bookmarks.DueAsync(null, now, 10, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static bookmark => bookmark.Id));
            Assert.Equal([timerDue.Id], (await s.Bookmarks.DueAsync(null, now, 1, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static bookmark => bookmark.Id));
            Assert.Equal([task1.Id, task2.Id], (await s.Bookmarks.ByKindAndKeyAsync(null, BookmarkKinds.UserTask, "alice", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static bookmark => bookmark.Id));
            Assert.Equal([signalAny.Id, signalDoc.Id, signalOther.Id], (await s.Bookmarks.BySignalAsync(null, "go", null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static bookmark => bookmark.Id));
            Assert.Equal([signalAny.Id, signalDoc.Id], (await s.Bookmarks.BySignalAsync(null, "go", "doc-1", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static bookmark => bookmark.Id));
            Assert.Equal(2, (await s.Bookmarks.FindManyAsync(null, [task1.Id, "missing", task2.Id], CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Count);
            Assert.True((await s.Bookmarks.DeleteAsync(null, [task1.Id, "missing"], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        }).ConfigureAwait(false);

        await host.InPartitionAsync(null, async s =>
        {
            Assert.False((await s.Bookmarks.FindAsync(null, task1.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
            Assert.True((await s.Bookmarks.DeleteByInstanceAsync(null, ["inst-1", "inst-2"], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        }).ConfigureAwait(false);

        await host.InPartitionAsync(null, async s =>
        {
            Assert.Empty((await s.Bookmarks.ByInstanceAsync(null, "inst-1", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Empty((await s.Bookmarks.DueAsync(null, now, 10, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Equal(3, (await s.Bookmarks.BySignalAsync(null, "go", null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Count);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 5：实例锁互斥。
    /// </summary>
    /// <remarks>
    /// 持有期间第二个获取者等到超时得 <c>WORKFLOW_TIMEOUT_001</c>；释放后能获取；不同实例互不影响。
    /// </remarks>
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
    /// 用例 6：可领取的书签按到期时刻限量查询。
    /// </summary>
    /// <remarks>
    /// 按种类、键与到期时刻过滤，创建时间升序，条数受上限约束。到期时刻为空或不晚于此刻才可领取（恰好等于此刻也算）。
    /// </remarks>
    [Fact]
    public async Task Bookmarks_Available_FilterByKindKeyAndDue_WithLimit()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        // 取整到秒：恰好等于此刻的到期时刻要原样存取，不受各存储时间精度的影响
        var now = DateTimeOffset.UtcNow;
        now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
        var fresh = Bookmark("inst-1", "ni-1", BookmarkKinds.External, "pay", null, now.AddSeconds(-3));
        var expired = Bookmark("inst-2", "ni-2", BookmarkKinds.External, "pay", now.AddSeconds(-1), now.AddSeconds(-2));
        var boundary = Bookmark("inst-3", "ni-3", BookmarkKinds.External, "pay", now, now.AddSeconds(-1));
        var locked = Bookmark("inst-4", "ni-4", BookmarkKinds.External, "pay", now.AddMinutes(5), now.AddSeconds(-4));
        var otherTopic = Bookmark("inst-6", "ni-6", BookmarkKinds.External, "ship", null, now.AddSeconds(-6));
        var otherKind = Bookmark("inst-7", "ni-7", BookmarkKinds.UserTask, "pay", null, now.AddSeconds(-7));
        await host.InPartitionAsync(null, async s => Assert.True((await s.Bookmarks.SaveAsync(null, [fresh, expired, boundary, locked, otherTopic, otherKind], CancellationToken.None).ConfigureAwait(false)).IsSuccess)).ConfigureAwait(false);

        await host.InPartitionAsync(null, async s =>
        {
            Assert.Equal([fresh.Id, expired.Id, boundary.Id], await AvailableAsync(s, now, 10).ConfigureAwait(false));
            Assert.Equal([fresh.Id, expired.Id], await AvailableAsync(s, now, 2).ConfigureAwait(false));
            Assert.Equal([locked.Id, fresh.Id, expired.Id, boundary.Id], await AvailableAsync(s, now.AddMinutes(10), 10).ConfigureAwait(false));
            Assert.Empty(await AvailableAsync(s, now, 0).ConfigureAwait(false));
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 7：按名字的查询以分区为名字空间。
    /// </summary>
    /// <remarks>
    /// 版本号、按名字与版本找、最新已发布都只看本分区的定义：平台与各租户各自从版本 1 编号，互不可见。
    /// </remarks>
    [Fact]
    public async Task Definitions_NameQueries_AreScopedToThePartition()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var platform = Definition("conf-ns", 1, WorkflowDefinitionStatus.Published);
        var t1First = Definition("conf-ns", 1, WorkflowDefinitionStatus.Published) with { TenantId = "t1" };
        var t1Second = Definition("conf-ns", 2, WorkflowDefinitionStatus.Published) with { TenantId = "t1" };
        var t1Draft = Definition("conf-ns", 3, WorkflowDefinitionStatus.Draft) with { TenantId = "t1" };
        var name = new WorkflowName("conf-ns");
        await host.InPartitionAsync(null, async s => Assert.True((await s.Definitions.SaveAsync(null, [platform], CancellationToken.None).ConfigureAwait(false)).IsSuccess)).ConfigureAwait(false);
        await host.InPartitionAsync("t1", async s => Assert.True((await s.Definitions.SaveAsync("t1", [t1First, t1Second, t1Draft], CancellationToken.None).ConfigureAwait(false)).IsSuccess)).ConfigureAwait(false);

        await host.InPartitionAsync(null, async s =>
        {
            Assert.Equal(1, (await s.Definitions.MaxVersionAsync(null, name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Equal(platform.Id, (await s.Definitions.FindAsync(null, name, 1, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().Id);
            Assert.False((await s.Definitions.FindAsync(null, name, 2, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
            Assert.Equal(platform.Id, (await s.Definitions.FindLatestPublishedAsync(null, name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().Id);
        }).ConfigureAwait(false);
        await host.InPartitionAsync("t1", async s =>
        {
            Assert.Equal(3, (await s.Definitions.MaxVersionAsync("t1", name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Equal(t1First.Id, (await s.Definitions.FindAsync("t1", name, 1, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().Id);
            Assert.Equal(t1Second.Id, (await s.Definitions.FindLatestPublishedAsync("t1", name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().Id);
        }).ConfigureAwait(false);
        await host.InPartitionAsync("t2", async s =>
        {
            Assert.Equal(0, (await s.Definitions.MaxVersionAsync("t2", name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.False((await s.Definitions.FindAsync("t2", name, 1, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
            Assert.False((await s.Definitions.FindLatestPublishedAsync("t2", name, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 8：分区互不可见。
    /// </summary>
    /// <remarks>
    /// 租户 t1 写下的定义、实例、节点实例与书签，平台与租户 t2 按 ID、列表、计数、到期、种类与键、信号、可领取都查不到，删除也碰不到；t1 自己照常查得到。
    /// </remarks>
    [Fact]
    public async Task Partitions_AreIsolated()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var definition = Definition("conf-part", 1, WorkflowDefinitionStatus.Published) with { TenantId = "t1" };
        var instance = Instance("conf-part", "inst-part");
        instance.TenantId = "t1";
        instance.CorrelationId = "doc-part";
        var node = NodeInstance("inst-part", "n1", now);
        var task = Bookmark("inst-part", node.Id, BookmarkKinds.UserTask, "alice", null, now);
        var timer = Bookmark("inst-part", node.Id, BookmarkKinds.Timer, null, now.AddSeconds(-1), now);
        var signal = Bookmark("inst-part", node.Id, BookmarkKinds.Signal, "go", null, now);
        var external = Bookmark("inst-part", node.Id, BookmarkKinds.External, "pay", null, now);
        foreach (var bookmark in new[] { task, timer, signal, external })
        {
            bookmark.TenantId = "t1";
        }

        await host.InPartitionAsync("t1", async s =>
        {
            Assert.True((await s.Definitions.SaveAsync("t1", [definition], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            Assert.True((await s.Instances.SaveAsync("t1", [instance], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            Assert.True((await s.Instances.SaveNodeInstancesAsync("t1", [node], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            Assert.True((await s.Bookmarks.SaveAsync("t1", [task, timer, signal, external], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        }).ConfigureAwait(false);

        foreach (var outsider in new[] { null, "t2" })
        {
            await host.InPartitionAsync(outsider, async s =>
            {
                Assert.False((await s.Definitions.FindAsync(outsider, definition.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
                Assert.Empty((await s.Definitions.ListAsync(outsider, null, null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
                Assert.Empty((await s.Definitions.ListSummariesAsync(outsider, new WorkflowDefinitionQuery { Keyword = "conf" }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
                Assert.Equal(0, (await s.Definitions.CountAsync(outsider, new WorkflowDefinitionQuery(), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
                Assert.Empty((await s.Definitions.FindManyAsync(outsider, [definition.Id], CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
                Assert.False((await s.Instances.FindAsync(outsider, instance.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
                Assert.Empty((await s.Instances.FindManyAsync(outsider, [instance.Id], CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
                Assert.Empty((await s.Instances.ListAsync(outsider, new WorkflowInstanceQuery { CorrelationIds = ["doc-part"] }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
                Assert.Equal(0, (await s.Instances.CountAsync(outsider, new WorkflowInstanceQuery(), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
                Assert.Empty((await s.Instances.NodeInstancesAsync(outsider, instance.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
                Assert.False((await s.Instances.FindNodeInstanceAsync(outsider, node.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
                Assert.False((await s.Bookmarks.FindAsync(outsider, task.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
                Assert.Empty((await s.Bookmarks.ByInstanceAsync(outsider, instance.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
                Assert.Empty((await s.Bookmarks.DueAsync(outsider, now, 10, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
                Assert.Empty((await s.Bookmarks.ByKindAndKeyAsync(outsider, BookmarkKinds.UserTask, "alice", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
                Assert.Empty((await s.Bookmarks.BySignalAsync(outsider, "go", null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
                Assert.Empty((await s.Bookmarks.AvailableAsync(outsider, BookmarkKinds.External, "pay", now, 10, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
                // 删除碰不到别的分区的记录
                Assert.True((await s.Definitions.DeleteAsync(outsider, [definition.Id], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
                Assert.True((await s.Instances.DeleteAsync(outsider, [instance.Id], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
                Assert.True((await s.Bookmarks.DeleteByInstanceAsync(outsider, [instance.Id], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            }).ConfigureAwait(false);
        }

        await host.InPartitionAsync("t1", async s =>
        {
            Assert.True((await s.Definitions.FindAsync("t1", definition.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
            Assert.Equal(("t1", 1L), (Assert.Single((await s.Definitions.ListSummariesAsync("t1", new WorkflowDefinitionQuery { Keyword = "conf" }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow()).TenantId, (await s.Definitions.CountAsync("t1", new WorkflowDefinitionQuery(), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow()));
            Assert.Equal("t1", (await s.Instances.FindAsync("t1", instance.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().TenantId);
            Assert.Single((await s.Instances.NodeInstancesAsync("t1", instance.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Equal(4, (await s.Bookmarks.ByInstanceAsync("t1", instance.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Count);
            Assert.Equal([timer.Id], (await s.Bookmarks.DueAsync("t1", now, 10, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static bookmark => bookmark.Id));
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 9：写入的记录属于别的租户是编程错误。
    /// </summary>
    /// <remarks>
    /// 记录的 <c>TenantId</c> 与写入的分区不同时抛出 <see cref="ArgumentException"/>，不替调用方改写、不落库。
    /// </remarks>
    [Fact]
    public async Task Writes_RejectRecordsOfAnotherPartition()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var instance = Instance("conf-owner", "inst-owner");
        instance.TenantId = "t2";
        await host.InPartitionAsync("t1", async s =>
        {
            await Assert.ThrowsAsync<ArgumentException>(async () => await s.Instances.SaveAsync("t1", [instance], CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
            await Assert.ThrowsAsync<ArgumentException>(async () => await s.Definitions.SaveAsync("t1", [Definition("conf-owner", 1, WorkflowDefinitionStatus.Draft)], CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
            await Assert.ThrowsAsync<ArgumentException>(async () => await s.Bookmarks.SaveAsync("t1", [Bookmark("inst-owner", "ni", BookmarkKinds.Timer, null, null)], CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
        }).ConfigureAwait(false);
        await host.InPartitionAsync("t2", async s => Assert.False((await s.Instances.FindAsync("t2", instance.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue)).ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 10：待办查询下推到存储。
    /// </summary>
    /// <remarks>
    /// 只含用户任务、所属实例运行中的书签；按指派人、实例、节点、定义名、创建时间与关键字（任务标题、实例标题、定义名或所属实例的关联 ID，ADR-0096、ADR-0099）过滤，按创建时间升序或降序分页，计数与列表同条件。实例挂起后它的任务退出结果、恢复后回来，实例的标题或关联 ID 变了关键字随之匹配新值；别的分区的任务看不到。
    /// </remarks>
    [Fact]
    public async Task Bookmarks_UserTaskQuery_FiltersOrdersPagesAndFollowsInstanceStatus()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        // 取整到秒：创建时间的边界条件不受各存储时间精度的影响
        var now = DateTimeOffset.UtcNow;
        now = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
        var a = Instance("conf.a", "inst-qa", now.AddMinutes(-1));
        a.DisplayName = "Expense Claim";
        a.CorrelationId = "EXP_2026-07";
        var b = Instance("conf.b", "inst-qb", now.AddMinutes(-1));
        var held = Instance("conf.a", "inst-qs", now.AddMinutes(-1));
        var t1 = Bookmark(a.Id, "ni-1", BookmarkKinds.UserTask, "alice", null, now.AddSeconds(-5));
        t1.NodeId = "approve";
        t1.Payload[UserTaskPayloadKeys.Title] = "Approve 100% now";
        var t2 = Bookmark(b.Id, "ni-2", BookmarkKinds.UserTask, "alice", null, now.AddSeconds(-4));
        t2.NodeId = "review";
        t2.Payload[UserTaskPayloadKeys.Title] = "Review_Doc";
        var t3 = Bookmark(a.Id, "ni-1", BookmarkKinds.UserTask, "bob", null, now.AddSeconds(-3));
        t3.NodeId = "approve";
        var t4 = Bookmark(held.Id, "ni-3", BookmarkKinds.UserTask, "alice", null, now.AddSeconds(-2));
        t4.NodeId = "approve";
        var t5 = Bookmark(a.Id, "ni-4", BookmarkKinds.UserTask, "alice", null, now.AddSeconds(-1));
        t5.NodeId = "approve";
        t5.Payload[UserTaskPayloadKeys.Title] = "Second look";
        var signal = Bookmark(a.Id, "ni-5", BookmarkKinds.Signal, "alice", null, now.AddSeconds(-6));
        var foreign = Instance("conf.a", "inst-qt", now.AddMinutes(-1));
        foreign.TenantId = "t1";
        var foreignTask = Bookmark(foreign.Id, "ni-6", BookmarkKinds.UserTask, "alice", null, now.AddSeconds(-7));
        foreignTask.TenantId = "t1";
        // 引擎的写回先写实例再写书签，用例照这个顺序
        await host.InPartitionAsync(null, async s =>
        {
            Assert.True((await s.Instances.SaveAsync(null, [a, b, held], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            Assert.True((await s.Bookmarks.SaveAsync(null, [t1, t2, t3, t4, t5, signal], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        }).ConfigureAwait(false);
        await host.InPartitionAsync("t1", async s =>
        {
            Assert.True((await s.Instances.SaveAsync("t1", [foreign], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            Assert.True((await s.Bookmarks.SaveAsync("t1", [foreignTask], CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        }).ConfigureAwait(false);

        await host.InPartitionAsync(null, async s =>
        {
            Assert.Equal([t1.Id, t2.Id, t4.Id, t5.Id], await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice" }).ConfigureAwait(false));
            Assert.Equal(4, await TaskCountAsync(s, new UserTaskQuery { AssigneeId = "alice" }).ConfigureAwait(false));
        }).ConfigureAwait(false);

        // 挂起：实例的状态变了，它的任务退出结果
        held.Status = WorkflowInstanceStatus.Suspended;
        await host.InPartitionAsync(null, async s => Assert.True((await s.Instances.SaveAsync(null, [held], CancellationToken.None).ConfigureAwait(false)).IsSuccess)).ConfigureAwait(false);
        await host.InPartitionAsync(null, async s =>
        {
            Assert.Equal([t1.Id, t2.Id, t5.Id], await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice" }).ConfigureAwait(false));
            Assert.Equal(3, await TaskCountAsync(s, new UserTaskQuery { AssigneeId = "alice" }).ConfigureAwait(false));
            Assert.Equal([t5.Id, t2.Id, t1.Id], await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Descending = true }).ConfigureAwait(false));
            Assert.Equal([t2.Id], await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Skip = 1, Limit = 1 }).ConfigureAwait(false));
            Assert.Equal(3, await TaskCountAsync(s, new UserTaskQuery { AssigneeId = "alice", Skip = 1, Limit = 1 }).ConfigureAwait(false));
            Assert.Empty(await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Skip = 3 }).ConfigureAwait(false));
            Assert.Equal([t1.Id, t5.Id], await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", DefinitionName = "conf.a" }).ConfigureAwait(false));
            Assert.Equal([t2.Id], await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", NodeId = "review" }).ConfigureAwait(false));
            Assert.Equal([t1.Id, t3.Id, t5.Id], await TaskIdsAsync(s, new UserTaskQuery { InstanceId = a.Id }).ConfigureAwait(false));
            Assert.Equal(1, await TaskCountAsync(s, new UserTaskQuery { InstanceId = a.Id, AssigneeId = "bob" }).ConfigureAwait(false));
            Assert.Equal([t2.Id], await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", CreatedFrom = now.AddSeconds(-4), CreatedTo = now.AddSeconds(-1) }).ConfigureAwait(false));
            Assert.Empty(await TaskIdsAsync(s, new UserTaskQuery { InstanceId = held.Id }).ConfigureAwait(false));

            // 标题关键字：任务标题或实例标题（显示名，没有取定义名）任一包含即匹配；不区分大小写，% 与 _ 按字面，不跨字段
            Assert.Equal([t1.Id, t5.Id], await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "EXPENSE" }).ConfigureAwait(false));
            Assert.Equal([t2.Id], await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "w_d" }).ConfigureAwait(false));
            Assert.Equal([t2.Id], await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = " conf.b " }).ConfigureAwait(false));
            Assert.Equal([t1.Id], await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "100% " }).ConfigureAwait(false));
            Assert.Empty(await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "100_" }).ConfigureAwait(false));
            Assert.Empty(await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "nowexpense" }).ConfigureAwait(false));
            Assert.Equal(1, await TaskCountAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "LOOK" }).ConfigureAwait(false));
            Assert.Equal(3, await TaskCountAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "   " }).ConfigureAwait(false));

            // 定义名与所属实例的关联 ID 也被关键字匹配（同 4.x，ADR-0099）：a 的标题里没有定义名；关联 ID 里的 _ 按字面；不跨字段
            Assert.Equal([t1.Id, t5.Id], await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "CONF.A" }).ConfigureAwait(false));
            Assert.Equal([t1.Id, t5.Id], await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "exp_2026" }).ConfigureAwait(false));
            Assert.Empty(await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "exp%2026" }).ConfigureAwait(false));
            // 全角与半角是不同的字符（ADR-0099 第四节）
            Assert.Empty(await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "exp＿2026" }).ConfigureAwait(false));
            Assert.Empty(await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "100％" }).ConfigureAwait(false));
            Assert.Empty(await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "review＿doc" }).ConfigureAwait(false));
            Assert.Equal(1, await TaskCountAsync(s, new UserTaskQuery { InstanceId = a.Id, AssigneeId = "bob", Keyword = "2026-07" }).ConfigureAwait(false));
            Assert.Empty(await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "claimexp" }).ConfigureAwait(false));
            Assert.Empty(await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "07conf" }).ConfigureAwait(false));
        }).ConfigureAwait(false);

        // 恢复：任务回到结果里；别的分区的任务一直看不到
        held.Status = WorkflowInstanceStatus.Running;
        await host.InPartitionAsync(null, async s => Assert.True((await s.Instances.SaveAsync(null, [held], CancellationToken.None).ConfigureAwait(false)).IsSuccess)).ConfigureAwait(false);
        await host.InPartitionAsync(null, async s => Assert.Equal([t1.Id, t2.Id, t4.Id, t5.Id], await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice" }).ConfigureAwait(false))).ConfigureAwait(false);
        await host.InPartitionAsync("t1", async s =>
        {
            Assert.Equal([foreignTask.Id], await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice" }).ConfigureAwait(false));
            Assert.Equal(1, await TaskCountAsync(s, new UserTaskQuery { AssigneeId = "alice" }).ConfigureAwait(false));
        }).ConfigureAwait(false);

        // 实例的标题与关联 ID 变了：它的书签行随之同步，关键字匹配新值、不再匹配旧值；定义名照旧匹配
        a.DisplayName = "Leave Request";
        a.CorrelationId = "LV-9";
        await host.InPartitionAsync(null, async s => Assert.True((await s.Instances.SaveAsync(null, [a], CancellationToken.None).ConfigureAwait(false)).IsSuccess)).ConfigureAwait(false);
        await host.InPartitionAsync(null, async s =>
        {
            Assert.Equal([t1.Id, t5.Id], await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "leave" }).ConfigureAwait(false));
            Assert.Empty(await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "expense" }).ConfigureAwait(false));
            Assert.Equal([t1.Id, t5.Id], await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "lv-9" }).ConfigureAwait(false));
            Assert.Empty(await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "exp_2026" }).ConfigureAwait(false));
            // held 已恢复：它的标题就是定义名 conf.a
            Assert.Equal([t1.Id, t4.Id, t5.Id], await TaskIdsAsync(s, new UserTaskQuery { AssigneeId = "alice", Keyword = "conf.a" }).ConfigureAwait(false));
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 11：定义查询按关键字、名字、分类与状态过滤，排序、分页与计数正确。
    /// </summary>
    /// <remarks>
    /// 关键字去掉首尾空白、不区分大小写地包含在名字、显示名或说明里（ADR-0096）：<c>%</c>、<c>_</c>、<c>[</c>、<c>\</c> 按字面匹配，与全角的 <c>％</c>、<c>＿</c>、<c>［</c>、<c>＼</c> 不视为相同（ADR-0099），不跨字段匹配；整体替换后按新内容匹配。结果按名字升序、版本降序，总数不看分页，摘要带列表要的字段。
    /// </remarks>
    [Fact]
    public async Task Definitions_Query_FiltersPagesAndCounts()
    {
        await using var host = await StartAsync().ConfigureAwait(false);
        var expense1 = Definition("conf-q-expense", 1, WorkflowDefinitionStatus.Published) with { DisplayName = "Expense Claim", Category = "finance", Description = "Reimburse travel_costs at 50% rate" };
        var expense2 = Definition("conf-q-expense", 2, WorkflowDefinitionStatus.Draft) with { DisplayName = "Expense Claim", Category = "finance" };
        var leave = Definition("conf-q-leave", 1, WorkflowDefinitionStatus.Published) with { DisplayName = "请假申请", Category = "hr", Description = @"Form C:\hr\[leave]" };
        var purchase = Definition("conf-q-purchase", 1, WorkflowDefinitionStatus.Disabled) with { Description = "Buy EQUIPMENT at 10％ off" };
        await host.InPartitionAsync(null, async s => Assert.True((await s.Definitions.SaveAsync(null, [purchase, leave, expense2, expense1], CancellationToken.None).ConfigureAwait(false)).IsSuccess)).ConfigureAwait(false);

        await host.InPartitionAsync(null, async s =>
        {
            Assert.Equal([expense2.Id, expense1.Id, leave.Id, purchase.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery()).ConfigureAwait(false));
            Assert.Equal(4, await DefinitionCountAsync(s, new WorkflowDefinitionQuery()).ConfigureAwait(false));

            // 关键字：名字、显示名、说明任一包含即匹配，不区分大小写，去掉首尾空白，只有空白不过滤
            Assert.Equal([expense2.Id, expense1.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "EXPENSE" }).ConfigureAwait(false));
            Assert.Equal([expense2.Id, expense1.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "claim" }).ConfigureAwait(false));
            Assert.Equal([leave.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "请假" }).ConfigureAwait(false));
            Assert.Equal([purchase.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "equipment" }).ConfigureAwait(false));
            Assert.Equal([leave.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "  q-leave  " }).ConfigureAwait(false));
            Assert.Equal(4, await DefinitionCountAsync(s, new WorkflowDefinitionQuery { Keyword = "   " }).ConfigureAwait(false));
            Assert.Equal(2, await DefinitionCountAsync(s, new WorkflowDefinitionQuery { Keyword = "Expense" }).ConfigureAwait(false));

            // % 与 _ 按字面匹配，不当通配；关键字不跨字段（名字末尾 + 显示名开头）匹配
            Assert.Equal([expense1.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "l_c" }).ConfigureAwait(false));
            Assert.Equal([expense1.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "50% r" }).ConfigureAwait(false));
            Assert.Empty(await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "50_" }).ConfigureAwait(false));
            Assert.Empty(await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "t%r" }).ConfigureAwait(false));
            Assert.Empty(await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "leave请" }).ConfigureAwait(false));

            // [ 与 \ 也按字面（SQL Server 的 [ 字符类、PostgreSQL 与 MySQL 的 LIKE 缺省转义符都不起作用，以 \ 结尾的关键字照常查询）；
            // 全角的 ％ ＿ ［ ＼ 与半角是不同的字符，互不匹配（ADR-0099 第四节撤销了第八批的全角替换）
            Assert.Equal([leave.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = @"C:\HR\[" }).ConfigureAwait(false));
            Assert.Equal([leave.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "[leave]" }).ConfigureAwait(false));
            Assert.Equal([leave.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = @"c:\" }).ConfigureAwait(false));
            Assert.Empty(await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = @"hr\\[" }).ConfigureAwait(false));
            Assert.Empty(await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "［leave］" }).ConfigureAwait(false));
            Assert.Empty(await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "＼hr" }).ConfigureAwait(false));
            Assert.Equal([purchase.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "10％" }).ConfigureAwait(false));
            Assert.Empty(await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "10%" }).ConfigureAwait(false));
            Assert.Empty(await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "50％" }).ConfigureAwait(false));
            Assert.Empty(await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "travel＿costs" }).ConfigureAwait(false));

            // 名字、分类、状态精确过滤，与关键字取交集
            Assert.Equal([expense2.Id, expense1.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Name = new WorkflowName("conf-q-expense") }).ConfigureAwait(false));
            Assert.Equal([expense2.Id, expense1.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Category = "finance" }).ConfigureAwait(false));
            Assert.Empty(await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Category = "fin" }).ConfigureAwait(false));
            Assert.Equal([expense1.Id, leave.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Status = WorkflowDefinitionStatus.Published }).ConfigureAwait(false));
            Assert.Equal([expense1.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "expense", Status = WorkflowDefinitionStatus.Published }).ConfigureAwait(false));
            Assert.Equal(1, await DefinitionCountAsync(s, new WorkflowDefinitionQuery { Category = "hr", Status = WorkflowDefinitionStatus.Published }).ConfigureAwait(false));

            // 分页：总数不看分页；越过末尾为空
            Assert.Equal([expense1.Id, leave.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Skip = 1, Limit = 2 }).ConfigureAwait(false));
            Assert.Equal(4, await DefinitionCountAsync(s, new WorkflowDefinitionQuery { Skip = 1, Limit = 2 }).ConfigureAwait(false));
            Assert.Empty(await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Skip = 4 }).ConfigureAwait(false));

            // 摘要带列表要的字段，不带整份文档
            var summary = Assert.Single((await s.Definitions.ListSummariesAsync(null, new WorkflowDefinitionQuery { Name = new WorkflowName("conf-q-expense"), Status = WorkflowDefinitionStatus.Published }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
            Assert.Equal((expense1.Id, "conf-q-expense", 1, WorkflowDefinitionStatus.Published, "Expense Claim"), (summary.Id, summary.Name.Value, summary.Version, summary.Status, summary.Title));
            Assert.Equal("Expense Claim", summary.DisplayName);
            Assert.Equal("Reimburse travel_costs at 50% rate", summary.Description);
            Assert.Equal("finance", summary.Category);
            Assert.Null(summary.TenantId);
            Assert.Equal(expense1.CreatedAt, summary.CreatedAt);
            Assert.Equal("conf-q-purchase", Assert.Single((await s.Definitions.ListSummariesAsync(null, new WorkflowDefinitionQuery { Keyword = "equipment" }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow()).Title);
        }).ConfigureAwait(false);

        // 整体替换后按新内容匹配
        await host.InPartitionAsync(null, async s => Assert.True((await s.Definitions.SaveAsync(null, [leave with { DisplayName = "Leave Request", Category = "people" }], CancellationToken.None).ConfigureAwait(false)).IsSuccess)).ConfigureAwait(false);
        await host.InPartitionAsync(null, async s =>
        {
            Assert.Equal([leave.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "request" }).ConfigureAwait(false));
            Assert.Empty(await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Keyword = "请假" }).ConfigureAwait(false));
            Assert.Equal([leave.Id], await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Category = "people" }).ConfigureAwait(false));
            Assert.Empty(await DefinitionIdsAsync(s, new WorkflowDefinitionQuery { Category = "hr" }).ConfigureAwait(false));
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// 定义查询的定义 ID。
    /// </summary>
    private static async Task<List<string>> DefinitionIdsAsync(WorkflowStores stores, WorkflowDefinitionQuery query)
        => (await stores.Definitions.ListSummariesAsync(stores.TenantId, query, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static summary => summary.Id).ToList();

    /// <summary>
    /// 定义查询的总数。
    /// </summary>
    private static async Task<long> DefinitionCountAsync(WorkflowStores stores, WorkflowDefinitionQuery query)
        => (await stores.Definitions.CountAsync(stores.TenantId, query, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();

    /// <summary>
    /// 待办查询的书签 ID。
    /// </summary>
    private static async Task<List<string>> TaskIdsAsync(WorkflowStores stores, UserTaskQuery query)
        => (await stores.Bookmarks.ListUserTasksAsync(stores.TenantId, query, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static bookmark => bookmark.Id).ToList();

    /// <summary>
    /// 待办查询的总数。
    /// </summary>
    private static async Task<long> TaskCountAsync(WorkflowStores stores, UserTaskQuery query)
        => (await stores.Bookmarks.CountUserTasksAsync(stores.TenantId, query, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();

    /// <summary>
    /// 实例列表的 ID。
    /// </summary>
    private static async Task<List<string>> IdsAsync(WorkflowStores stores, WorkflowInstanceQuery query)
        => (await stores.Instances.ListAsync(stores.TenantId, query, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static instance => instance.Id).ToList();

    /// <summary>
    /// 主题 <c>pay</c> 的外部任务书签里可领取的 ID。
    /// </summary>
    private static async Task<List<string>> AvailableAsync(WorkflowStores stores, DateTimeOffset now, int limit)
        => (await stores.Bookmarks.AvailableAsync(stores.TenantId, BookmarkKinds.External, "pay", now, limit, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static bookmark => bookmark.Id).ToList();

    /// <summary>
    /// 一个最小的定义（平台分区）。
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
    /// 一个运行中的实例（平台分区）。
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
    /// 一个书签（平台分区）。
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
