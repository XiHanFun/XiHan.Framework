#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SequentialAndDecisionFlowTests
// Guid:c2d84f60-95ab-4e13-b7d0-38f61c25e94a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 12:03:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Builders;

namespace XiHan.Framework.Workflow.Tests;

/// <summary>
/// 顺序流与条件分支测试
/// </summary>
public class SequentialAndDecisionFlowTests : IDisposable
{
    private readonly WorkflowTestHost _host = new();

    /// <summary>
    /// 直线流程一次跑完
    /// </summary>
    [Fact]
    public async Task 直线流程同步完成()
    {
        var definition = WorkflowDefinitionBuilder.Create("linear", "直线流程")
            .AddStart()
            .AddNode("set", WorkflowActivityTypes.SetVariable, "赋值", node => node
                .WithProperty("Expressions", new Dictionary<string, string> { ["total"] = "price * count" }))
            .AddEnd()
            .AddTransition("start", "set")
            .AddTransition("set", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest
        {
            DefinitionCode = "linear",
            Variables = new() { ["price"] = 25, ["count"] = 4 }
        });

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Equal(100m, new WorkflowVariables(instance.Variables).Get<decimal>("total"));
        Assert.NotNull(instance.EndTime);

        var nodeInstances = await _host.InstanceStore.GetNodeInstancesAsync(instance.Id);
        Assert.Equal(3, nodeInstances.Count);
        Assert.All(nodeInstances, item => Assert.Equal(WorkflowNodeInstanceStatus.Completed, item.Status));
    }

    /// <summary>
    /// 独占网关按条件与优先级选路
    /// </summary>
    [Theory]
    [InlineData(20000, "big")]
    [InlineData(500, "small")]
    public async Task 独占网关按条件选路(int amount, string expectedPath)
    {
        var definition = WorkflowDefinitionBuilder.Create("decision", "分支流程")
            .AddStart()
            .AddDecision("gateway")
            .AddNode("big", WorkflowActivityTypes.SetVariable, "大额", node => node
                .WithProperty("Values", new Dictionary<string, object?> { ["path"] = "big" }))
            .AddNode("small", WorkflowActivityTypes.SetVariable, "小额", node => node
                .WithProperty("Values", new Dictionary<string, object?> { ["path"] = "small" }))
            .AddEnd()
            .AddTransition("start", "gateway")
            .AddTransition("gateway", "big", "amount > 10000")
            .AddDefaultTransition("gateway", "small")
            .AddTransition("big", "end")
            .AddTransition("small", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest
        {
            DefinitionCode = "decision",
            Variables = new() { ["amount"] = amount }
        });

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Equal(expectedPath, new WorkflowVariables(instance.Variables).Get<string>("path"));
    }

    /// <summary>
    /// 独占网关无匹配且无默认分支时按失败关闭语义故障
    /// </summary>
    [Fact]
    public async Task 独占网关无匹配分支时实例故障()
    {
        var definition = WorkflowDefinitionBuilder.Create("decision-fail", "无默认分支")
            .AddStart()
            .AddDecision("gateway")
            .AddEnd()
            .AddTransition("start", "gateway")
            .AddTransition("gateway", "end", "amount > 10000")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest
        {
            DefinitionCode = "decision-fail",
            Variables = new() { ["amount"] = 1 }
        });

        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        Assert.Contains("无匹配分支", instance.FaultMessage);
    }

    /// <summary>
    /// 必填启动变量缺失拒绝启动
    /// </summary>
    [Fact]
    public async Task 缺失必填变量拒绝启动()
    {
        var definition = WorkflowDefinitionBuilder.Create("required-var", "必填变量")
            .AddVariable("amount", required: true)
            .AddStart()
            .AddEnd()
            .AddTransition("start", "end")
            .Build();
        await _host.PublishAsync(definition);

        await Assert.ThrowsAsync<WorkflowException>(() =>
            _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "required-var" }));
    }

    /// <summary>
    /// 未发布定义不可启动
    /// </summary>
    [Fact]
    public async Task 草稿定义不可启动()
    {
        var definition = WorkflowDefinitionBuilder.Create("draft-only", "草稿")
            .AddStart().AddEnd().AddTransition("start", "end").Build();
        var created = await _host.DefinitionManager.CreateAsync(definition);

        await Assert.ThrowsAsync<WorkflowException>(() =>
            _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionId = created.Id }));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _host.Dispose();
    }
}
