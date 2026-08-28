// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Runtime;

/// <summary>
/// 流程实例模型测试
/// </summary>
/// <remarks>
/// <c>IsFinalStatus</c> 是抽象层里唯一被引擎、任务服务、Worker 三处共用的判定方法，
/// 漏判会让终态实例被反复恢复、误判会让运行中实例被当成已结束，因此对全部状态逐一断言。
/// </remarks>
public class WorkflowInstanceTests
{
    /// <summary>
    /// 新建实例的默认值语义
    /// </summary>
    [Fact]
    public void Defaults_OnNewInstance_AreRunningAndEmpty()
    {
        var instance = new WorkflowInstance();

        Assert.Equal(string.Empty, instance.Id);
        Assert.Equal(string.Empty, instance.DefinitionId);
        Assert.Equal(string.Empty, instance.DefinitionCode);
        Assert.Equal(0, instance.DefinitionVersion);
        Assert.Equal(string.Empty, instance.Name);
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Empty(instance.Variables);
        Assert.Empty(instance.JoinStates);
        Assert.Null(instance.CorrelationId);
        Assert.Null(instance.StarterId);
        Assert.Null(instance.ParentInstanceId);
        Assert.Null(instance.ParentNodeInstanceId);
        Assert.Equal(0, instance.Depth);
        Assert.Null(instance.TenantId);
        Assert.Equal(default(DateTime), instance.CreationTime);
        Assert.Null(instance.StartTime);
        Assert.Null(instance.EndTime);
        Assert.Null(instance.FaultMessage);
        Assert.Null(instance.FaultNodeId);
        Assert.Null(instance.FaultNodeInstanceId);
        Assert.Null(instance.CancellationReason);
    }

    /// <summary>
    /// 终态判定覆盖全部状态
    /// </summary>
    [Theory]
    [InlineData(WorkflowInstanceStatus.Running, false)]
    [InlineData(WorkflowInstanceStatus.Suspended, false)]
    [InlineData(WorkflowInstanceStatus.Completed, true)]
    [InlineData(WorkflowInstanceStatus.Canceled, true)]
    [InlineData(WorkflowInstanceStatus.Faulted, true)]
    [InlineData(WorkflowInstanceStatus.Terminated, true)]
    public void IsFinalStatus_ForEachStatus_MatchesContract(WorkflowInstanceStatus status, bool expected)
    {
        var instance = new WorkflowInstance { Status = status };

        Assert.Equal(expected, instance.IsFinalStatus());
    }

    /// <summary>
    /// 挂起不是终态，实例仍可被恢复运行
    /// </summary>
    /// <remarks>
    /// 单独强调这一条：Suspended 在语义上很像"停下来了"，一旦被误归入终态，人工挂起的实例就再也恢复不了。
    /// </remarks>
    [Fact]
    public void IsFinalStatus_WhenSuspended_IsFalse()
    {
        Assert.False(new WorkflowInstance { Status = WorkflowInstanceStatus.Suspended }.IsFinalStatus());
    }

    /// <summary>
    /// 故障是终态但保留重试入口字段
    /// </summary>
    [Fact]
    public void IsFinalStatus_WhenFaulted_IsTrueAndKeepsRetryEntryPoint()
    {
        var instance = new WorkflowInstance
        {
            Status = WorkflowInstanceStatus.Faulted,
            FaultMessage = "远端超时",
            FaultNodeId = "http",
            FaultNodeInstanceId = "ni-9"
        };

        Assert.True(instance.IsFinalStatus());
        Assert.Equal("ni-9", instance.FaultNodeInstanceId);
    }

    /// <summary>
    /// 不同实例之间的变量与波次状态互相独立
    /// </summary>
    [Fact]
    public void Collections_OnDistinctInstances_AreNotShared()
    {
        var first = new WorkflowInstance();
        var second = new WorkflowInstance();

        first.Variables["a"] = 1;
        first.JoinStates["join"] = new WorkflowJoinState();

        Assert.Empty(second.Variables);
        Assert.Empty(second.JoinStates);
    }

    /// <summary>
    /// 实例 JSON 往返保留状态、变量与汇聚波次
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesStatusVariablesAndJoinStates()
    {
        var instance = new WorkflowInstance
        {
            Id = "ins-1",
            DefinitionId = "def-1",
            DefinitionCode = "leave",
            DefinitionVersion = 2,
            Name = "张三的请假",
            Status = WorkflowInstanceStatus.Suspended,
            CorrelationId = "biz-1",
            StarterId = "u-1",
            ParentInstanceId = "ins-0",
            ParentNodeInstanceId = "ni-0",
            Depth = 1,
            TenantId = 7L,
            CreationTime = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc),
            StartTime = new DateTime(2024, 5, 6, 7, 8, 10, DateTimeKind.Utc),
            Variables = { ["amount"] = 2000, ["name"] = "张三" },
            JoinStates =
            {
                ["join"] = new WorkflowJoinState { ArrivedTransitionIds = { "t1", "t2" }, Fired = true }
            }
        };

        var restored = JsonSerializer.Deserialize<WorkflowInstance>(JsonSerializer.Serialize(instance));

        Assert.NotNull(restored);
        Assert.Equal("ins-1", restored.Id);
        Assert.Equal(2, restored.DefinitionVersion);
        Assert.Equal(WorkflowInstanceStatus.Suspended, restored.Status);
        Assert.Equal("biz-1", restored.CorrelationId);
        Assert.Equal("u-1", restored.StarterId);
        Assert.Equal("ins-0", restored.ParentInstanceId);
        Assert.Equal("ni-0", restored.ParentNodeInstanceId);
        Assert.Equal(1, restored.Depth);
        Assert.Equal(7L, restored.TenantId);
        Assert.Equal(instance.CreationTime, restored.CreationTime);
        Assert.Equal(instance.StartTime, restored.StartTime);
        Assert.Null(restored.EndTime);

        // 变量是弱类型字典，往返后必然是 JsonElement，读取方必须经值转换器归一化
        Assert.Equal(2000m, WorkflowValueConverter.Normalize(restored.Variables["amount"]));
        Assert.Equal("张三", WorkflowValueConverter.Normalize(restored.Variables["name"]));

        var joinState = restored.JoinStates["join"];
        Assert.True(joinState.Fired);
        Assert.Equal(2, joinState.ArrivedTransitionIds.Count);
        Assert.Contains("t1", joinState.ArrivedTransitionIds);
        Assert.Contains("t2", joinState.ArrivedTransitionIds);
    }

    /// <summary>
    /// 终态实例往返后仍被判定为终态
    /// </summary>
    [Fact]
    public void JsonRoundTrip_FinalStatus_StaysFinal()
    {
        var instance = new WorkflowInstance
        {
            Status = WorkflowInstanceStatus.Terminated,
            EndTime = new DateTime(2024, 5, 6, 0, 0, 0, DateTimeKind.Utc),
            CancellationReason = "管理员强制终止"
        };

        var restored = JsonSerializer.Deserialize<WorkflowInstance>(JsonSerializer.Serialize(instance));

        Assert.NotNull(restored);
        Assert.True(restored.IsFinalStatus());
        Assert.Equal("管理员强制终止", restored.CancellationReason);
        Assert.Equal(instance.EndTime, restored.EndTime);
    }
}
