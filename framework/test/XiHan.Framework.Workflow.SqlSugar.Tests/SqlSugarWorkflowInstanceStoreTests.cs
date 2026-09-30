// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Expressions;
using XiHan.Framework.Workflow.SqlSugar.Entities;
using XiHan.Framework.Workflow.SqlSugar.Stores;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 流程实例存储测试
/// </summary>
public class SqlSugarWorkflowInstanceStoreTests : IDisposable
{
    private static readonly DateTime BaseTime = new(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);

    private readonly WorkflowTestDatabase _database = WorkflowTestDatabase.CreateSqlite();
    private readonly SqlSugarWorkflowInstanceStore _store;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SqlSugarWorkflowInstanceStoreTests()
    {
        _store = new SqlSugarWorkflowInstanceStore(_database.Executor);
    }

    /// <summary>
    /// 插入后按标识可查到
    /// </summary>
    [Fact]
    public async Task 插入后按标识可查到()
    {
        var instance = NewInstance("i1", BaseTime);
        instance.Variables["amount"] = 88;

        await _store.InsertAsync(instance);
        var found = await _store.FindAsync("i1");

        Assert.NotNull(found);
        Assert.Equal("leave", found.DefinitionCode);
        Assert.Equal(88, new WorkflowVariables(found.Variables).Get<int>("amount"));
        Assert.Null(await _store.FindAsync("missing"));
    }

    /// <summary>
    /// 变量中的对象值读回后属性名保持原样并可被表达式求值
    /// </summary>
    [Fact]
    public async Task 变量中的对象值读回后属性名保持原样并可被表达式求值()
    {
        var instance = NewInstance("i1", BaseTime);
        instance.Variables["order"] = new OrderVariable { Amount = 100 };
        instance.Variables["byKey"] = new Dictionary<string, object?> { ["Mixed"] = 1 };

        await _store.InsertAsync(instance);
        var found = await _store.FindAsync("i1");

        Assert.NotNull(found);
        var evaluator = new WorkflowExpressionEvaluator(new TestClock());
        Assert.True(await evaluator.EvaluateConditionAsync("order.Amount > 50", found.Variables));
        Assert.True(await evaluator.EvaluateConditionAsync("byKey.Mixed == 1", found.Variables));
    }

    /// <summary>
    /// 按定义编码过滤按序数比较并在过滤后截取条数
    /// </summary>
    [Fact]
    public async Task 按定义编码过滤按序数比较并在过滤后截取条数()
    {
        var lower = NewInstance("i1", BaseTime);
        var upper = NewInstance("i2", BaseTime.AddMinutes(1));
        upper.DefinitionCode = "Leave";
        var lowerNewer = NewInstance("i3", BaseTime.AddMinutes(2));
        await _store.InsertAsync(lower);
        await _store.InsertAsync(upper);
        await _store.InsertAsync(lowerNewer);

        var found = await _store.GetListAsync(definitionCode: "leave", maxResultCount: 2);

        Assert.Equal(["i3", "i1"], found.Select(item => item.Id));
    }

    /// <summary>
    /// 按定义编码过滤时带分页限制读取而不是一次读全部
    /// </summary>
    /// <remarks>
    /// SQLite 的等值比较区分大小写，数据库端已排除大小写不同的编码，凑满条数后只发出一条带 LIMIT 的查询。
    /// </remarks>
    [Fact]
    public async Task 按定义编码过滤时带分页限制读取而不是一次读全部()
    {
        for (var index = 1; index <= 10; index++)
        {
            var instance = NewInstance("i" + index, BaseTime.AddMinutes(index));
            if (index % 2 == 0)
            {
                instance.DefinitionCode = "Leave";
            }

            await _store.InsertAsync(instance);
        }

        var selects = new List<string>();
        _database.Scope.Aop.OnLogExecuting = (sql, _) =>
        {
            if (sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                selects.Add(sql);
            }
        };

        var found = await _store.GetListAsync(definitionCode: "leave", maxResultCount: 2);

        Assert.Equal(["i9", "i7"], found.Select(item => item.Id));
        var select = Assert.Single(selects);
        Assert.Contains("LIMIT", select, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 按定义编码过滤时匹配行不足条数则读完所有分页
    /// </summary>
    [Fact]
    public async Task 按定义编码过滤时匹配行不足条数则读完所有分页()
    {
        for (var index = 1; index <= 7; index++)
        {
            var instance = NewInstance("i" + index, BaseTime.AddMinutes(index));
            if (index != 4)
            {
                instance.DefinitionCode = "Leave";
            }

            await _store.InsertAsync(instance);
        }

        var found = await _store.GetListAsync(definitionCode: "leave", maxResultCount: 3);

        Assert.Equal(["i4"], found.Select(item => item.Id));
    }

    /// <summary>
    /// 无定义编码条件时按条数上限截取
    /// </summary>
    [Fact]
    public async Task 无定义编码条件时按条数上限截取()
    {
        for (var index = 1; index <= 5; index++)
        {
            await _store.InsertAsync(NewInstance("i" + index, BaseTime.AddMinutes(index)));
        }

        var found = await _store.GetListAsync(maxResultCount: 3);

        Assert.Equal(["i5", "i4", "i3"], found.Select(item => item.Id));
    }

    /// <summary>
    /// 每次查找返回新对象
    /// </summary>
    [Fact]
    public async Task 每次查找返回新对象()
    {
        await _store.InsertAsync(NewInstance("i1", BaseTime));

        var first = await _store.FindAsync("i1");
        var second = await _store.FindAsync("i1");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
    }

    /// <summary>
    /// 列表按条件过滤并按创建时间降序截取
    /// </summary>
    [Fact]
    public async Task 列表按条件过滤并按创建时间降序截取()
    {
        var older = NewInstance("i1", BaseTime);
        var newer = NewInstance("i2", BaseTime.AddMinutes(1));
        var other = NewInstance("i3", BaseTime.AddMinutes(2));
        other.DefinitionCode = "expense";
        other.Status = WorkflowInstanceStatus.Completed;
        other.CorrelationId = "ORDER-9";
        await _store.InsertAsync(older);
        await _store.InsertAsync(newer);
        await _store.InsertAsync(other);

        var all = await _store.GetListAsync();
        Assert.Equal(["i3", "i2", "i1"], all.Select(item => item.Id));

        var running = await _store.GetListAsync(status: WorkflowInstanceStatus.Running);
        Assert.Equal(["i2", "i1"], running.Select(item => item.Id));

        var leave = await _store.GetListAsync(definitionCode: "leave", maxResultCount: 1);
        Assert.Equal("i2", Assert.Single(leave).Id);

        var correlated = await _store.GetListAsync(correlationId: "ORDER-9");
        Assert.Equal("i3", Assert.Single(correlated).Id);

        Assert.Empty(await _store.GetListAsync(maxResultCount: 0));
    }

    /// <summary>
    /// 子实例按创建时间升序
    /// </summary>
    [Fact]
    public async Task 子实例按创建时间升序()
    {
        var later = NewInstance("c2", BaseTime.AddSeconds(2));
        later.ParentInstanceId = "p1";
        var earlier = NewInstance("c1", BaseTime.AddSeconds(1));
        earlier.ParentInstanceId = "p1";
        var unrelated = NewInstance("c3", BaseTime);
        unrelated.ParentInstanceId = "p2";
        await _store.InsertAsync(later);
        await _store.InsertAsync(earlier);
        await _store.InsertAsync(unrelated);

        var children = await _store.GetChildrenAsync("p1");

        Assert.Equal(["c1", "c2"], children.Select(item => item.Id));
    }

    /// <summary>
    /// 更新能把可空字段写回空值
    /// </summary>
    [Fact]
    public async Task 更新能把可空字段写回空值()
    {
        var instance = NewInstance("i1", BaseTime);
        instance.Status = WorkflowInstanceStatus.Faulted;
        instance.EndTime = BaseTime.AddMinutes(1);
        instance.FaultMessage = "故障";
        instance.FaultNodeId = "n1";
        instance.FaultNodeInstanceId = "ni1";
        await _store.InsertAsync(instance);

        instance.Status = WorkflowInstanceStatus.Running;
        instance.EndTime = null;
        instance.FaultMessage = null;
        instance.FaultNodeId = null;
        instance.FaultNodeInstanceId = null;
        instance.JoinStates["join"] = new WorkflowJoinState { ArrivedTransitionIds = ["t1"] };
        await _store.UpdateAsync(instance);

        var found = await _store.FindAsync("i1");
        Assert.NotNull(found);
        Assert.Equal(WorkflowInstanceStatus.Running, found.Status);
        Assert.Null(found.EndTime);
        Assert.Null(found.FaultMessage);
        Assert.Null(found.FaultNodeId);
        Assert.Null(found.FaultNodeInstanceId);
        Assert.Contains("t1", found.JoinStates["join"].ArrivedTransitionIds);
    }

    /// <summary>
    /// 更新不存在的实例不新建行
    /// </summary>
    [Fact]
    public async Task 更新不存在的实例不新建行()
    {
        await _store.UpdateAsync(NewInstance("ghost", BaseTime));

        using var probe = _database.CreateProbeClient();
        Assert.Equal(0, probe.Queryable<SysWorkflowInstance>().Count());
    }

    /// <summary>
    /// 删除实例级联删除其节点实例
    /// </summary>
    [Fact]
    public async Task 删除实例级联删除其节点实例()
    {
        await _store.InsertAsync(NewInstance("i1", BaseTime));
        await _store.InsertAsync(NewInstance("i2", BaseTime));
        await _store.InsertNodeInstanceAsync(NewNodeInstance("101", "i1", BaseTime));
        await _store.InsertNodeInstanceAsync(NewNodeInstance("102", "i1", BaseTime));
        await _store.InsertNodeInstanceAsync(NewNodeInstance("201", "i2", BaseTime));

        await _store.DeleteAsync("i1");

        Assert.Null(await _store.FindAsync("i1"));
        Assert.Empty(await _store.GetNodeInstancesAsync("i1"));
        Assert.NotNull(await _store.FindAsync("i2"));
        Assert.Equal("201", Assert.Single(await _store.GetNodeInstancesAsync("i2")).Id);
    }

    /// <summary>
    /// 节点实例插入后可查到并可更新私有状态
    /// </summary>
    [Fact]
    public async Task 节点实例插入后可查到并可更新私有状态()
    {
        var nodeInstance = NewNodeInstance("101", "i1", BaseTime);
        await _store.InsertNodeInstanceAsync(nodeInstance);

        nodeInstance.Status = WorkflowNodeInstanceStatus.Suspended;
        nodeInstance.State["assignees"] = new List<string> { "u1", "u2" };
        await _store.UpdateNodeInstanceAsync(nodeInstance);

        var found = await _store.FindNodeInstanceAsync("101");
        Assert.NotNull(found);
        Assert.Equal(WorkflowNodeInstanceStatus.Suspended, found.Status);
        Assert.Equal(["u1", "u2"], WorkflowValueConverter.ConvertTo<List<string>>(found.State["assignees"]));
        Assert.Null(await _store.FindNodeInstanceAsync("missing"));
    }

    /// <summary>
    /// 执行历史同秒按创建顺序而非字符串标识排序
    /// </summary>
    [Fact]
    public async Task 执行历史同秒按创建顺序而非字符串标识排序()
    {
        await _store.InsertNodeInstanceAsync(NewNodeInstance("10", "i1", BaseTime));
        await _store.InsertNodeInstanceAsync(NewNodeInstance("9", "i1", BaseTime));
        await _store.InsertNodeInstanceAsync(NewNodeInstance("1", "i1", BaseTime.AddSeconds(1)));
        await _store.InsertNodeInstanceAsync(NewNodeInstance("5", "i2", BaseTime));

        var history = await _store.GetNodeInstancesAsync("i1");

        Assert.Equal(["9", "10", "1"], history.Select(item => item.Id));
    }

    /// <summary>
    /// 释放测试夹具
    /// </summary>
    public void Dispose()
    {
        _database.Dispose();
    }

    private static WorkflowInstance NewInstance(string id, DateTime creationTime)
    {
        return new WorkflowInstance
        {
            Id = id,
            DefinitionId = "d1",
            DefinitionCode = "leave",
            DefinitionVersion = 1,
            Name = "请假",
            Status = WorkflowInstanceStatus.Running,
            CreationTime = creationTime,
            StartTime = creationTime
        };
    }

    private static WorkflowNodeInstance NewNodeInstance(string id, string instanceId, DateTime startTime)
    {
        return new WorkflowNodeInstance
        {
            Id = id,
            InstanceId = instanceId,
            NodeId = "n" + id,
            Name = "节点" + id,
            ActivityType = "SetVariable",
            Status = WorkflowNodeInstanceStatus.Completed,
            TryCount = 1,
            StartTime = startTime
        };
    }

    private sealed class OrderVariable
    {
        public decimal Amount { get; set; }
    }
}
