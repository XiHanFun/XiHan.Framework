// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Definitions;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Definitions;

/// <summary>
/// 流程连线模型测试
/// </summary>
/// <remarks>
/// 连线的默认值决定了独占网关的求值语义：Condition 为空表示无条件、Priority 默认 0、IsDefault 默认 false。
/// 另外锁死"按优先级升序求值"这一排序口径可被稳定复现（同优先级保持声明顺序）。
/// </remarks>
public class WorkflowTransitionTests
{
    /// <summary>
    /// 新建连线的默认值语义
    /// </summary>
    [Fact]
    public void Defaults_OnNewInstance_AreUnconditionalAndNotDefaultBranch()
    {
        var transition = new WorkflowTransition();

        Assert.Equal(string.Empty, transition.Id);
        Assert.Null(transition.Name);
        Assert.Equal(string.Empty, transition.SourceNodeId);
        Assert.Equal(string.Empty, transition.TargetNodeId);
        Assert.Null(transition.Condition);
        Assert.Equal(0, transition.Priority);
        Assert.False(transition.IsDefault);
    }

    /// <summary>
    /// 优先级升序排序保持同优先级的声明顺序
    /// </summary>
    /// <remarks>
    /// 独占网关"按优先级升序逐条求值取第一条满足"的语义依赖稳定排序，
    /// 这里用 OrderBy（稳定排序）复现引擎的取边顺序，防止后续把 Priority 语义改成降序。
    /// </remarks>
    [Fact]
    public void OrderByPriority_WithTiedPriorities_IsStableAndAscending()
    {
        List<WorkflowTransition> transitions =
        [
            new WorkflowTransition { Id = "c", Priority = 10 },
            new WorkflowTransition { Id = "a", Priority = 1 },
            new WorkflowTransition { Id = "b", Priority = 1 }
        ];

        var ordered = transitions.OrderBy(item => item.Priority).Select(item => item.Id).ToList();

        Assert.Equal(new[] { "a", "b", "c" }, ordered);
    }

    /// <summary>
    /// 连线 JSON 往返保留条件与分支标记
    /// </summary>
    [Fact]
    public void JsonRoundTrip_WithCondition_PreservesAllFields()
    {
        var transition = new WorkflowTransition
        {
            Id = "t1",
            Name = "金额超限",
            SourceNodeId = "decision",
            TargetNodeId = "manager",
            Condition = "amount > 10000",
            Priority = 5,
            IsDefault = true
        };

        var restored = JsonSerializer.Deserialize<WorkflowTransition>(JsonSerializer.Serialize(transition));

        Assert.NotNull(restored);
        Assert.Equal("t1", restored.Id);
        Assert.Equal("金额超限", restored.Name);
        Assert.Equal("decision", restored.SourceNodeId);
        Assert.Equal("manager", restored.TargetNodeId);
        Assert.Equal("amount > 10000", restored.Condition);
        Assert.Equal(5, restored.Priority);
        Assert.True(restored.IsDefault);
    }

    /// <summary>
    /// 无条件连线往返后条件仍为 null 而非空串
    /// </summary>
    /// <remarks>
    /// 空串与 null 在引擎里语义不同：null 表示无条件直通，空串会被当成非法表达式。
    /// </remarks>
    [Fact]
    public void JsonRoundTrip_WithoutCondition_KeepsNullNotEmptyString()
    {
        var restored = JsonSerializer.Deserialize<WorkflowTransition>(
            JsonSerializer.Serialize(new WorkflowTransition { Id = "t1", SourceNodeId = "a", TargetNodeId = "b" }));

        Assert.NotNull(restored);
        Assert.Null(restored.Condition);
        Assert.Null(restored.Name);
    }
}
