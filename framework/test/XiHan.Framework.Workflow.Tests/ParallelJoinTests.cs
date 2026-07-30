// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Builders;

namespace XiHan.Framework.Workflow.Tests;

/// <summary>
/// 并行网关与汇聚测试
/// </summary>
public class ParallelJoinTests : IDisposable
{
    private readonly WorkflowTestHost _host = new();

    /// <summary>
    /// 并行扇出后 WaitAll 汇聚，汇聚后节点只执行一次
    /// </summary>
    [Fact]
    public async Task 并行分支等待全部到达后汇聚()
    {
        var definition = WorkflowDefinitionBuilder.Create("fork-join", "并行汇聚")
            .AddStart()
            .AddParallel("fork")
            .AddNode("a", WorkflowActivityTypes.SetVariable, "分支A", node => node
                .WithProperty("Values", new Dictionary<string, object?> { ["a"] = 1 }))
            .AddNode("b", WorkflowActivityTypes.SetVariable, "分支B", node => node
                .WithProperty("Values", new Dictionary<string, object?> { ["b"] = 2 }))
            .AddJoin("join")
            .AddNode("after", WorkflowActivityTypes.SetVariable, "汇聚后", node => node
                .WithProperty("Expressions", new Dictionary<string, string> { ["sum"] = "a + b" }))
            .AddEnd()
            .AddTransition("start", "fork")
            .AddTransition("fork", "a")
            .AddTransition("fork", "b")
            .AddTransition("a", "join")
            .AddTransition("b", "join")
            .AddTransition("join", "after")
            .AddTransition("after", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "fork-join" });

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Equal(3m, new WorkflowVariables(instance.Variables).Get<decimal>("sum"));

        var nodeInstances = await _host.InstanceStore.GetNodeInstancesAsync(instance.Id);
        Assert.Equal(1, nodeInstances.Count(item => item.NodeId == "join"));
        Assert.Equal(1, nodeInstances.Count(item => item.NodeId == "after"));
    }

    /// <summary>
    /// WaitAny 汇聚在首个分支到达时触发且只触发一次
    /// </summary>
    [Fact]
    public async Task 任一到达模式首个分支触发汇聚()
    {
        var definition = WorkflowDefinitionBuilder.Create("fork-any", "任一汇聚")
            .AddStart()
            .AddParallel("fork")
            .AddNode("a", WorkflowActivityTypes.SetVariable, "分支A", node => node
                .WithProperty("Values", new Dictionary<string, object?> { ["a"] = 1 }))
            .AddNode("b", WorkflowActivityTypes.SetVariable, "分支B", node => node
                .WithProperty("Values", new Dictionary<string, object?> { ["b"] = 2 }))
            .AddJoin("join", waitAny: true)
            .AddEnd()
            .AddTransition("start", "fork")
            .AddTransition("fork", "a")
            .AddTransition("fork", "b")
            .AddTransition("a", "join")
            .AddTransition("b", "join")
            .AddTransition("join", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "fork-any" });

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        var nodeInstances = await _host.InstanceStore.GetNodeInstancesAsync(instance.Id);
        Assert.Equal(1, nodeInstances.Count(item => item.NodeId == "join"));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _host.Dispose();
    }
}
