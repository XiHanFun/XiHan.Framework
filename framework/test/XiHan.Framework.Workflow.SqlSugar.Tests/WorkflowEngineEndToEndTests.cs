// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Builders;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 以 SqlSugar 存储运行真实引擎的端到端测试
/// </summary>
public class WorkflowEngineEndToEndTests : IDisposable
{
    private readonly WorkflowEngineTestHost _host = new();

    /// <summary>
    /// 延时书签到期后恢复并完成
    /// </summary>
    [Fact]
    public async Task 延时书签到期后恢复并完成()
    {
        await _host.PublishAsync(BuildDelayDefinition());

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "delay" });

        var timer = Assert.Single(await _host.BookmarkStore.GetByInstanceAsync(instance.Id));
        Assert.Equal(WorkflowBookmarkKinds.Timer, timer.Kind);
        Assert.Equal(_host.Clock.Now.AddSeconds(300), timer.DueTime);
        Assert.Empty(await _host.BookmarkStore.GetDueAsync(_host.Clock.Now, 10));

        _host.Clock.Advance(TimeSpan.FromSeconds(301));
        Assert.Single(await _host.BookmarkStore.GetDueAsync(_host.Clock.Now, 10));

        await _host.Engine.ResumeBookmarkAsync(timer.Id);

        var reloaded = await _host.ReloadAsync(instance.Id);
        Assert.Equal(WorkflowInstanceStatus.Completed, reloaded.Status);
        Assert.Empty(await _host.BookmarkStore.GetByInstanceAsync(instance.Id));

        var history = await _host.InstanceStore.GetNodeInstancesAsync(instance.Id);
        Assert.Equal(["start", "wait", "end"], history.Select(item => item.NodeId));
    }

    /// <summary>
    /// 信号按相关性定向恢复后再广播
    /// </summary>
    [Fact]
    public async Task 信号按相关性定向恢复后再广播()
    {
        var definition = WorkflowDefinitionBuilder.Create("signal", "信号流程")
            .AddStart()
            .AddNode("wait", WorkflowActivityTypes.WaitSignal, "等待支付", node => node
                .WithProperty("SignalName", "order-paid"))
            .AddEnd()
            .AddTransition("start", "wait")
            .AddTransition("wait", "end")
            .Build();
        await _host.PublishAsync(definition);

        var first = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "signal", CorrelationId = "ORDER-1" });
        var second = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "signal", CorrelationId = "ORDER-2" });

        var targeted = await _host.Engine.PublishSignalAsync(
            "order-paid", new Dictionary<string, object?> { ["paidAmount"] = 88 }, "ORDER-1");

        Assert.Equal(1, targeted);
        var firstReloaded = await _host.ReloadAsync(first.Id);
        Assert.Equal(WorkflowInstanceStatus.Completed, firstReloaded.Status);
        Assert.Equal(88m, new WorkflowVariables(firstReloaded.Variables).Get<decimal>("paidAmount"));
        Assert.Equal(WorkflowInstanceStatus.Running, (await _host.ReloadAsync(second.Id)).Status);

        Assert.Equal(1, await _host.Engine.PublishSignalAsync("order-paid"));
        Assert.Equal(WorkflowInstanceStatus.Completed, (await _host.ReloadAsync(second.Id)).Status);
    }

    /// <summary>
    /// 会签节点状态跨两次办理保持
    /// </summary>
    [Fact]
    public async Task 会签节点状态跨两次办理保持()
    {
        var definition = WorkflowDefinitionBuilder.Create("all-approve", "会签流程")
            .AddStart()
            .AddUserTask("approve", "审批", node => node
                .WithProperty("Assignees", new List<string> { "u1", "u2" })
                .WithProperty("CompletionPolicy", "All")
                .WithProperty("Title", "单据 {{billNo}} 审批"))
            .AddNode("accepted", WorkflowActivityTypes.SetVariable, "通过", node => node
                .WithProperty("Values", new Dictionary<string, object?> { ["result"] = "accepted" }))
            .AddNode("denied", WorkflowActivityTypes.SetVariable, "拒绝", node => node
                .WithProperty("Values", new Dictionary<string, object?> { ["result"] = "denied" }))
            .AddEnd()
            .AddTransition("start", "approve")
            .AddTransition("approve", "accepted", "outcome == 'approved'")
            .AddTransition("approve", "denied", "outcome == 'rejected'")
            .AddTransition("accepted", "end")
            .AddTransition("denied", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest
        {
            DefinitionCode = "all-approve",
            Variables = new Dictionary<string, object?> { ["billNo"] = "B001" }
        });

        var firstTask = Assert.Single(await _host.UserTaskService.GetPendingAsync("u1"));
        Assert.Equal("单据 B001 审批", firstTask.Title);
        var afterFirst = await _host.UserTaskService.CompleteAsync(firstTask.TaskId, "u1", WorkflowUserTaskOutcomes.Approved);
        Assert.Equal(WorkflowInstanceStatus.Running, afterFirst.Status);

        var secondTask = Assert.Single(await _host.UserTaskService.GetPendingAsync("u2"));
        var afterSecond = await _host.UserTaskService.CompleteAsync(secondTask.TaskId, "u2", WorkflowUserTaskOutcomes.Approved);
        Assert.Equal(WorkflowInstanceStatus.Completed, afterSecond.Status);

        var reloaded = await _host.ReloadAsync(instance.Id);
        Assert.Equal("accepted", new WorkflowVariables(reloaded.Variables).Get<string>("result"));
    }

    /// <summary>
    /// 并行分支一支挂起后仍能汇合
    /// </summary>
    [Fact]
    public async Task 并行分支一支挂起后仍能汇合()
    {
        var definition = WorkflowDefinitionBuilder.Create("fork-wait", "并行等待")
            .AddStart()
            .AddParallel("fork")
            .AddNode("a", WorkflowActivityTypes.SetVariable, "分支A", node => node
                .WithProperty("Values", new Dictionary<string, object?> { ["a"] = 1 }))
            .AddNode("wait", WorkflowActivityTypes.WaitSignal, "等待", node => node
                .WithProperty("SignalName", "go"))
            .AddJoin("join")
            .AddEnd()
            .AddTransition("start", "fork")
            .AddTransition("fork", "a")
            .AddTransition("fork", "wait")
            .AddTransition("a", "join")
            .AddTransition("wait", "join")
            .AddTransition("join", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "fork-wait", CorrelationId = "FORK-1" });

        var suspended = await _host.ReloadAsync(instance.Id);
        Assert.Equal(WorkflowInstanceStatus.Running, suspended.Status);
        Assert.Single(suspended.JoinStates["join"].ArrivedTransitionIds);

        Assert.Equal(1, await _host.Engine.PublishSignalAsync("go"));

        var completed = await _host.ReloadAsync(instance.Id);
        Assert.Equal(WorkflowInstanceStatus.Completed, completed.Status);
        Assert.Empty(completed.JoinStates);
        Assert.Equal(1m, new WorkflowVariables(completed.Variables).Get<decimal>("a"));
    }

    /// <summary>
    /// 取消实例清空书签并取消挂起节点
    /// </summary>
    [Fact]
    public async Task 取消实例清空书签并取消挂起节点()
    {
        await _host.PublishAsync(BuildDelayDefinition());
        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "delay" });

        await _host.Engine.CancelAsync(instance.Id, "不需要了");

        var reloaded = await _host.ReloadAsync(instance.Id);
        Assert.Equal(WorkflowInstanceStatus.Canceled, reloaded.Status);
        Assert.Equal("不需要了", reloaded.CancellationReason);
        Assert.Empty(await _host.BookmarkStore.GetByInstanceAsync(instance.Id));

        var history = await _host.InstanceStore.GetNodeInstancesAsync(instance.Id);
        Assert.Equal(WorkflowNodeInstanceStatus.Canceled, Assert.Single(history, item => item.NodeId == "wait").Status);
    }

    /// <summary>
    /// 挂起实例的到期书签回退到期时间
    /// </summary>
    [Fact]
    public async Task 挂起实例的到期书签回退到期时间()
    {
        await _host.PublishAsync(BuildDelayDefinition());
        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "delay" });
        var timer = Assert.Single(await _host.BookmarkStore.GetByInstanceAsync(instance.Id));

        await _host.Engine.SuspendAsync(instance.Id);
        _host.Clock.Advance(TimeSpan.FromSeconds(301));
        await _host.Engine.ResumeBookmarkAsync(timer.Id, inputs: null, throwIfNotResumable: false);

        var kept = await _host.BookmarkStore.FindAsync(timer.Id);
        Assert.NotNull(kept);
        Assert.Equal(_host.Clock.Now.AddSeconds(300), kept.DueTime);
        Assert.Equal(WorkflowInstanceStatus.Suspended, (await _host.ReloadAsync(instance.Id)).Status);
    }

    /// <summary>
    /// 释放测试主机
    /// </summary>
    public void Dispose()
    {
        _host.Dispose();
    }

    private static WorkflowDefinition BuildDelayDefinition()
    {
        return WorkflowDefinitionBuilder.Create("delay", "延时流程")
            .AddStart()
            .AddDelay("wait", 300)
            .AddEnd()
            .AddTransition("start", "wait")
            .AddTransition("wait", "end")
            .Build();
    }
}
