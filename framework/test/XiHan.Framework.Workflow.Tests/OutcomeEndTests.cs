// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Abstractions.Outcomes;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Builders;

namespace XiHan.Framework.Workflow.Tests;

/// <summary>
/// 结果结束节点测试（实例结果、结果处理器回调、并行分支收口、处理器失败重试）
/// </summary>
public class OutcomeEndTests : IDisposable
{
    private readonly WorkflowTestHost _host = new(services =>
    {
        services.AddSingleton<OutcomeRecorder>();
        services.AddTransient<IWorkflowOutcomeHandler, RecordingOutcomeHandler>();
    });

    private OutcomeRecorder Recorder => _host.Provider.GetRequiredService<OutcomeRecorder>();

    /// <summary>
    /// 单人审批：同意走 approved 结束、驳回走 rejected 结束
    /// </summary>
    private async Task PublishSingleApprovalAsync(string code)
    {
        var definition = WorkflowDefinitionBuilder.Create(code, "单人审批")
            .AddStart()
            .AddUserTask("approve", "审批", node => node.WithProperty("Assignees", new List<string> { "u1" }))
            .AddOutcomeEnd("end-approved", WorkflowUserTaskOutcomes.Approved, "通过")
            .AddOutcomeEnd("end-rejected", WorkflowUserTaskOutcomes.Rejected, "驳回")
            .AddTransition("start", "approve")
            .AddTransition("approve", "end-approved", "outcome == 'approved'")
            .AddTransition("approve", "end-rejected", "outcome == 'rejected'")
            .Build();
        await _host.PublishAsync(definition);
    }

    /// <summary>
    /// 结果结束节点写入实例结果，结果处理器在实例完成前被调用一次
    /// </summary>
    [Fact]
    public async Task 结果结束节点写入实例结果并回调处理器()
    {
        await PublishSingleApprovalAsync("outcome-approved");
        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "outcome-approved", CorrelationId = "BILL-1" });

        var task = (await _host.UserTaskService.GetPendingAsync("u1")).Single();
        var completed = await _host.UserTaskService.CompleteAsync(task.TaskId, "u1", WorkflowUserTaskOutcomes.Approved);

        Assert.Equal(WorkflowInstanceStatus.Completed, completed.Status);
        Assert.Equal(WorkflowUserTaskOutcomes.Approved, completed.Outcome);
        Assert.Equal(WorkflowUserTaskOutcomes.Approved, (await _host.ReloadAsync(instance.Id)).Outcome);

        var call = Assert.Single(Recorder.Calls);
        Assert.Equal(("BILL-1", WorkflowUserTaskOutcomes.Approved, WorkflowInstanceStatus.Running), call);

        var completedEvent = Assert.Single(_host.Events.OfType<WorkflowInstanceCompletedEventData>());
        Assert.Equal(WorkflowUserTaskOutcomes.Approved, completedEvent.Instance.Outcome);
    }

    /// <summary>
    /// 并行审批中任一驳回即整单驳回：其余分支的待办随实例完成清理
    /// </summary>
    [Fact]
    public async Task 并行分支驳回立即结束整个实例()
    {
        var definition = WorkflowDefinitionBuilder.Create("parallel-reject", "并行会审")
            .AddStart()
            .AddParallel("fork")
            .AddUserTask("finance", "财务", node => node.WithProperty("Assignees", new List<string> { "f1" }))
            .AddUserTask("legal", "法务", node => node.WithProperty("Assignees", new List<string> { "l1" }))
            .AddJoin("join")
            .AddOutcomeEnd("end-approved", WorkflowUserTaskOutcomes.Approved)
            .AddOutcomeEnd("end-rejected", WorkflowUserTaskOutcomes.Rejected)
            .AddTransition("start", "fork")
            .AddTransition("fork", "finance")
            .AddTransition("fork", "legal")
            .AddTransition("finance", "join", "outcome == 'approved'")
            .AddTransition("finance", "end-rejected", "outcome == 'rejected'")
            .AddTransition("legal", "join", "outcome == 'approved'")
            .AddTransition("legal", "end-rejected", "outcome == 'rejected'")
            .AddTransition("join", "end-approved")
            .Build();
        await _host.PublishAsync(definition);
        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "parallel-reject" });

        var financeTask = (await _host.UserTaskService.GetPendingAsync("f1")).Single();
        var result = await _host.UserTaskService.CompleteAsync(financeTask.TaskId, "f1", WorkflowUserTaskOutcomes.Rejected);

        Assert.Equal(WorkflowInstanceStatus.Completed, result.Status);
        Assert.Equal(WorkflowUserTaskOutcomes.Rejected, result.Outcome);
        Assert.Empty(await _host.UserTaskService.GetPendingAsync("l1"));
        Assert.Empty(await _host.BookmarkStore.GetByInstanceAsync(instance.Id));

        var legalNode = (await _host.InstanceStore.GetNodeInstancesAsync(instance.Id)).Single(item => item.NodeId == "legal");
        Assert.Equal(WorkflowNodeInstanceStatus.Canceled, legalNode.Status);
        Assert.Empty((await _host.ReloadAsync(instance.Id)).JoinStates);
    }

    /// <summary>
    /// 处理器失败：结束节点故障、实例停在故障态且不写结果；排除原因后重试即完成
    /// </summary>
    [Fact]
    public async Task 处理器失败实例故障且重试后完成()
    {
        await PublishSingleApprovalAsync("outcome-retry");
        Recorder.FailuresRemaining = 1;
        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "outcome-retry" });

        var task = (await _host.UserTaskService.GetPendingAsync("u1")).Single();
        var faulted = await _host.UserTaskService.CompleteAsync(task.TaskId, "u1", WorkflowUserTaskOutcomes.Approved);

        Assert.Equal(WorkflowInstanceStatus.Faulted, faulted.Status);
        Assert.Equal("end-approved", faulted.FaultNodeId);
        Assert.Contains("业务回写失败", faulted.FaultMessage);
        Assert.Null(faulted.Outcome);

        var retried = await _host.Engine.RetryAsync(instance.Id);

        Assert.Equal(WorkflowInstanceStatus.Completed, retried.Status);
        Assert.Equal(WorkflowUserTaskOutcomes.Approved, retried.Outcome);
        Assert.Equal(2, Recorder.Attempts);
    }

    /// <summary>
    /// 普通结束节点不回调处理器、不写结果
    /// </summary>
    [Fact]
    public async Task 普通结束节点不回调处理器()
    {
        var definition = WorkflowDefinitionBuilder.Create("plain-end", "普通结束")
            .AddStart()
            .AddEnd()
            .AddTransition("start", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "plain-end" });

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Null(instance.Outcome);
        Assert.Empty(Recorder.Calls);
    }

    /// <summary>
    /// 发布校验：结束节点 Outcome 为空字符串被拒
    /// </summary>
    [Fact]
    public async Task 发布拦截空结果()
    {
        var definition = WorkflowDefinitionBuilder.Create("blank-outcome", "空结果")
            .AddStart()
            .AddNode("end", WorkflowActivityTypes.End, "结束", node => node.WithProperty("Outcome", " "))
            .AddTransition("start", "end")
            .Build();
        var created = await _host.DefinitionManager.CreateAsync(definition);

        var exception = await Assert.ThrowsAsync<WorkflowDefinitionValidationException>(() =>
            _host.DefinitionManager.PublishAsync(created.Id));

        Assert.Contains(exception.Errors, error => error.Contains("Outcome"));
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _host.Dispose();
    }
}

/// <summary>
/// 结果回调记录器
/// </summary>
public sealed class OutcomeRecorder
{
    /// <summary>
    /// 成功回调（相关性标识、结果、回调时实例状态）
    /// </summary>
    public List<(string? CorrelationId, string Outcome, WorkflowInstanceStatus Status)> Calls { get; } = [];

    /// <summary>
    /// 剩余需要模拟失败的次数
    /// </summary>
    public int FailuresRemaining { get; set; }

    /// <summary>
    /// 总调用次数（含失败）
    /// </summary>
    public int Attempts { get; set; }
}

/// <summary>
/// 记录型结果处理器
/// </summary>
public sealed class RecordingOutcomeHandler : IWorkflowOutcomeHandler
{
    private readonly OutcomeRecorder _recorder;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="recorder">记录器</param>
    public RecordingOutcomeHandler(OutcomeRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <summary>
    /// 处理流程结果
    /// </summary>
    /// <param name="context">结果上下文</param>
    /// <returns>任务</returns>
    public Task HandleAsync(WorkflowOutcomeContext context)
    {
        _recorder.Attempts++;
        if (_recorder.FailuresRemaining > 0)
        {
            _recorder.FailuresRemaining--;
            throw new InvalidOperationException("业务回写失败");
        }

        _recorder.Calls.Add((context.Instance.CorrelationId, context.Outcome, context.Instance.Status));
        return Task.CompletedTask;
    }
}
