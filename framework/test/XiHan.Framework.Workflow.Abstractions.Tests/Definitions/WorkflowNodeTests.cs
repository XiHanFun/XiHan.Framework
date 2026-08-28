// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Workflow.Abstractions.Definitions;

namespace XiHan.Framework.Workflow.Abstractions.Tests.Definitions;

/// <summary>
/// 流程节点模型测试
/// </summary>
/// <remarks>
/// 三个"为空即关闭"的语义必须锁死：RetryPolicy 为空表示不重试、TimeoutSeconds 为空表示不超时、
/// ContinueOnError 默认 false 表示节点失败即故障实例。默认值一旦反转会静默改变线上流程行为。
/// </remarks>
public class WorkflowNodeTests
{
    /// <summary>
    /// 新建节点的默认值语义
    /// </summary>
    [Fact]
    public void Defaults_OnNewInstance_DisableRetryTimeoutAndContinueOnError()
    {
        var node = new WorkflowNode();

        Assert.Equal(string.Empty, node.Id);
        Assert.Equal(string.Empty, node.Name);
        Assert.Equal(string.Empty, node.ActivityType);
        Assert.Empty(node.Properties);
        Assert.Null(node.RetryPolicy);
        Assert.Null(node.TimeoutSeconds);
        Assert.False(node.ContinueOnError);
    }

    /// <summary>
    /// 不同节点实例的属性字典互相独立
    /// </summary>
    [Fact]
    public void Properties_OnDistinctInstances_AreNotShared()
    {
        var first = new WorkflowNode();
        var second = new WorkflowNode();

        first.Properties["assignee"] = "u-1";

        Assert.Empty(second.Properties);
        Assert.NotSame(first.Properties, second.Properties);
    }

    /// <summary>
    /// 属性字典允许显式写入 null 值并与"键不存在"区分
    /// </summary>
    /// <remarks>
    /// 活动读属性时依赖这个区分：键不存在走默认值，键存在但为 null 表示设计器显式清空。
    /// </remarks>
    [Fact]
    public void Properties_WithExplicitNullValue_IsDistinctFromMissingKey()
    {
        var node = new WorkflowNode();

        node.Properties["assignee"] = null;

        Assert.True(node.Properties.ContainsKey("assignee"));
        Assert.Null(node.Properties["assignee"]);
        Assert.False(node.Properties.ContainsKey("category"));
    }

    /// <summary>
    /// 节点 JSON 往返保留标量字段与重试策略
    /// </summary>
    [Fact]
    public void JsonRoundTrip_WithRetryPolicy_PreservesScalarFields()
    {
        var node = new WorkflowNode
        {
            Id = "http",
            Name = "调用外部接口",
            ActivityType = WorkflowActivityTypes.Http,
            TimeoutSeconds = 30,
            ContinueOnError = true,
            RetryPolicy = new WorkflowRetryPolicy { MaxAttempts = 5, FirstDelaySeconds = 3, BackoffFactor = 2.5 }
        };

        var restored = JsonSerializer.Deserialize<WorkflowNode>(JsonSerializer.Serialize(node));

        Assert.NotNull(restored);
        Assert.Equal("http", restored.Id);
        Assert.Equal("调用外部接口", restored.Name);
        Assert.Equal(WorkflowActivityTypes.Http, restored.ActivityType);
        Assert.Equal(30, restored.TimeoutSeconds);
        Assert.True(restored.ContinueOnError);
        Assert.NotNull(restored.RetryPolicy);
        Assert.Equal(5, restored.RetryPolicy.MaxAttempts);
        Assert.Equal(3, restored.RetryPolicy.FirstDelaySeconds);
        Assert.Equal(2.5, restored.RetryPolicy.BackoffFactor);
    }

    /// <summary>
    /// 未配置重试与超时的节点往返后仍为空
    /// </summary>
    [Fact]
    public void JsonRoundTrip_WithoutRetryPolicy_KeepsNulls()
    {
        var node = new WorkflowNode { Id = "start", ActivityType = WorkflowActivityTypes.Start };

        var restored = JsonSerializer.Deserialize<WorkflowNode>(JsonSerializer.Serialize(node));

        Assert.NotNull(restored);
        Assert.Null(restored.RetryPolicy);
        Assert.Null(restored.TimeoutSeconds);
        Assert.False(restored.ContinueOnError);
        Assert.Empty(restored.Properties);
    }
}
