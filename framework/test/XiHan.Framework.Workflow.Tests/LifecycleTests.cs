#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LifecycleTests
// Guid:d68a41f0-2c97-4e53-b0a8-96f31d75e2c4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/16 12:08:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Builders;
using XiHan.Framework.Workflow.Extensions.DependencyInjection;

namespace XiHan.Framework.Workflow.Tests;

/// <summary>
/// 实例生命周期测试（重试/取消补偿/终止/挂起）
/// </summary>
public class LifecycleTests : IDisposable
{
    private readonly WorkflowTestHost _host;
    private readonly CompensationRecorder _recorder = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    public LifecycleTests()
    {
        _host = new WorkflowTestHost(services =>
        {
            services.AddSingleton(_recorder);
            services.AddXiHanWorkflowActivity<FlakyActivity>();
            services.AddXiHanWorkflowActivity<RecordingCompensableActivity>();
        });
    }

    /// <summary>
    /// 节点重试策略：失败后排期重试书签，重试成功流程完成
    /// </summary>
    [Fact]
    public async Task 节点失败按策略重试成功()
    {
        var definition = WorkflowDefinitionBuilder.Create("retry-policy", "重试流程")
            .AddStart()
            .AddNode("flaky", "TestFlaky", "不稳定节点", node => node
                .WithProperty("SucceedOnAttempt", 2)
                .WithRetry(maxAttempts: 3, firstDelaySeconds: 5))
            .AddEnd()
            .AddTransition("start", "flaky")
            .AddTransition("flaky", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "retry-policy" });
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);

        var retryBookmark = Assert.Single(await _host.BookmarkStore.GetByInstanceAsync(instance.Id));
        Assert.Equal(WorkflowBookmarkKinds.Retry, retryBookmark.Kind);
        Assert.Equal(_host.Clock.Now.AddSeconds(5), retryBookmark.DueTime);

        var resumed = await _host.Engine.ResumeBookmarkAsync(retryBookmark.Id);
        Assert.Equal(WorkflowInstanceStatus.Completed, resumed.Status);

        var nodeInstance = (await _host.InstanceStore.GetNodeInstancesAsync(instance.Id))
            .Single(item => item.NodeId == "flaky");
        Assert.Equal(2, nodeInstance.TryCount);
    }

    /// <summary>
    /// 重试耗尽实例故障，人工重试后恢复
    /// </summary>
    [Fact]
    public async Task 重试耗尽后人工重试恢复()
    {
        var definition = WorkflowDefinitionBuilder.Create("manual-retry", "人工重试")
            .AddStart()
            .AddNode("flaky", "TestFlaky", "不稳定节点", node => node
                .WithProperty("SucceedOnAttempt", 2))
            .AddEnd()
            .AddTransition("start", "flaky")
            .AddTransition("flaky", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "manual-retry" });
        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        Assert.Equal("flaky", instance.FaultNodeId);

        var retried = await _host.Engine.RetryAsync(instance.Id);
        Assert.Equal(WorkflowInstanceStatus.Completed, retried.Status);
        Assert.Null(retried.FaultMessage);
    }

    /// <summary>
    /// 取消实例触发补偿，按执行逆序执行
    /// </summary>
    [Fact]
    public async Task 取消实例按完成逆序补偿()
    {
        var definition = WorkflowDefinitionBuilder.Create("compensate", "补偿流程")
            .WithCompensation()
            .AddStart()
            .AddNode("step1", "TestCompensable", "第一步")
            .AddNode("step2", "TestCompensable", "第二步")
            .AddUserTask("approve", "审批", node => node
                .WithProperty("Assignees", new List<string> { "u1" }))
            .AddEnd()
            .AddTransition("start", "step1")
            .AddTransition("step1", "step2")
            .AddTransition("step2", "approve")
            .AddTransition("approve", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "compensate" });
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);

        var canceled = await _host.Engine.CancelAsync(instance.Id, "用户撤回");

        Assert.Equal(WorkflowInstanceStatus.Canceled, canceled.Status);
        Assert.Equal("用户撤回", canceled.CancellationReason);
        Assert.Equal(["step2", "step1"], _recorder.CompensatedNodeIds);
        Assert.Empty(await _host.BookmarkStore.GetByInstanceAsync(instance.Id));

        var nodeInstances = await _host.InstanceStore.GetNodeInstancesAsync(instance.Id);
        Assert.Equal(2, nodeInstances.Count(item => item.Status == WorkflowNodeInstanceStatus.Compensated));
        Assert.Equal(1, nodeInstances.Count(item => item.Status == WorkflowNodeInstanceStatus.Canceled));

        // 终态幂等
        var again = await _host.Engine.CancelAsync(instance.Id);
        Assert.Equal(WorkflowInstanceStatus.Canceled, again.Status);
    }

    /// <summary>
    /// 终止活动强制结束整个实例（并行分支同时终止）
    /// </summary>
    [Fact]
    public async Task 终止活动结束全部分支()
    {
        var definition = WorkflowDefinitionBuilder.Create("terminate", "终止流程")
            .AddStart()
            .AddParallel("fork")
            .AddUserTask("approve", "永远等待", node => node
                .WithProperty("Assignees", new List<string> { "u1" }))
            .AddNode("kill", WorkflowActivityTypes.Terminate, "终止", node => node
                .WithProperty("Reason", "业务终止"))
            .AddTransition("start", "fork")
            .AddTransition("fork", "approve")
            .AddTransition("fork", "kill")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "terminate" });

        Assert.Equal(WorkflowInstanceStatus.Terminated, instance.Status);
        Assert.Equal("业务终止", instance.CancellationReason);
        Assert.Empty(await _host.BookmarkStore.GetByInstanceAsync(instance.Id));
    }

    /// <summary>
    /// 挂起实例拒绝办理，恢复后可办理
    /// </summary>
    [Fact]
    public async Task 挂起实例拒绝恢复书签()
    {
        var definition = WorkflowDefinitionBuilder.Create("suspend", "挂起流程")
            .AddStart()
            .AddUserTask("approve", "审批", node => node
                .WithProperty("Assignees", new List<string> { "u1" }))
            .AddEnd()
            .AddTransition("start", "approve")
            .AddTransition("approve", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "suspend" });
        var task = (await _host.UserTaskService.GetPendingAsync("u1")).Single();

        await _host.Engine.SuspendAsync(instance.Id, "例行冻结");
        await Assert.ThrowsAsync<WorkflowException>(() =>
            _host.UserTaskService.CompleteAsync(task.TaskId, "u1", WorkflowUserTaskOutcomes.Approved));

        await _host.Engine.ResumeAsync(instance.Id);
        var result = await _host.UserTaskService.CompleteAsync(task.TaskId, "u1", WorkflowUserTaskOutcomes.Approved);
        Assert.Equal(WorkflowInstanceStatus.Completed, result.Status);
    }

    /// <summary>
    /// 失败续行：节点失败写入错误变量并以 error 结果继续
    /// </summary>
    [Fact]
    public async Task 失败续行写入错误变量继续流转()
    {
        var definition = WorkflowDefinitionBuilder.Create("continue-on-error", "失败续行")
            .AddStart()
            .AddNode("flaky", "TestFlaky", "不稳定节点", node => node
                .WithProperty("SucceedOnAttempt", 99)
                .WithContinueOnError())
            .AddNode("errorPath", WorkflowActivityTypes.SetVariable, "错误处理", node => node
                .WithProperty("Values", new Dictionary<string, object?> { ["handled"] = true }))
            .AddEnd()
            .AddTransition("start", "flaky")
            .AddTransition("flaky", "errorPath", "outcome == 'error'")
            .AddTransition("errorPath", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "continue-on-error" });

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        var variables = new WorkflowVariables(instance.Variables);
        Assert.True(variables.Get<bool>("handled"));
        Assert.Contains("注定失败", variables.Get<string>(WorkflowConsts.LastErrorVariableName));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _host.Dispose();
    }
}
