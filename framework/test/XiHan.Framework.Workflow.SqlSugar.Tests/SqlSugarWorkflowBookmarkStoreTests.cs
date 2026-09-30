// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.SqlSugar.Entities;
using XiHan.Framework.Workflow.SqlSugar.Stores;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 流程书签存储测试
/// </summary>
public class SqlSugarWorkflowBookmarkStoreTests : IDisposable
{
    private static readonly DateTime BaseTime = new(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);

    private readonly WorkflowTestDatabase _database = WorkflowTestDatabase.CreateSqlite();
    private readonly SqlSugarWorkflowBookmarkStore _store;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SqlSugarWorkflowBookmarkStoreTests()
    {
        _store = new SqlSugarWorkflowBookmarkStore(_database.Executor);
    }

    /// <summary>
    /// 插入后按标识可查到
    /// </summary>
    [Fact]
    public async Task 插入后按标识可查到()
    {
        var bookmark = NewBookmark("b1", WorkflowBookmarkKinds.UserTask, "u1");
        bookmark.Payload["title"] = "审批";

        await _store.InsertAsync(bookmark);
        var found = await _store.FindAsync("b1");

        Assert.NotNull(found);
        Assert.Equal("u1", found.Key);
        Assert.Equal("审批", WorkflowValueConverter.ConvertTo<string>(found.Payload["title"]));
        Assert.Null(await _store.FindAsync("missing"));
    }

    /// <summary>
    /// 按实例与节点实例查询按创建时间升序
    /// </summary>
    [Fact]
    public async Task 按实例与节点实例查询按创建时间升序()
    {
        var later = NewBookmark("b2", WorkflowBookmarkKinds.Timer, null, creationTime: BaseTime.AddSeconds(1));
        var earlier = NewBookmark("b1", WorkflowBookmarkKinds.Timer, null);
        var otherNode = NewBookmark("b3", WorkflowBookmarkKinds.Timer, null, nodeInstanceId: "n2");
        var otherInstance = NewBookmark("b4", WorkflowBookmarkKinds.Timer, null, instanceId: "i2", nodeInstanceId: "n9");
        await _store.InsertAsync(later);
        await _store.InsertAsync(earlier);
        await _store.InsertAsync(otherNode);
        await _store.InsertAsync(otherInstance);

        var byInstance = await _store.GetByInstanceAsync("i1");
        var byNodeInstance = await _store.GetByNodeInstanceAsync("n1");

        Assert.Equal(["b1", "b3", "b2"], byInstance.Select(item => item.Id));
        Assert.Equal(["b1", "b2"], byNodeInstance.Select(item => item.Id));
    }

    /// <summary>
    /// 到期查询只含已到期书签并按到期时间升序截取
    /// </summary>
    [Fact]
    public async Task 到期查询只含已到期书签并按到期时间升序截取()
    {
        await _store.InsertAsync(NewBookmark("due-late", WorkflowBookmarkKinds.Timer, null, dueTime: BaseTime.AddSeconds(-10)));
        await _store.InsertAsync(NewBookmark("due-early", WorkflowBookmarkKinds.Retry, null, dueTime: BaseTime.AddSeconds(-20)));
        await _store.InsertAsync(NewBookmark("due-now", WorkflowBookmarkKinds.NodeTimeout, null, dueTime: BaseTime));
        await _store.InsertAsync(NewBookmark("future", WorkflowBookmarkKinds.Timer, null, dueTime: BaseTime.AddSeconds(1)));
        await _store.InsertAsync(NewBookmark("no-due", WorkflowBookmarkKinds.UserTask, "u1"));

        var all = await _store.GetDueAsync(BaseTime, 10);
        var limited = await _store.GetDueAsync(BaseTime, 2);

        Assert.Equal(["due-early", "due-late", "due-now"], all.Select(item => item.Id));
        Assert.Equal(["due-early", "due-late"], limited.Select(item => item.Id));
        Assert.Empty(await _store.GetDueAsync(BaseTime, 0));
    }

    /// <summary>
    /// 按种类和索引键查询
    /// </summary>
    [Fact]
    public async Task 按种类和索引键查询()
    {
        await _store.InsertAsync(NewBookmark("b2", WorkflowBookmarkKinds.UserTask, "u1", creationTime: BaseTime.AddSeconds(1)));
        await _store.InsertAsync(NewBookmark("b1", WorkflowBookmarkKinds.UserTask, "u1"));
        await _store.InsertAsync(NewBookmark("b3", WorkflowBookmarkKinds.UserTask, "u2"));
        await _store.InsertAsync(NewBookmark("b4", WorkflowBookmarkKinds.Signal, "u1"));

        var tasks = await _store.GetByKindAndKeyAsync(WorkflowBookmarkKinds.UserTask, "u1");

        Assert.Equal(["b1", "b2"], tasks.Select(item => item.Id));
    }

    /// <summary>
    /// 信号定向匹配不限相关性与相关性相同的书签
    /// </summary>
    [Fact]
    public async Task 信号定向匹配不限相关性与相关性相同的书签()
    {
        await SeedSignalBookmarksAsync();

        var matched = await _store.GetBySignalAsync("paid", "A");

        Assert.Equal(["any", "a"], matched.Select(item => item.Id));
    }

    /// <summary>
    /// 信号相关性为空值时广播
    /// </summary>
    [Fact]
    public async Task 信号相关性为空值时广播()
    {
        await SeedSignalBookmarksAsync();

        var matched = await _store.GetBySignalAsync("paid", null);

        Assert.Equal(["any", "a", "b", "empty"], matched.Select(item => item.Id));
    }

    /// <summary>
    /// 信号相关性为空串时不是广播
    /// </summary>
    [Fact]
    public async Task 信号相关性为空串时不是广播()
    {
        await SeedSignalBookmarksAsync();

        var matched = await _store.GetBySignalAsync("paid", string.Empty);

        Assert.Equal(["any", "empty"], matched.Select(item => item.Id));
    }

    /// <summary>
    /// 更新改写索引键与到期时间
    /// </summary>
    [Fact]
    public async Task 更新改写索引键与到期时间()
    {
        var bookmark = NewBookmark("b1", WorkflowBookmarkKinds.UserTask, "u1", dueTime: BaseTime);
        await _store.InsertAsync(bookmark);

        bookmark.Key = "u2";
        bookmark.DueTime = null;
        await _store.UpdateAsync(bookmark);

        var found = await _store.FindAsync("b1");
        Assert.NotNull(found);
        Assert.Equal("u2", found.Key);
        Assert.Null(found.DueTime);
        Assert.Empty(await _store.GetByKindAndKeyAsync(WorkflowBookmarkKinds.UserTask, "u1"));
    }

    /// <summary>
    /// 更新不存在的书签不新建行
    /// </summary>
    [Fact]
    public async Task 更新不存在的书签不新建行()
    {
        await _store.UpdateAsync(NewBookmark("ghost", WorkflowBookmarkKinds.Timer, null, dueTime: BaseTime));

        using var probe = _database.CreateProbeClient();
        Assert.Equal(0, probe.Queryable<SysWorkflowBookmark>().Count());
    }

    /// <summary>
    /// 删除单个书签与删除实例的全部书签
    /// </summary>
    [Fact]
    public async Task 删除单个书签与删除实例的全部书签()
    {
        await _store.InsertAsync(NewBookmark("b1", WorkflowBookmarkKinds.Timer, null));
        await _store.InsertAsync(NewBookmark("b2", WorkflowBookmarkKinds.Timer, null));
        await _store.InsertAsync(NewBookmark("b3", WorkflowBookmarkKinds.Timer, null, instanceId: "i2"));

        await _store.DeleteAsync("b1");
        Assert.Null(await _store.FindAsync("b1"));

        await _store.DeleteByInstanceAsync("i1");
        Assert.Empty(await _store.GetByInstanceAsync("i1"));
        Assert.Equal("b3", Assert.Single(await _store.GetByInstanceAsync("i2")).Id);
    }

    /// <summary>
    /// 删除不存在或已被删除的书签抛出工作流异常
    /// </summary>
    [Fact]
    public async Task 删除不存在或已被删除的书签抛出工作流异常()
    {
        await _store.InsertAsync(NewBookmark("b1", WorkflowBookmarkKinds.Timer, null));
        await _store.DeleteAsync("b1");

        await Assert.ThrowsAsync<WorkflowException>(() => _store.DeleteAsync("b1"));
        await Assert.ThrowsAsync<WorkflowException>(() => _store.DeleteAsync("missing"));
    }

    /// <summary>
    /// 删除没有书签的实例不抛异常
    /// </summary>
    [Fact]
    public async Task 删除没有书签的实例不抛异常()
    {
        await _store.DeleteByInstanceAsync("no-bookmarks");

        Assert.Empty(await _store.GetByInstanceAsync("no-bookmarks"));
    }

    /// <summary>
    /// 释放测试夹具
    /// </summary>
    public void Dispose()
    {
        _database.Dispose();
    }

    private async Task SeedSignalBookmarksAsync()
    {
        await _store.InsertAsync(NewBookmark("any", WorkflowBookmarkKinds.Signal, "paid", correlationId: null));
        await _store.InsertAsync(NewBookmark("a", WorkflowBookmarkKinds.Signal, "paid", correlationId: "A", creationTime: BaseTime.AddSeconds(1)));
        await _store.InsertAsync(NewBookmark("b", WorkflowBookmarkKinds.Signal, "paid", correlationId: "B", creationTime: BaseTime.AddSeconds(2)));
        await _store.InsertAsync(NewBookmark("empty", WorkflowBookmarkKinds.Signal, "paid", correlationId: string.Empty, creationTime: BaseTime.AddSeconds(3)));
        await _store.InsertAsync(NewBookmark("other-signal", WorkflowBookmarkKinds.Signal, "shipped", correlationId: null));
        await _store.InsertAsync(NewBookmark("not-signal", WorkflowBookmarkKinds.UserTask, "paid", correlationId: null));
    }

    private static WorkflowBookmark NewBookmark(
        string id,
        string kind,
        string? key,
        string instanceId = "i1",
        string nodeInstanceId = "n1",
        DateTime? dueTime = null,
        string? correlationId = null,
        DateTime? creationTime = null)
    {
        return new WorkflowBookmark
        {
            Id = id,
            InstanceId = instanceId,
            NodeId = "node",
            NodeInstanceId = nodeInstanceId,
            Kind = kind,
            Key = key,
            DueTime = dueTime,
            CorrelationId = correlationId,
            CreationTime = creationTime ?? BaseTime
        };
    }
}
