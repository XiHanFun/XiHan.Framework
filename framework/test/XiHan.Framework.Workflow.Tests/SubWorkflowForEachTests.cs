// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Builders;

namespace XiHan.Framework.Workflow.Tests;

/// <summary>
/// 子流程与遍历测试
/// </summary>
public class SubWorkflowForEachTests : IDisposable
{
    private readonly WorkflowTestHost _host = new();

    private async Task PublishChildAsync()
    {
        var child = WorkflowDefinitionBuilder.Create("child", "子流程")
            .AddStart()
            .AddNode("double", WorkflowActivityTypes.SetVariable, "翻倍", node => node
                .WithProperty("Expressions", new Dictionary<string, string> { ["doubled"] = "item * 2" }))
            .AddEnd()
            .AddTransition("start", "double")
            .AddTransition("double", "end")
            .Build();
        await _host.PublishAsync(child);
    }

    /// <summary>
    /// 子流程完成后回调父实例并回传变量
    /// </summary>
    [Fact]
    public async Task 子流程完成后父实例继续()
    {
        await PublishChildAsync();

        var parent = WorkflowDefinitionBuilder.Create("parent", "父流程")
            .AddStart()
            .AddNode("sub", WorkflowActivityTypes.SubWorkflow, "调用子流程", node => node
                .WithProperty("DefinitionCode", "child")
                .WithProperty("VariableExpressions", new Dictionary<string, string> { ["item"] = "seed" })
                .WithProperty("ResultVariable", "childResult"))
            .AddEnd()
            .AddTransition("start", "sub")
            .AddTransition("sub", "end")
            .Build();
        await _host.PublishAsync(parent);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest
        {
            DefinitionCode = "parent",
            Variables = new() { ["seed"] = 21 }
        });

        var reloaded = await _host.ReloadAsync(instance.Id);
        Assert.Equal(WorkflowInstanceStatus.Completed, reloaded.Status);

        var childResult = new WorkflowVariables(reloaded.Variables).Get<Dictionary<string, object?>>("childResult");
        Assert.NotNull(childResult);
        Assert.Equal(42m, WorkflowValueConverter.ConvertTo<decimal>(childResult["doubled"]));

        // 子实例带父链接且已完成
        var children = await _host.InstanceStore.GetListAsync(definitionCode: "child");
        var child = Assert.Single(children);
        Assert.Equal(instance.Id, child.ParentInstanceId);
        Assert.Equal(WorkflowInstanceStatus.Completed, child.Status);
    }

    /// <summary>
    /// 顺序遍历逐项执行子流程并聚合结果
    /// </summary>
    [Fact]
    public async Task 顺序遍历聚合子流程结果()
    {
        await PublishChildAsync();
        await PublishForEachParentAsync(parallel: false);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest
        {
            DefinitionCode = "foreach-parent",
            Variables = new() { ["numbers"] = new List<object?> { 1, 2, 3 } }
        });

        await AssertForEachCompletedAsync(instance.Id);
    }

    /// <summary>
    /// 并行遍历全部子流程完成后聚合
    /// </summary>
    [Fact]
    public async Task 并行遍历聚合子流程结果()
    {
        await PublishChildAsync();
        await PublishForEachParentAsync(parallel: true);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest
        {
            DefinitionCode = "foreach-parent",
            Variables = new() { ["numbers"] = new List<object?> { 1, 2, 3 } }
        });

        await AssertForEachCompletedAsync(instance.Id);
    }

    private async Task PublishForEachParentAsync(bool parallel)
    {
        var parent = WorkflowDefinitionBuilder.Create("foreach-parent", "遍历父流程")
            .AddStart()
            .AddNode("each", WorkflowActivityTypes.ForEach, "遍历", node => node
                .WithProperty("ItemsExpression", "numbers")
                .WithProperty("DefinitionCode", "child")
                .WithProperty("Parallel", parallel)
                .WithProperty("ResultVariable", "results"))
            .AddEnd()
            .AddTransition("start", "each")
            .AddTransition("each", "end")
            .Build();
        await _host.PublishAsync(parent);
    }

    private async Task AssertForEachCompletedAsync(string instanceId)
    {
        var reloaded = await _host.ReloadAsync(instanceId);
        Assert.Equal(WorkflowInstanceStatus.Completed, reloaded.Status);

        var results = new WorkflowVariables(reloaded.Variables).Get<List<Dictionary<string, object?>>>("results");
        Assert.NotNull(results);
        Assert.Equal(3, results.Count);

        var doubled = results
            .Select(item => WorkflowValueConverter.ConvertTo<decimal>(item["doubled"]))
            .OrderBy(value => value)
            .ToList();
        Assert.Equal([2m, 4m, 6m], doubled);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _host.Dispose();
    }
}
