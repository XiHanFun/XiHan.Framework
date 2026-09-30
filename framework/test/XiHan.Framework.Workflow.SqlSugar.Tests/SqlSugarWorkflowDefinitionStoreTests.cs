// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.SqlSugar.Entities;
using XiHan.Framework.Workflow.SqlSugar.Stores;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 流程定义存储测试
/// </summary>
public class SqlSugarWorkflowDefinitionStoreTests : IDisposable
{
    private readonly WorkflowTestDatabase _database = WorkflowTestDatabase.CreateSqlite();
    private readonly SqlSugarWorkflowDefinitionStore _store;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SqlSugarWorkflowDefinitionStoreTests()
    {
        _store = new SqlSugarWorkflowDefinitionStore(_database.Executor);
    }

    /// <summary>
    /// 插入后按标识可查到
    /// </summary>
    [Fact]
    public async Task 插入后按标识可查到()
    {
        var definition = NewDefinition("leave", 1);
        definition.Nodes.Add(new WorkflowNode { Id = "start", Name = "开始", ActivityType = WorkflowActivityTypes.Start });

        await _store.InsertAsync(definition);
        var found = await _store.FindAsync(definition.Id);

        Assert.NotNull(found);
        Assert.Equal("leave", found.Code);
        Assert.Equal("start", Assert.Single(found.Nodes).Id);
    }

    /// <summary>
    /// 标识不存在时返回空
    /// </summary>
    [Fact]
    public async Task 标识不存在时返回空()
    {
        Assert.Null(await _store.FindAsync("missing"));
    }

    /// <summary>
    /// 按编码与版本查找
    /// </summary>
    [Fact]
    public async Task 按编码与版本查找()
    {
        await _store.InsertAsync(NewDefinition("leave", 1));
        var second = NewDefinition("leave", 2);
        await _store.InsertAsync(second);

        var found = await _store.FindByVersionAsync("leave", 2);

        Assert.NotNull(found);
        Assert.Equal(second.Id, found.Id);
        Assert.Null(await _store.FindByVersionAsync("leave", 3));
    }

    /// <summary>
    /// 最新已发布版本忽略更高的草稿
    /// </summary>
    [Fact]
    public async Task 最新已发布版本忽略更高的草稿()
    {
        await _store.InsertAsync(NewDefinition("leave", 1, WorkflowDefinitionStatus.Published));
        var published = NewDefinition("leave", 2, WorkflowDefinitionStatus.Published);
        await _store.InsertAsync(published);
        await _store.InsertAsync(NewDefinition("leave", 3, WorkflowDefinitionStatus.Draft));
        await _store.InsertAsync(NewDefinition("other", 9, WorkflowDefinitionStatus.Published));

        var latest = await _store.FindLatestPublishedAsync("leave");

        Assert.NotNull(latest);
        Assert.Equal(published.Id, latest.Id);
        Assert.Null(await _store.FindLatestPublishedAsync("missing"));
    }

    /// <summary>
    /// 编码不存在时最大版本为零
    /// </summary>
    [Fact]
    public async Task 编码不存在时最大版本为零()
    {
        Assert.Equal(0, await _store.GetMaxVersionAsync("missing"));
    }

    /// <summary>
    /// 最大版本取编码下的最大值
    /// </summary>
    [Fact]
    public async Task 最大版本取编码下的最大值()
    {
        await _store.InsertAsync(NewDefinition("leave", 1));
        await _store.InsertAsync(NewDefinition("leave", 5));
        await _store.InsertAsync(NewDefinition("other", 9));

        Assert.Equal(5, await _store.GetMaxVersionAsync("leave"));
    }

    /// <summary>
    /// 取最大版本时不读回节点等大字段
    /// </summary>
    [Fact]
    public async Task 取最大版本时不读回节点等大字段()
    {
        await _store.InsertAsync(NewDefinition("leave", 1));
        var selects = new List<string>();
        _database.Scope.Aop.OnLogExecuting = (sql, _) =>
        {
            if (sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                selects.Add(sql);
            }
        };

        await _store.GetMaxVersionAsync("leave");

        var select = Assert.Single(selects);
        Assert.DoesNotContain("Nodes_Json", select, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Transitions_Json", select, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 列表按编码升序版本降序并按条件过滤
    /// </summary>
    [Fact]
    public async Task 列表按编码升序版本降序并按条件过滤()
    {
        await _store.InsertAsync(NewDefinition("b", 1, WorkflowDefinitionStatus.Published));
        await _store.InsertAsync(NewDefinition("a", 1, WorkflowDefinitionStatus.Published));
        await _store.InsertAsync(NewDefinition("a", 2, WorkflowDefinitionStatus.Draft));

        var all = await _store.GetListAsync();
        Assert.Equal(["a:2", "a:1", "b:1"], all.Select(item => $"{item.Code}:{item.Version}"));

        var onlyA = await _store.GetListAsync(code: "a");
        Assert.Equal([2, 1], onlyA.Select(item => item.Version));

        var published = await _store.GetListAsync(status: WorkflowDefinitionStatus.Published);
        Assert.Equal(["a:1", "b:1"], published.Select(item => $"{item.Code}:{item.Version}"));
    }

    /// <summary>
    /// 编码查询按序数比较不受大小写相近编码影响
    /// </summary>
    [Fact]
    public async Task 编码查询按序数比较不受大小写相近编码影响()
    {
        var upper = NewDefinition("Leave", 2, WorkflowDefinitionStatus.Published);
        var lower = NewDefinition("leave", 1, WorkflowDefinitionStatus.Published);
        await _store.InsertAsync(upper);
        await _store.InsertAsync(lower);

        var latest = await _store.FindLatestPublishedAsync("leave");
        Assert.NotNull(latest);
        Assert.Equal(lower.Id, latest.Id);
        Assert.Equal(1, await _store.GetMaxVersionAsync("leave"));
        Assert.Equal(2, await _store.GetMaxVersionAsync("Leave"));
        Assert.Null(await _store.FindByVersionAsync("leave", 2));
        Assert.Equal(lower.Id, (await _store.FindByVersionAsync("leave", 1))!.Id);
        Assert.Equal([1], (await _store.GetListAsync(code: "leave")).Select(item => item.Version));
    }

    /// <summary>
    /// 更新写回全部字段包括清空的可空字段
    /// </summary>
    [Fact]
    public async Task 更新写回全部字段包括清空的可空字段()
    {
        var definition = NewDefinition("leave", 1, WorkflowDefinitionStatus.Published);
        definition.Description = "旧描述";
        await _store.InsertAsync(definition);

        definition.Status = WorkflowDefinitionStatus.Disabled;
        definition.Description = null;
        definition.PublishTime = null;
        definition.Nodes.Add(new WorkflowNode { Id = "end", Name = "结束", ActivityType = WorkflowActivityTypes.End });
        await _store.UpdateAsync(definition);

        var found = await _store.FindAsync(definition.Id);
        Assert.NotNull(found);
        Assert.Equal(WorkflowDefinitionStatus.Disabled, found.Status);
        Assert.Null(found.Description);
        Assert.Null(found.PublishTime);
        Assert.Equal("end", Assert.Single(found.Nodes).Id);
    }

    /// <summary>
    /// 更新不存在的标识不新建行
    /// </summary>
    [Fact]
    public async Task 更新不存在的标识不新建行()
    {
        await _store.UpdateAsync(NewDefinition("ghost", 1));

        using var probe = _database.CreateProbeClient();
        Assert.Equal(0, probe.Queryable<SysWorkflowDefinition>().Count());
    }

    /// <summary>
    /// 重复主键插入抛出异常
    /// </summary>
    [Fact]
    public async Task 重复主键插入抛出异常()
    {
        var definition = NewDefinition("leave", 1);
        await _store.InsertAsync(definition);

        definition.Version = 2;
        await Assert.ThrowsAnyAsync<Exception>(() => _store.InsertAsync(definition));
    }

    /// <summary>
    /// 同编码同版本插入抛出异常
    /// </summary>
    [Fact]
    public async Task 同编码同版本插入抛出异常()
    {
        await _store.InsertAsync(NewDefinition("leave", 1));

        await Assert.ThrowsAnyAsync<Exception>(() => _store.InsertAsync(NewDefinition("leave", 1)));
    }

    /// <summary>
    /// 删除后查不到
    /// </summary>
    [Fact]
    public async Task 删除后查不到()
    {
        var definition = NewDefinition("leave", 1);
        await _store.InsertAsync(definition);

        await _store.DeleteAsync(definition.Id);

        Assert.Null(await _store.FindAsync(definition.Id));
    }

    /// <summary>
    /// 释放测试夹具
    /// </summary>
    public void Dispose()
    {
        _database.Dispose();
    }

    private static WorkflowDefinition NewDefinition(
        string code,
        int version,
        WorkflowDefinitionStatus status = WorkflowDefinitionStatus.Draft)
    {
        return new WorkflowDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Code = code,
            Name = code,
            Version = version,
            Status = status,
            CreationTime = new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc),
            PublishTime = status == WorkflowDefinitionStatus.Published
                ? new DateTime(2026, 9, 28, 9, 0, 0, DateTimeKind.Utc)
                : null
        };
    }
}
