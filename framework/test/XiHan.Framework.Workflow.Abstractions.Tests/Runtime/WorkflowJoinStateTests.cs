// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 汇聚网关波次状态模型测试
/// </summary>
/// <remarks>
/// 到达集合刻意用 HashSet：同一条入边在一个波次里重复到达必须被吞掉，否则 WaitAll 会被单条分支
/// 反复触发而提前汇聚。集合语义和 JSON 往返后仍是集合，两点都要锁死。
/// </remarks>
public class WorkflowJoinStateTests
{
    /// <summary>
    /// 新建波次状态为空且未触发
    /// </summary>
    [Fact]
    public void Defaults_OnNewInstance_AreEmptyAndNotFired()
    {
        var state = new WorkflowJoinState();

        Assert.Empty(state.ArrivedTransitionIds);
        Assert.False(state.Fired);
    }

    /// <summary>
    /// 同一入边重复到达只计一次
    /// </summary>
    [Fact]
    public void ArrivedTransitionIds_WithDuplicateArrival_CountsOnce()
    {
        var state = new WorkflowJoinState();

        Assert.True(state.ArrivedTransitionIds.Add("t1"));
        Assert.False(state.ArrivedTransitionIds.Add("t1"));
        Assert.True(state.ArrivedTransitionIds.Add("t2"));

        Assert.Equal(2, state.ArrivedTransitionIds.Count);
    }

    /// <summary>
    /// 不同波次状态实例之间的集合互相独立
    /// </summary>
    [Fact]
    public void ArrivedTransitionIds_OnDistinctInstances_AreNotShared()
    {
        var first = new WorkflowJoinState();
        var second = new WorkflowJoinState();

        first.ArrivedTransitionIds.Add("t1");

        Assert.Empty(second.ArrivedTransitionIds);
        Assert.NotSame(first.ArrivedTransitionIds, second.ArrivedTransitionIds);
    }

    /// <summary>
    /// JSON 往返后仍是去重集合而不是可重复列表
    /// </summary>
    [Fact]
    public void JsonRoundTrip_KeepsSetSemantics()
    {
        var state = new WorkflowJoinState { ArrivedTransitionIds = { "t1", "t2" }, Fired = true };

        var restored = JsonSerializer.Deserialize<WorkflowJoinState>(JsonSerializer.Serialize(state));

        Assert.NotNull(restored);
        Assert.True(restored.Fired);
        Assert.Equal(2, restored.ArrivedTransitionIds.Count);
        Assert.False(restored.ArrivedTransitionIds.Add("t1"));
        Assert.Equal(2, restored.ArrivedTransitionIds.Count);
    }

    /// <summary>
    /// 未触发的波次状态往返后仍未触发
    /// </summary>
    [Fact]
    public void JsonRoundTrip_NotFired_StaysFalse()
    {
        var restored = JsonSerializer.Deserialize<WorkflowJoinState>(JsonSerializer.Serialize(new WorkflowJoinState()));

        Assert.NotNull(restored);
        Assert.False(restored.Fired);
        Assert.Empty(restored.ArrivedTransitionIds);
    }
}
