// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.SqlSugar.Entities;
using XiHan.Framework.Workflow.SqlSugar.Mapping;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 流程定义映射测试
/// </summary>
public class WorkflowDefinitionMapperTests
{
    /// <summary>
    /// 往返后标量字段一致
    /// </summary>
    [Fact]
    public void 往返后标量字段一致()
    {
        var definition = CreateDefinition();

        var entity = WorkflowDefinitionMapper.ToEntity(definition);
        var restored = WorkflowDefinitionMapper.ToDefinition(entity);

        Assert.Equal("1001", entity.BasicId);
        Assert.Equal(7, entity.OwnerTenantId);
        Assert.Equal((int)WorkflowDefinitionStatus.Published, entity.Status);
        Assert.Equal(definition.Id, restored.Id);
        Assert.Equal(definition.Code, restored.Code);
        Assert.Equal(definition.Name, restored.Name);
        Assert.Equal(definition.Version, restored.Version);
        Assert.Equal(definition.Description, restored.Description);
        Assert.Equal(definition.Category, restored.Category);
        Assert.Equal(definition.Status, restored.Status);
        Assert.Equal(definition.EnableCompensation, restored.EnableCompensation);
        Assert.Equal(definition.TenantId, restored.TenantId);
        Assert.Equal(definition.CreationTime, restored.CreationTime);
        Assert.Equal(definition.PublishTime, restored.PublishTime);
        Assert.Equal("{\"zoom\":1}", restored.ExtraProperties["layout"]);
    }

    /// <summary>
    /// 往返后图结构一致
    /// </summary>
    [Fact]
    public void 往返后图结构一致()
    {
        var restored = WorkflowDefinitionMapper.ToDefinition(WorkflowDefinitionMapper.ToEntity(CreateDefinition()));

        var node = Assert.Single(restored.Nodes);
        Assert.Equal("wait", node.Id);
        Assert.Equal(WorkflowActivityTypes.Delay, node.ActivityType);
        Assert.Equal(300, WorkflowValueConverter.ConvertTo<int>(node.Properties["Duration"]));
        Assert.Equal(60, node.TimeoutSeconds);
        Assert.True(node.ContinueOnError);
        Assert.NotNull(node.RetryPolicy);
        Assert.Equal(3, node.RetryPolicy.MaxAttempts);

        var transition = Assert.Single(restored.Transitions);
        Assert.Equal("start", transition.SourceNodeId);
        Assert.Equal("wait", transition.TargetNodeId);
        Assert.Equal("days > 1", transition.Condition);
        Assert.Equal(2, transition.Priority);
        Assert.True(transition.IsDefault);

        var variable = Assert.Single(restored.Variables);
        Assert.Equal("days", variable.Name);
        Assert.True(variable.Required);
        Assert.Equal(1, WorkflowValueConverter.ConvertTo<int>(variable.DefaultValue));
    }

    /// <summary>
    /// 空白 JSON 列读回为空集合
    /// </summary>
    [Fact]
    public void 空白JSON列读回为空集合()
    {
        var entity = new SysWorkflowDefinition("1002")
        {
            Code = "empty",
            Name = "空",
            NodesJson = string.Empty,
            TransitionsJson = " ",
            VariablesJson = string.Empty,
            ExtraProperties = string.Empty
        };

        var restored = WorkflowDefinitionMapper.ToDefinition(entity);

        Assert.Empty(restored.Nodes);
        Assert.Empty(restored.Transitions);
        Assert.Empty(restored.Variables);
        Assert.Empty(restored.ExtraProperties);
    }

    /// <summary>
    /// 变量名的大小写保持不变
    /// </summary>
    [Fact]
    public void 字典键大小写保持不变()
    {
        var definition = CreateDefinition();
        definition.ExtraProperties["CanvasLayout"] = "x";

        var restored = WorkflowDefinitionMapper.ToDefinition(WorkflowDefinitionMapper.ToEntity(definition));

        Assert.True(restored.ExtraProperties.ContainsKey("CanvasLayout"));
        Assert.Equal("Duration", Assert.Single(restored.Nodes).Properties.Keys.Single());
    }

    private static WorkflowDefinition CreateDefinition()
    {
        return new WorkflowDefinition
        {
            Id = "1001",
            Code = "leave",
            Name = "请假",
            Version = 3,
            Description = "请假审批",
            Category = "hr",
            Status = WorkflowDefinitionStatus.Published,
            EnableCompensation = true,
            Nodes =
            [
                new WorkflowNode
                {
                    Id = "wait",
                    Name = "等待",
                    ActivityType = WorkflowActivityTypes.Delay,
                    Properties = new Dictionary<string, object?> { ["Duration"] = 300 },
                    RetryPolicy = new WorkflowRetryPolicy { MaxAttempts = 3 },
                    TimeoutSeconds = 60,
                    ContinueOnError = true
                }
            ],
            Transitions =
            [
                new WorkflowTransition
                {
                    Id = "t1",
                    SourceNodeId = "start",
                    TargetNodeId = "wait",
                    Condition = "days > 1",
                    Priority = 2,
                    IsDefault = true
                }
            ],
            Variables =
            [
                new WorkflowVariableDefinition { Name = "days", Required = true, DefaultValue = 1 }
            ],
            TenantId = 7,
            CreationTime = new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc),
            PublishTime = new DateTime(2026, 9, 28, 9, 0, 0, DateTimeKind.Utc),
            ExtraProperties = new Dictionary<string, string> { ["layout"] = "{\"zoom\":1}" }
        };
    }
}
