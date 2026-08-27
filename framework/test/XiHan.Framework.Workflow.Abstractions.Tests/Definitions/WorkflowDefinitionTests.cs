// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Runtime;

namespace XiHan.Framework.Workflow.Abstractions.Tests;

/// <summary>
/// 流程定义模型测试
/// </summary>
/// <remarks>
/// 定义是要持久化并跨版本读回的模板，因此重点是默认值语义（新建即草稿、版本从 1 起）
/// 与 JSON 往返后结构不丢失；节点属性字典是 object? 弱类型，往返后必然退化成 JsonElement，
/// 这正是 <see cref="WorkflowValueConverter"/> 存在的理由，一并锁死。
/// </remarks>
public class WorkflowDefinitionTests
{
    /// <summary>
    /// 新建定义的默认值语义
    /// </summary>
    [Fact]
    public void Defaults_OnNewInstance_AreDraftAndEmpty()
    {
        var definition = new WorkflowDefinition();

        Assert.Equal(string.Empty, definition.Id);
        Assert.Equal(string.Empty, definition.Code);
        Assert.Equal(string.Empty, definition.Name);
        Assert.Equal(1, definition.Version);
        Assert.Null(definition.Description);
        Assert.Null(definition.Category);
        Assert.Equal(WorkflowDefinitionStatus.Draft, definition.Status);
        Assert.False(definition.EnableCompensation);
        Assert.Empty(definition.Nodes);
        Assert.Empty(definition.Transitions);
        Assert.Empty(definition.Variables);
        Assert.Null(definition.TenantId);
        Assert.Equal(default(DateTime), definition.CreationTime);
        Assert.Null(definition.PublishTime);
        Assert.Empty(definition.ExtraProperties);
    }

    /// <summary>
    /// 不同定义实例之间的集合互相独立
    /// </summary>
    /// <remarks>
    /// 属性初始化器写在实例字段上而非静态字段，任何一处写成静态共享都会让所有定义串味，故显式验证。
    /// </remarks>
    [Fact]
    public void Collections_OnDistinctInstances_AreNotShared()
    {
        var first = new WorkflowDefinition();
        var second = new WorkflowDefinition();

        first.Nodes.Add(new WorkflowNode { Id = "n1" });
        first.Transitions.Add(new WorkflowTransition { Id = "t1" });
        first.Variables.Add(new WorkflowVariableDefinition { Name = "v1" });
        first.ExtraProperties["layout"] = "{}";

        Assert.Empty(second.Nodes);
        Assert.Empty(second.Transitions);
        Assert.Empty(second.Variables);
        Assert.Empty(second.ExtraProperties);
        Assert.NotSame(first.Nodes, second.Nodes);
    }

    /// <summary>
    /// 完整定义 JSON 往返后结构与标量字段不丢失
    /// </summary>
    [Fact]
    public void JsonRoundTrip_FullGraph_PreservesStructure()
    {
        var definition = new WorkflowDefinition
        {
            Id = "def-1",
            Code = "leave",
            Name = "请假流程",
            Version = 3,
            Description = "员工请假审批",
            Category = "人事",
            Status = WorkflowDefinitionStatus.Published,
            EnableCompensation = true,
            TenantId = 1024L,
            CreationTime = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            PublishTime = new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc),
            Nodes =
            [
                new WorkflowNode { Id = "start", Name = "开始", ActivityType = WorkflowActivityTypes.Start },
                new WorkflowNode
                {
                    Id = "approve",
                    Name = "审批",
                    ActivityType = WorkflowActivityTypes.UserTask,
                    ContinueOnError = true,
                    TimeoutSeconds = 3600,
                    RetryPolicy = new WorkflowRetryPolicy { MaxAttempts = 3, FirstDelaySeconds = 5, BackoffFactor = 1.5 }
                }
            ],
            Transitions =
            [
                new WorkflowTransition
                {
                    Id = "t1",
                    Name = "同意",
                    SourceNodeId = "start",
                    TargetNodeId = "approve",
                    Condition = "outcome == 'approved'",
                    Priority = 10,
                    IsDefault = true
                }
            ],
            Variables =
            [
                new WorkflowVariableDefinition { Name = "days", Type = "number", Required = true, Description = "请假天数" }
            ],
            ExtraProperties = { ["layout"] = "{\"x\":1}" }
        };

        var json = JsonSerializer.Serialize(definition);
        var restored = JsonSerializer.Deserialize<WorkflowDefinition>(json);

        Assert.NotNull(restored);
        Assert.Equal("def-1", restored.Id);
        Assert.Equal("leave", restored.Code);
        Assert.Equal("请假流程", restored.Name);
        Assert.Equal(3, restored.Version);
        Assert.Equal("员工请假审批", restored.Description);
        Assert.Equal("人事", restored.Category);
        Assert.Equal(WorkflowDefinitionStatus.Published, restored.Status);
        Assert.True(restored.EnableCompensation);
        Assert.Equal(1024L, restored.TenantId);
        Assert.Equal(definition.CreationTime, restored.CreationTime);
        Assert.Equal(definition.PublishTime, restored.PublishTime);

        Assert.Equal(2, restored.Nodes.Count);
        Assert.Equal("start", restored.Nodes[0].Id);
        Assert.Equal("approve", restored.Nodes[1].Id);
        Assert.True(restored.Nodes[1].ContinueOnError);
        Assert.Equal(3600, restored.Nodes[1].TimeoutSeconds);
        Assert.NotNull(restored.Nodes[1].RetryPolicy);
        Assert.Equal(3, restored.Nodes[1].RetryPolicy!.MaxAttempts);
        Assert.Equal(1.5, restored.Nodes[1].RetryPolicy!.BackoffFactor);
        Assert.Null(restored.Nodes[0].RetryPolicy);

        var transition = Assert.Single(restored.Transitions);
        Assert.Equal("t1", transition.Id);
        Assert.Equal("同意", transition.Name);
        Assert.Equal("outcome == 'approved'", transition.Condition);
        Assert.Equal(10, transition.Priority);
        Assert.True(transition.IsDefault);

        var variable = Assert.Single(restored.Variables);
        Assert.Equal("days", variable.Name);
        Assert.Equal("number", variable.Type);
        Assert.True(variable.Required);
        Assert.Equal("请假天数", variable.Description);

        Assert.Equal("{\"x\":1}", restored.ExtraProperties["layout"]);
    }

    /// <summary>
    /// 节点弱类型属性往返后退化为 JsonElement，需经值转换器归一化
    /// </summary>
    [Fact]
    public void JsonRoundTrip_NodeProperties_DegradeToJsonElementAndNormalizeBack()
    {
        var definition = new WorkflowDefinition
        {
            Code = "leave",
            Nodes =
            [
                new WorkflowNode
                {
                    Id = "approve",
                    ActivityType = WorkflowActivityTypes.UserTask,
                    Properties =
                    {
                        ["assignee"] = "u-1",
                        ["limit"] = 42,
                        ["enabled"] = true
                    }
                }
            ]
        };

        var restored = JsonSerializer.Deserialize<WorkflowDefinition>(JsonSerializer.Serialize(definition));

        Assert.NotNull(restored);
        var properties = Assert.Single(restored.Nodes).Properties;

        Assert.Equal("u-1", WorkflowValueConverter.Normalize(properties["assignee"]));
        Assert.Equal(42m, WorkflowValueConverter.Normalize(properties["limit"]));
        Assert.True(Assert.IsType<bool>(WorkflowValueConverter.Normalize(properties["enabled"])));
        Assert.Equal("u-1", WorkflowValueConverter.ConvertTo<string>(properties["assignee"]));
        Assert.Equal(42, WorkflowValueConverter.ConvertTo<int>(properties["limit"]));
    }

    /// <summary>
    /// 未赋值的可空字段往返后仍为 null
    /// </summary>
    [Fact]
    public void JsonRoundTrip_OptionalFields_StayNull()
    {
        var restored = JsonSerializer.Deserialize<WorkflowDefinition>(JsonSerializer.Serialize(new WorkflowDefinition()));

        Assert.NotNull(restored);
        Assert.Null(restored.Description);
        Assert.Null(restored.Category);
        Assert.Null(restored.TenantId);
        Assert.Null(restored.PublishTime);
        Assert.Equal(WorkflowDefinitionStatus.Draft, restored.Status);
        Assert.Equal(1, restored.Version);
    }
}
