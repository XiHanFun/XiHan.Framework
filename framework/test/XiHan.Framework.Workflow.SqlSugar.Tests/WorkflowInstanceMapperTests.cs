// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.SqlSugar.Mapping;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 流程实例与节点实例映射测试
/// </summary>
public class WorkflowInstanceMapperTests
{
    private static readonly DateTime BaseTime = new(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 实例标量字段往返一致
    /// </summary>
    [Fact]
    public void 实例标量字段往返一致()
    {
        var instance = CreateInstance();

        var entity = WorkflowInstanceMapper.ToEntity(instance);
        var restored = WorkflowInstanceMapper.ToInstance(entity);

        Assert.Equal("2001", entity.BasicId);
        Assert.Equal((int)WorkflowInstanceStatus.Faulted, entity.Status);
        Assert.Equal(9, entity.OwnerTenantId);
        Assert.Equal(instance.Id, restored.Id);
        Assert.Equal(instance.DefinitionId, restored.DefinitionId);
        Assert.Equal(instance.DefinitionCode, restored.DefinitionCode);
        Assert.Equal(instance.DefinitionVersion, restored.DefinitionVersion);
        Assert.Equal(instance.Name, restored.Name);
        Assert.Equal(instance.Status, restored.Status);
        Assert.Equal(instance.StarterId, restored.StarterId);
        Assert.Equal(instance.ParentInstanceId, restored.ParentInstanceId);
        Assert.Equal(instance.ParentNodeInstanceId, restored.ParentNodeInstanceId);
        Assert.Equal(instance.Depth, restored.Depth);
        Assert.Equal(instance.TenantId, restored.TenantId);
        Assert.Equal(instance.CreationTime, restored.CreationTime);
        Assert.Equal(instance.StartTime, restored.StartTime);
        Assert.Equal(instance.EndTime, restored.EndTime);
        Assert.Equal(instance.FaultMessage, restored.FaultMessage);
        Assert.Equal(instance.FaultNodeId, restored.FaultNodeId);
        Assert.Equal(instance.FaultNodeInstanceId, restored.FaultNodeInstanceId);
        Assert.Equal(instance.CancellationReason, restored.CancellationReason);
    }

    /// <summary>
    /// 相关性标识的空串与空值各自保留
    /// </summary>
    [Fact]
    public void 相关性标识的空串与空值各自保留()
    {
        var emptyInstance = CreateInstance();
        emptyInstance.CorrelationId = string.Empty;
        var nullInstance = CreateInstance();
        nullInstance.CorrelationId = null;

        Assert.Equal(string.Empty, WorkflowInstanceMapper.ToInstance(WorkflowInstanceMapper.ToEntity(emptyInstance)).CorrelationId);
        Assert.Null(WorkflowInstanceMapper.ToInstance(WorkflowInstanceMapper.ToEntity(nullInstance)).CorrelationId);
    }

    /// <summary>
    /// 变量往返后可按原类型取值
    /// </summary>
    [Fact]
    public void 变量往返后可按原类型取值()
    {
        var instance = CreateInstance();
        instance.Variables = new Dictionary<string, object?>
        {
            ["text"] = "a",
            ["count"] = 3,
            ["amount"] = 12.5m,
            ["flag"] = true,
            ["when"] = BaseTime,
            ["nested"] = new Dictionary<string, object?> { ["k"] = "v" },
            ["list"] = new List<string> { "x", "y" },
            ["none"] = null
        };

        var restored = WorkflowInstanceMapper.ToInstance(WorkflowInstanceMapper.ToEntity(instance));
        var variables = new WorkflowVariables(restored.Variables);

        Assert.Equal("a", variables.Get<string>("text"));
        Assert.Equal(3, variables.Get<int>("count"));
        Assert.Equal(12.5m, variables.Get<decimal>("amount"));
        Assert.True(variables.Get<bool>("flag"));
        Assert.Equal(BaseTime, variables.Get<DateTime>("when"));
        Assert.Equal("v", variables.Get<Dictionary<string, string>>("nested")?["k"]);
        Assert.Equal(["x", "y"], variables.Get<List<string>>("list"));
        Assert.True(restored.Variables.ContainsKey("none"));
        Assert.Null(variables.Get("none"));
    }

    /// <summary>
    /// 汇聚波次状态往返一致
    /// </summary>
    [Fact]
    public void 汇聚波次状态往返一致()
    {
        var instance = CreateInstance();
        instance.JoinStates = new Dictionary<string, WorkflowJoinState>
        {
            ["join"] = new WorkflowJoinState { ArrivedTransitionIds = ["t1", "t2"], Fired = true }
        };

        var restored = WorkflowInstanceMapper.ToInstance(WorkflowInstanceMapper.ToEntity(instance));

        var state = Assert.Single(restored.JoinStates);
        Assert.Equal("join", state.Key);
        Assert.True(state.Value.Fired);
        Assert.True(state.Value.ArrivedTransitionIds.SetEquals(["t1", "t2"]));
    }

    /// <summary>
    /// 节点实例往返一致
    /// </summary>
    [Fact]
    public void 节点实例往返一致()
    {
        var nodeInstance = new WorkflowNodeInstance
        {
            Id = "3001",
            InstanceId = "2001",
            NodeId = "approve",
            Name = "审批",
            ActivityType = "UserTask",
            Status = WorkflowNodeInstanceStatus.Suspended,
            TryCount = 2,
            StartTime = BaseTime,
            EndTime = BaseTime.AddSeconds(5),
            Inputs = new Dictionary<string, object?> { ["actorId"] = "u1" },
            Outputs = new Dictionary<string, object?> { ["outcome"] = "approved" },
            State = new Dictionary<string, object?> { ["assignees"] = new List<string> { "u1", "u2" } },
            FaultMessage = "旧故障",
            CompensatedTime = BaseTime.AddSeconds(9),
            TenantId = 9
        };

        var entity = WorkflowNodeInstanceMapper.ToEntity(nodeInstance);
        var restored = WorkflowNodeInstanceMapper.ToNodeInstance(entity);

        Assert.Equal(3001, entity.Sequence);
        Assert.Equal((int)WorkflowNodeInstanceStatus.Suspended, entity.Status);
        Assert.Equal(nodeInstance.Id, restored.Id);
        Assert.Equal(nodeInstance.InstanceId, restored.InstanceId);
        Assert.Equal(nodeInstance.NodeId, restored.NodeId);
        Assert.Equal(nodeInstance.Name, restored.Name);
        Assert.Equal(nodeInstance.ActivityType, restored.ActivityType);
        Assert.Equal(nodeInstance.Status, restored.Status);
        Assert.Equal(nodeInstance.TryCount, restored.TryCount);
        Assert.Equal(nodeInstance.StartTime, restored.StartTime);
        Assert.Equal(nodeInstance.EndTime, restored.EndTime);
        Assert.Equal(nodeInstance.FaultMessage, restored.FaultMessage);
        Assert.Equal(nodeInstance.CompensatedTime, restored.CompensatedTime);
        Assert.Equal(nodeInstance.TenantId, restored.TenantId);
        Assert.Equal("u1", WorkflowValueConverter.ConvertTo<string>(restored.Inputs["actorId"]));
        Assert.Equal("approved", WorkflowValueConverter.ConvertTo<string>(restored.Outputs["outcome"]));
        Assert.Equal(["u1", "u2"], WorkflowValueConverter.ConvertTo<List<string>>(restored.State["assignees"]));
    }

    /// <summary>
    /// 创建顺序由标识解析
    /// </summary>
    [Fact]
    public void 创建顺序由标识解析()
    {
        Assert.Equal(123, WorkflowNodeInstanceMapper.ParseSequence("123"));
        Assert.Equal(0, WorkflowNodeInstanceMapper.ParseSequence("abc"));
        Assert.Equal(0, WorkflowNodeInstanceMapper.ParseSequence(string.Empty));
    }

    private static WorkflowInstance CreateInstance()
    {
        return new WorkflowInstance
        {
            Id = "2001",
            DefinitionId = "1001",
            DefinitionCode = "leave",
            DefinitionVersion = 3,
            Name = "请假",
            Status = WorkflowInstanceStatus.Faulted,
            CorrelationId = "ORDER-1",
            StarterId = "u0",
            ParentInstanceId = "2000",
            ParentNodeInstanceId = "3000",
            Depth = 1,
            TenantId = 9,
            CreationTime = BaseTime,
            StartTime = BaseTime,
            EndTime = BaseTime.AddMinutes(1),
            FaultMessage = "故障",
            FaultNodeId = "approve",
            FaultNodeInstanceId = "3001",
            CancellationReason = "原因"
        };
    }
}
