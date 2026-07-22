// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Activities.BuiltIn;
using XiHan.Framework.Workflow.Builders;
using XiHan.Framework.Workflow.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Options;

namespace XiHan.Framework.Workflow.Tests;

/// <summary>
/// 评审加固回归测试（失败关闭语义、事件契约、边界防护）
/// </summary>
public class ReviewHardeningTests : IDisposable
{
    private readonly WorkflowTestHost _host;

    /// <summary>
    /// 构造函数
    /// </summary>
    public ReviewHardeningTests()
    {
        _host = new WorkflowTestHost(services =>
        {
            services.AddXiHanWorkflowActivity<CancellationThrowingActivity>();
            services.AddHttpClient(HttpRequestActivity.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler());
        });
    }

    /// <summary>
    /// 生命周期事件按序发布且携带正确数据
    /// </summary>
    [Fact]
    public async Task 生命周期事件按契约发布()
    {
        var definition = WorkflowDefinitionBuilder.Create("events", "事件流程")
            .AddStart()
            .AddUserTask("approve", "审批", node => node
                .WithProperty("Assignees", new List<string> { "u1" })
                .WithProperty("CcUserIds", new List<string> { "cc1", "cc2" }))
            .AddEnd()
            .AddTransition("start", "approve")
            .AddTransition("approve", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "events" });

        Assert.Single(_host.Events.OfType<WorkflowInstanceStartedEventData>());
        var created = Assert.Single(_host.Events.OfType<WorkflowUserTaskCreatedEventData>());
        Assert.Equal("u1", created.Task.AssigneeId);
        Assert.Equal(["cc1", "cc2"], created.CcUserIds);

        var task = (await _host.UserTaskService.GetPendingAsync("u1")).Single();
        await _host.UserTaskService.CompleteAsync(task.TaskId, "u1", WorkflowUserTaskOutcomes.Approved, "同意");

        var completedTask = Assert.Single(_host.Events.OfType<WorkflowUserTaskCompletedEventData>());
        Assert.Equal("u1", completedTask.ActorId);
        Assert.Equal(WorkflowUserTaskOutcomes.Approved, completedTask.Outcome);

        var completedInstance = Assert.Single(_host.Events.OfType<WorkflowInstanceCompletedEventData>());
        Assert.Equal(instance.Id, completedInstance.Instance.Id);
    }

    /// <summary>
    /// 失控环路被批次步数上限拦截并故障实例
    /// </summary>
    [Fact]
    public async Task 失控环路触发步数上限故障()
    {
        using var host = new WorkflowTestHost(services =>
            services.Configure<XiHanWorkflowOptions>(options => options.MaxNodeExecutionsPerBurst = 20));

        var definition = WorkflowDefinitionBuilder.Create("loop", "环路流程")
            .AddStart()
            .AddNode("a", WorkflowActivityTypes.SetVariable, "甲")
            .AddNode("b", WorkflowActivityTypes.SetVariable, "乙")
            .AddTransition("start", "a")
            .AddTransition("a", "b")
            .AddTransition("b", "a")
            .Build();
        await host.PublishAsync(definition);

        var instance = await host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "loop" });

        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        Assert.Contains("失控环路", instance.FaultMessage);
    }

    /// <summary>
    /// 实例锁被占用时操作以锁超时异常失败
    /// </summary>
    [Fact]
    public async Task 锁竞争抛出锁超时异常()
    {
        using var host = new WorkflowTestHost(services =>
            services.Configure<XiHanWorkflowOptions>(options => options.InstanceLockAcquireTimeoutSeconds = 0));

        var definition = WorkflowDefinitionBuilder.Create("locked", "锁竞争流程")
            .AddStart()
            .AddUserTask("approve", "审批", node => node.WithProperty("Assignees", new List<string> { "u1" }))
            .AddEnd()
            .AddTransition("start", "approve")
            .AddTransition("approve", "end")
            .Build();
        await host.PublishAsync(definition);

        var instance = await host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "locked" });

        await using var handle = await host.Lock.TryAcquireAsync(
            WorkflowConsts.InstanceLockKeyPrefix + instance.Id, TimeSpan.FromMinutes(1));
        Assert.NotNull(handle);

        await Assert.ThrowsAsync<WorkflowLockTimeoutException>(() => host.Engine.SuspendAsync(instance.Id));
    }

    /// <summary>
    /// 新版本发布后在途实例仍沿原版本继续
    /// </summary>
    [Fact]
    public async Task 在途实例锁定原定义版本()
    {
        var v1 = WorkflowDefinitionBuilder.Create("versioned", "版本流程")
            .AddStart()
            .AddUserTask("approve", "审批", node => node.WithProperty("Assignees", new List<string> { "u1" }))
            .AddNode("mark", WorkflowActivityTypes.SetVariable, "标记", node => node
                .WithProperty("Values", new Dictionary<string, object?> { ["path"] = "v1" }))
            .AddEnd()
            .AddTransition("start", "approve")
            .AddTransition("approve", "mark")
            .AddTransition("mark", "end")
            .Build();
        await _host.PublishAsync(v1);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "versioned" });
        Assert.Equal(1, instance.DefinitionVersion);

        // 发布结构不同的 v2
        var v2 = await _host.DefinitionManager.CreateNewVersionAsync("versioned");
        v2.Nodes.Single(node => node.Id == "mark").Properties["Values"] =
            new Dictionary<string, object?> { ["path"] = "v2" };
        await _host.DefinitionManager.UpdateDraftAsync(v2);
        await _host.DefinitionManager.PublishAsync(v2.Id);

        var task = (await _host.UserTaskService.GetPendingAsync("u1")).Single();
        var result = await _host.UserTaskService.CompleteAsync(task.TaskId, "u1", WorkflowUserTaskOutcomes.Approved);

        Assert.Equal(WorkflowInstanceStatus.Completed, result.Status);
        Assert.Equal(1, result.DefinitionVersion);
        Assert.Equal("v1", new WorkflowVariables(result.Variables).Get<string>("path"));

        // 新实例走 v2
        var fresh = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "versioned" });
        Assert.Equal(2, fresh.DefinitionVersion);
    }

    /// <summary>
    /// 未知完成策略与未知汇聚模式在发布时被校验拦截
    /// </summary>
    [Fact]
    public async Task 未知策略与模式发布被拦截()
    {
        var badPolicy = WorkflowDefinitionBuilder.Create("bad-policy", "非法策略")
            .AddStart()
            .AddUserTask("approve", "审批", node => node
                .WithProperty("Assignees", new List<string> { "u1" })
                .WithProperty("CompletionPolicy", "Countersign"))
            .AddEnd()
            .AddTransition("start", "approve")
            .AddTransition("approve", "end")
            .Build();
        var createdPolicy = await _host.DefinitionManager.CreateAsync(badPolicy);
        var policyErrors = await Assert.ThrowsAsync<WorkflowDefinitionValidationException>(() =>
            _host.DefinitionManager.PublishAsync(createdPolicy.Id));
        Assert.Contains(policyErrors.Errors, error => error.Contains("未知完成策略"));

        var badMode = WorkflowDefinitionBuilder.Create("bad-mode", "非法模式")
            .AddStart()
            .AddParallel("fork")
            .AddNode("join", WorkflowActivityTypes.Join, "汇聚", node => node.WithProperty("Mode", "WaitFirst"))
            .AddEnd()
            .AddTransition("start", "fork")
            .AddTransition("fork", "join")
            .AddTransition("join", "end")
            .Build();
        var createdMode = await _host.DefinitionManager.CreateAsync(badMode);
        var modeErrors = await Assert.ThrowsAsync<WorkflowDefinitionValidationException>(() =>
            _host.DefinitionManager.PublishAsync(createdMode.Id));
        Assert.Contains(modeErrors.Errors, error => error.Contains("未知模式"));
    }

    /// <summary>
    /// 裸书签恢复（无办理结果）不自动同意，待办被重建
    /// </summary>
    [Fact]
    public async Task 裸书签恢复不自动同意()
    {
        var definition = WorkflowDefinitionBuilder.Create("bare-resume", "裸恢复流程")
            .AddStart()
            .AddUserTask("approve", "审批", node => node.WithProperty("Assignees", new List<string> { "u1" }))
            .AddEnd()
            .AddTransition("start", "approve")
            .AddTransition("approve", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "bare-resume" });
        var bookmark = (await _host.BookmarkStore.GetByInstanceAsync(instance.Id)).Single();

        var resumed = await _host.Engine.ResumeBookmarkAsync(bookmark.Id);

        Assert.Equal(WorkflowInstanceStatus.Running, resumed.Status);
        var rebuilt = (await _host.UserTaskService.GetPendingAsync("u1")).Single();
        Assert.NotEqual(bookmark.Id, rebuilt.TaskId);
    }

    /// <summary>
    /// 等待信号但实例无相关性时按失败关闭语义故障
    /// </summary>
    [Fact]
    public async Task 无相关性等待信号失败关闭()
    {
        var definition = WorkflowDefinitionBuilder.Create("no-corr", "无相关性信号")
            .AddStart()
            .AddNode("wait", WorkflowActivityTypes.WaitSignal, "等待", node => node
                .WithProperty("SignalName", "ping"))
            .AddEnd()
            .AddTransition("start", "wait")
            .AddTransition("wait", "end")
            .Build();
        await _host.PublishAsync(definition);

        var faulted = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "no-corr" });
        Assert.Equal(WorkflowInstanceStatus.Faulted, faulted.Status);
        Assert.Contains("CorrelationId", faulted.FaultMessage);

        // AcceptAnyCorrelation=true 时允许无相关性等待，广播信号可命中
        var anyDefinition = WorkflowDefinitionBuilder.Create("any-corr", "任意相关性信号")
            .AddStart()
            .AddNode("wait", WorkflowActivityTypes.WaitSignal, "等待", node => node
                .WithProperty("SignalName", "ping")
                .WithProperty("AcceptAnyCorrelation", true))
            .AddEnd()
            .AddTransition("start", "wait")
            .AddTransition("wait", "end")
            .Build();
        await _host.PublishAsync(anyDefinition);

        var waiting = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "any-corr" });
        Assert.Equal(WorkflowInstanceStatus.Running, waiting.Status);
        Assert.Equal(1, await _host.Engine.PublishSignalAsync("ping"));
        Assert.Equal(WorkflowInstanceStatus.Completed, (await _host.ReloadAsync(waiting.Id)).Status);
    }

    /// <summary>
    /// 活动执行途中被取消按节点故障处理（实例可重试，不悬空）
    /// </summary>
    [Fact]
    public async Task 执行途中取消转化为可恢复故障()
    {
        var definition = WorkflowDefinitionBuilder.Create("cancel-mid", "途中取消")
            .AddStart()
            .AddNode("boom", "TestCancellation", "取消节点")
            .AddEnd()
            .AddTransition("start", "boom")
            .AddTransition("boom", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "cancel-mid" });

        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        Assert.Equal("boom", instance.FaultNodeId);
        Assert.NotNull(instance.FaultNodeInstanceId);
    }

    /// <summary>
    /// 子流程定义不存在时父实例按故障回调而非永久等待
    /// </summary>
    [Fact]
    public async Task 子流程启动失败回调父实例故障()
    {
        var definition = WorkflowDefinitionBuilder.Create("bad-child", "坏子流程")
            .AddStart()
            .AddNode("sub", WorkflowActivityTypes.SubWorkflow, "子流程", node => node
                .WithProperty("DefinitionCode", "definitely-not-exists"))
            .AddEnd()
            .AddTransition("start", "sub")
            .AddTransition("sub", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "bad-child" });

        var reloaded = await _host.ReloadAsync(instance.Id);
        Assert.Equal(WorkflowInstanceStatus.Faulted, reloaded.Status);
        Assert.Contains("definitely-not-exists", reloaded.FaultMessage);
    }

    /// <summary>
    /// 挂起实例的到期书签被回退而非每轮空转
    /// </summary>
    [Fact]
    public async Task 挂起实例到期书签自动回退()
    {
        var definition = WorkflowDefinitionBuilder.Create("backoff", "回退流程")
            .AddStart()
            .AddDelay("wait", 10)
            .AddEnd()
            .AddTransition("start", "wait")
            .AddTransition("wait", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "backoff" });
        await _host.Engine.SuspendAsync(instance.Id);

        _host.Clock.Advance(TimeSpan.FromSeconds(11));
        var bookmark = (await _host.BookmarkStore.GetDueAsync(_host.Clock.Now, 10)).Single();

        // 模拟定时器 Worker 的恢复尝试：实例挂起时书签保留且到期回退
        await _host.Engine.ResumeBookmarkAsync(bookmark.Id, inputs: null, throwIfNotResumable: false);

        var retained = (await _host.BookmarkStore.GetByInstanceAsync(instance.Id)).Single();
        Assert.True(retained.DueTime > _host.Clock.Now);
        Assert.Empty(await _host.BookmarkStore.GetDueAsync(_host.Clock.Now, 10));
    }

    /// <summary>
    /// 转办给链上既有受理人被拒绝
    /// </summary>
    [Fact]
    public async Task 转办给既有受理人被拒绝()
    {
        var definition = WorkflowDefinitionBuilder.Create("dup-transfer", "重复转办")
            .AddStart()
            .AddUserTask("approve", "审批", node => node
                .WithProperty("Assignees", new List<string> { "u1", "u2" })
                .WithProperty("CompletionPolicy", "Sequential"))
            .AddEnd()
            .AddTransition("start", "approve")
            .AddTransition("approve", "end")
            .Build();
        await _host.PublishAsync(definition);

        await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "dup-transfer" });
        var task = (await _host.UserTaskService.GetPendingAsync("u1")).Single();

        await Assert.ThrowsAsync<WorkflowException>(() =>
            _host.UserTaskService.TransferAsync(task.TaskId, "u1", "u2"));
    }

    /// <summary>
    /// HTTP 活动：成功响应解析、错误状态失败关闭、宽松模式输出状态码
    /// </summary>
    [Fact]
    public async Task HTTP活动响应解析与错误语义()
    {
        var definition = WorkflowDefinitionBuilder.Create("http-flow", "HTTP流程")
            .AddStart()
            .AddNode("call", WorkflowActivityTypes.Http, "调用", node => node
                .WithProperty("Url", "https://stub.local/orders/{{orderId}}")
                .WithProperty("ResultVariable", "response"))
            .AddEnd()
            .AddTransition("start", "call")
            .AddTransition("call", "end")
            .Build();
        await _host.PublishAsync(definition);

        var ok = await _host.Engine.StartAsync(new WorkflowStartRequest
        {
            DefinitionCode = "http-flow",
            Variables = new() { ["orderId"] = "ok" }
        });
        Assert.Equal(WorkflowInstanceStatus.Completed, ok.Status);
        var response = new WorkflowVariables(ok.Variables).Get<Dictionary<string, object?>>("response");
        Assert.NotNull(response);
        Assert.Equal(42m, WorkflowValueConverter.ConvertTo<decimal>(response["total"]));

        // 500 + 默认失败关闭 → 实例故障
        var failed = await _host.Engine.StartAsync(new WorkflowStartRequest
        {
            DefinitionCode = "http-flow",
            Variables = new() { ["orderId"] = "fail" }
        });
        Assert.Equal(WorkflowInstanceStatus.Faulted, failed.Status);
        Assert.Contains("500", failed.FaultMessage);

        // 宽松模式：完成并输出状态码
        var lenient = WorkflowDefinitionBuilder.Create("http-lenient", "HTTP宽松流程")
            .AddStart()
            .AddNode("call", WorkflowActivityTypes.Http, "调用", node => node
                .WithProperty("Url", "https://stub.local/orders/fail")
                .WithProperty("FailOnErrorStatus", false))
            .AddEnd()
            .AddTransition("start", "call")
            .AddTransition("call", "end")
            .Build();
        await _host.PublishAsync(lenient);

        var tolerant = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "http-lenient" });
        Assert.Equal(WorkflowInstanceStatus.Completed, tolerant.Status);
        Assert.Equal(500m, new WorkflowVariables(tolerant.Variables).Get<decimal>("httpStatusCode"));
    }

    /// <summary>
    /// 脚本活动：变量回写、返回值捕获、编译错误失败关闭
    /// </summary>
    [Fact]
    public async Task 脚本活动变量回写与编译错误()
    {
        var definition = WorkflowDefinitionBuilder.Create("script-flow", "脚本流程")
            .AddStart()
            .AddNode("script", WorkflowActivityTypes.Script, "脚本", node => node
                .WithProperty("Code", "variables[\"total\"] = System.Convert.ToDecimal(variables[\"price\"]) * 3; return \"done\";")
                .WithProperty("ResultVariable", "scriptResult"))
            .AddEnd()
            .AddTransition("start", "script")
            .AddTransition("script", "end")
            .Build();
        await _host.PublishAsync(definition);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest
        {
            DefinitionCode = "script-flow",
            Variables = new() { ["price"] = 7 }
        });

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        var variables = new WorkflowVariables(instance.Variables);
        Assert.Equal(21m, variables.Get<decimal>("total"));
        Assert.Equal("done", variables.Get<string>("scriptResult"));

        // 编译错误 → 节点故障
        var bad = WorkflowDefinitionBuilder.Create("script-bad", "坏脚本")
            .AddStart()
            .AddNode("script", WorkflowActivityTypes.Script, "脚本", node => node
                .WithProperty("Code", "this is not csharp;"))
            .AddEnd()
            .AddTransition("start", "script")
            .AddTransition("script", "end")
            .Build();
        await _host.PublishAsync(bad);

        var faulted = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "script-bad" });
        Assert.Equal(WorkflowInstanceStatus.Faulted, faulted.Status);
        Assert.Contains("执行失败", faulted.FaultMessage);
    }

    /// <summary>
    /// 取消父实例级联取消运行中的子实例
    /// </summary>
    [Fact]
    public async Task 取消父实例级联取消子实例()
    {
        var child = WorkflowDefinitionBuilder.Create("cascade-child", "级联子流程")
            .AddStart()
            .AddUserTask("approve", "子审批", node => node.WithProperty("Assignees", new List<string> { "u1" }))
            .AddEnd()
            .AddTransition("start", "approve")
            .AddTransition("approve", "end")
            .Build();
        await _host.PublishAsync(child);

        var parent = WorkflowDefinitionBuilder.Create("cascade-parent", "级联父流程")
            .AddStart()
            .AddNode("sub", WorkflowActivityTypes.SubWorkflow, "子流程", node => node
                .WithProperty("DefinitionCode", "cascade-child"))
            .AddEnd()
            .AddTransition("start", "sub")
            .AddTransition("sub", "end")
            .Build();
        await _host.PublishAsync(parent);

        var instance = await _host.Engine.StartAsync(new WorkflowStartRequest { DefinitionCode = "cascade-parent" });
        var children = await _host.InstanceStore.GetChildrenAsync(instance.Id);
        var childInstance = Assert.Single(children);
        Assert.Equal(WorkflowInstanceStatus.Running, childInstance.Status);

        await _host.Engine.CancelAsync(instance.Id, "整单撤销");

        Assert.Equal(WorkflowInstanceStatus.Canceled, (await _host.ReloadAsync(childInstance.Id)).Status);
        Assert.Empty(await _host.UserTaskService.GetPendingAsync("u1"));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _host.Dispose();
    }

    /// <summary>
    /// HTTP 桩处理器（/orders/ok 返回 JSON，/orders/fail 返回 500）
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            var response = path.EndsWith("/orders/ok", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"total":42,"status":"paid"}""", Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("boom", Encoding.UTF8, "text/plain")
                };

            return Task.FromResult(response);
        }
    }
}
