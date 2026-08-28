// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Runtime;

/// <summary>
/// 节点实例模型测试
/// </summary>
/// <remarks>
/// 节点实例是执行历史，同时承载三个语义不同的弱类型字典（输入/输出/活动私有状态），
/// 三者必须互相独立、往返可还原——活动私有状态跨挂起恢复保持是会签与遍历能续跑的前提。
/// </remarks>
public class WorkflowNodeInstanceTests
{
    /// <summary>
    /// 新建节点实例的默认值语义
    /// </summary>
    [Fact]
    public void Defaults_OnNewInstance_AreRunningAndEmpty()
    {
        var nodeInstance = new WorkflowNodeInstance();

        Assert.Equal(string.Empty, nodeInstance.Id);
        Assert.Equal(string.Empty, nodeInstance.InstanceId);
        Assert.Equal(string.Empty, nodeInstance.NodeId);
        Assert.Equal(string.Empty, nodeInstance.Name);
        Assert.Equal(string.Empty, nodeInstance.ActivityType);
        Assert.Equal(WorkflowNodeInstanceStatus.Running, nodeInstance.Status);
        Assert.Equal(0, nodeInstance.TryCount);
        Assert.Equal(default(DateTime), nodeInstance.StartTime);
        Assert.Null(nodeInstance.EndTime);
        Assert.Empty(nodeInstance.Inputs);
        Assert.Empty(nodeInstance.Outputs);
        Assert.Empty(nodeInstance.State);
        Assert.Null(nodeInstance.FaultMessage);
        Assert.Null(nodeInstance.CompensatedTime);
        Assert.Null(nodeInstance.TenantId);
    }

    /// <summary>
    /// 输入、输出与活动私有状态三个字典互不共享
    /// </summary>
    [Fact]
    public void Inputs_Outputs_State_AreIndependentDictionaries()
    {
        var nodeInstance = new WorkflowNodeInstance();

        nodeInstance.Inputs["a"] = 1;

        Assert.Empty(nodeInstance.Outputs);
        Assert.Empty(nodeInstance.State);
        Assert.NotSame(nodeInstance.Inputs, nodeInstance.Outputs);
        Assert.NotSame(nodeInstance.Inputs, nodeInstance.State);
        Assert.NotSame(nodeInstance.Outputs, nodeInstance.State);
    }

    /// <summary>
    /// 不同节点实例之间的字典互相独立
    /// </summary>
    [Fact]
    public void Collections_OnDistinctInstances_AreNotShared()
    {
        var first = new WorkflowNodeInstance();
        var second = new WorkflowNodeInstance();

        first.Outputs["result"] = "ok";
        first.State[WorkflowConsts.ChildInstanceIdsStateKey] = new List<string> { "ins-2" };

        Assert.Empty(second.Outputs);
        Assert.Empty(second.State);
    }

    /// <summary>
    /// 节点实例 JSON 往返保留状态、尝试次数与三个字典
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesStatusTryCountAndDictionaries()
    {
        var nodeInstance = new WorkflowNodeInstance
        {
            Id = "ni-1",
            InstanceId = "ins-1",
            NodeId = "approve",
            Name = "审批",
            ActivityType = WorkflowActivityTypes.UserTask,
            Status = WorkflowNodeInstanceStatus.Suspended,
            TryCount = 2,
            StartTime = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc),
            EndTime = new DateTime(2024, 5, 6, 7, 9, 9, DateTimeKind.Utc),
            CompensatedTime = new DateTime(2024, 5, 6, 8, 0, 0, DateTimeKind.Utc),
            FaultMessage = "受理人不存在",
            TenantId = 9L,
            Inputs = { ["comment"] = "同意" },
            Outputs = { ["outcome"] = "approved" },
            State = { ["cursor"] = 3 }
        };

        var restored = JsonSerializer.Deserialize<WorkflowNodeInstance>(JsonSerializer.Serialize(nodeInstance));

        Assert.NotNull(restored);
        Assert.Equal("ni-1", restored.Id);
        Assert.Equal("ins-1", restored.InstanceId);
        Assert.Equal("approve", restored.NodeId);
        Assert.Equal("审批", restored.Name);
        Assert.Equal(WorkflowActivityTypes.UserTask, restored.ActivityType);
        Assert.Equal(WorkflowNodeInstanceStatus.Suspended, restored.Status);
        Assert.Equal(2, restored.TryCount);
        Assert.Equal(nodeInstance.StartTime, restored.StartTime);
        Assert.Equal(nodeInstance.EndTime, restored.EndTime);
        Assert.Equal(nodeInstance.CompensatedTime, restored.CompensatedTime);
        Assert.Equal("受理人不存在", restored.FaultMessage);
        Assert.Equal(9L, restored.TenantId);
        Assert.Equal("同意", WorkflowValueConverter.Normalize(restored.Inputs["comment"]));
        Assert.Equal("approved", WorkflowValueConverter.Normalize(restored.Outputs["outcome"]));
        Assert.Equal(3m, WorkflowValueConverter.Normalize(restored.State["cursor"]));
    }

    /// <summary>
    /// 未补偿的节点实例往返后补偿时间仍为空
    /// </summary>
    [Fact]
    public void JsonRoundTrip_WithoutCompensation_KeepsCompensatedTimeNull()
    {
        var restored = JsonSerializer.Deserialize<WorkflowNodeInstance>(
            JsonSerializer.Serialize(new WorkflowNodeInstance { Id = "ni-1" }));

        Assert.NotNull(restored);
        Assert.Null(restored.CompensatedTime);
        Assert.Null(restored.EndTime);
        Assert.Null(restored.FaultMessage);
        Assert.Equal(WorkflowNodeInstanceStatus.Running, restored.Status);
    }
}
