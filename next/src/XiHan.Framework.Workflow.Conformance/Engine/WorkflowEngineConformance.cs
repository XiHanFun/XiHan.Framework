// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions.Outbox;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Assignees;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.ExternalTasks;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Tasks;
using Xunit;

namespace XiHan.Framework.Workflow.Conformance.Engine;

/// <summary>
/// 引擎的一致性套件（RFC-0021）。
/// </summary>
/// <remarks>
/// 顺序流、决策、并行与汇合、用户任务的三种完成策略、转办与加签、延时与超时、信号、子流程与 ForEach、重试与继续、取消 / 终止 / 挂起 / 恢复、定义生命周期、事件主题、事件网关、HTTP、补偿、输出映射、实例级超时、延迟启动与重用策略、信号与定时启动、改变量 / 跳转 / 迁移、清理与延时变体。Provider 继承并实现 <see cref="StartAsync"/>：装配被测特性，并把套件的记录器、活动与处理器登记进容器。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class WorkflowEngineConformance
{
    /// <summary>
    /// 启动装配了被测 Provider 的宿主。
    /// </summary>
    /// <remarks>
    /// <paramref name="configure"/> 登记套件的记录器与活动，必须在宿主构建前应用；<paramref name="configuration"/> 是额外配置。
    /// </remarks>
    protected abstract Task<WorkflowEngineHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null);

    /// <summary>
    /// 延时与超时用例等待的上限。
    /// </summary>
    protected virtual TimeSpan WaitTimeout => TimeSpan.FromSeconds(10);

    /// <summary>
    /// 被测 Provider 的写入是否随调用方提交或回滚。
    /// </summary>
    /// <remarks>
    /// 数据库实现是（加入 Transaction 阶段开启的工作单元）；内存实现没有事务、写入立即生效，覆盖为 <see langword="false"/>，事务相关的用例按它断言。
    /// </remarks>
    protected virtual bool JoinsCallerTransaction => true;

    /// <summary>
    /// 用例 1：顺序流：输出合并进变量、表达式求值、节点实例与事件按顺序记录、实例完成。
    /// </summary>
    [Fact]
    public async Task Linear_RunsToCompletion_MergesOutputs_AndPublishesEvents()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var definition = WorkflowDefinitionBuilder.Create("conf-linear")
            .AddVariable("x", required: true)
            .AddStart()
            .AddNode("a", RecordActivity.TypeName, configure: node => node.WithProperty(RecordActivity.OutputsProperty, new JsonObject { ["y"] = 2 }))
            .AddNode("b", ActivityTypes.SetVariable, configure: node => node.WithProperty(SetVariableActivity.ExpressionsProperty, new JsonObject { ["z"] = "x + y", ["greeting"] = "'hi ' + upper(name)", ["nothing"] = "null" }))
            .AddEnd()
            .AddTransition("start", "a").AddTransition("a", "b").AddTransition("b", "end")
            .Build();
        await host.PublishAsync(definition).ConfigureAwait(false);

        var missing = await host.StartAsync("conf-linear", new JsonObject { ["name"] = "bob" }).ConfigureAwait(false);
        Assert.Equal(WorkflowErrors.InputInvalid, missing.Error?.Code);

        var started = (await host.StartAsync("conf-linear", new JsonObject { ["x"] = 40, ["name"] = "bob" }, starterId: "alice", correlationId: "doc-1").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Completed, started.Status);
        var instance = await host.InstanceAsync(started.Id).ConfigureAwait(false);
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Equal(42, instance.Variables["z"]!.GetValue<decimal>());
        Assert.Equal("hi BOB", instance.Variables["greeting"]!.GetValue<string>());
        // 求出 null 的表达式写成值为 null 的变量，不让实例出错（ADR-0059）
        Assert.True(instance.Variables.TryGetPropertyValue("nothing", out var nothing) && nothing is null);
        Assert.NotNull(instance.EndedAt);
        Assert.Equal(("alice", "doc-1"), (instance.StarterId, instance.CorrelationId));

        var nodes = await host.NodeInstancesAsync(started.Id).ConfigureAwait(false);
        Assert.Equal(["start", "a", "b", "end"], nodes.Select(static node => node.NodeId));
        Assert.All(nodes, node => Assert.Equal(WorkflowNodeInstanceStatus.Completed, node.Status));
        Assert.Equal([(started.Id, "a", 1)], log.Executed);
        // 活动在引擎这次操作的调用里执行：调用方没有调用时引擎自己开的那一次，来源是工作流协议、目标是操作名
        Assert.Equal([new InvocationOrigin { Protocol = InvocationProtocols.Workflow, Target = "workflow.start" }], log.Origins);
        Assert.Empty(await host.BookmarksAsync(started.Id).ConfigureAwait(false));

        var topics = log.Events.Where(pending => pending.Data.InstanceId == started.Id).Select(static pending => pending.Topic).ToList();
        Assert.Equal(WorkflowTopics.InstanceStarted, topics[0]);
        Assert.Equal(WorkflowTopics.InstanceCompleted, topics[^1]);
        Assert.Equal(4, topics.Count(static topic => topic == WorkflowTopics.NodeExecuting));
        Assert.Equal(4, topics.Count(static topic => topic == WorkflowTopics.NodeExecuted));
    }

    /// <summary>
    /// 用例 2：决策按条件选一条出边，都不匹配走默认出边。
    /// </summary>
    /// <remarks>
    /// 条件里能用结果标签。
    /// </remarks>
    [Fact]
    public async Task Decision_PicksMatchingTransition_OrDefault()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var definition = WorkflowDefinitionBuilder.Create("conf-decision")
            .AddStart()
            .AddNode("label", RecordActivity.TypeName, configure: node => node.WithProperty(RecordActivity.LabelProperty, "big"))
            .AddDecision("check")
            .AddNode("high", RecordActivity.TypeName).AddNode("low", RecordActivity.TypeName).AddNode("other", RecordActivity.TypeName)
            .AddEnd()
            .AddTransition("start", "label").AddTransition("label", "check")
            .AddTransition("check", "high", "amount > 100 && outcome == 'big'").AddTransition("check", "low", "amount <= 10")
            .AddDefaultTransition("check", "other")
            .AddTransition("high", "end").AddTransition("low", "end").AddTransition("other", "end")
            .Build();
        await host.PublishAsync(definition).ConfigureAwait(false);

        var high = (await host.StartAsync("conf-decision", new JsonObject { ["amount"] = 500 }).ConfigureAwait(false)).ValueOrThrow();
        var low = (await host.StartAsync("conf-decision", new JsonObject { ["amount"] = 5 }).ConfigureAwait(false)).ValueOrThrow();
        var other = (await host.StartAsync("conf-decision", new JsonObject { ["amount"] = 50 }).ConfigureAwait(false)).ValueOrThrow();
        Assert.All(new[] { high, low, other }, instance => Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status));
        Assert.Equal(["label", "high"], log.Executed.Where(entry => entry.InstanceId == high.Id).Select(static entry => entry.NodeId));
        Assert.Equal(["label", "low"], log.Executed.Where(entry => entry.InstanceId == low.Id).Select(static entry => entry.NodeId));
        Assert.Equal(["label", "other"], log.Executed.Where(entry => entry.InstanceId == other.Id).Select(static entry => entry.NodeId));

        // 条件引用未知变量 → 实例故障（表达式错误），不是悄悄为假
        var broken = (await host.StartAsync("conf-decision", new JsonObject()).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Faulted, broken.Status);
        Assert.Equal(WorkflowErrors.ExpressionInvalid, broken.Fault?.Code);
    }

    /// <summary>
    /// 用例 3：并行网关让两个分支都执行。
    /// </summary>
    /// <remarks>
    /// 汇合等全部到达只执行一次；有分支死掉的汇合让实例故障。
    /// </remarks>
    [Fact]
    public async Task Parallel_RunsBothBranches_AndJoinWaitsForAll()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var definition = WorkflowDefinitionBuilder.Create("conf-parallel")
            .AddStart().AddParallel("fork")
            .AddNode("left", RecordActivity.TypeName).AddNode("right", RecordActivity.TypeName)
            .AddJoin("join").AddNode("after", RecordActivity.TypeName).AddEnd()
            .AddTransition("start", "fork").AddTransition("fork", "left").AddTransition("fork", "right")
            .AddTransition("left", "join").AddTransition("right", "join").AddTransition("join", "after").AddTransition("after", "end")
            .Build();
        await host.PublishAsync(definition).ConfigureAwait(false);

        var instance = (await host.StartAsync("conf-parallel").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        var executed = log.Executed.Where(entry => entry.InstanceId == instance.Id).Select(static entry => entry.NodeId).ToList();
        Assert.Equal(["left", "right", "after"], executed);
        Assert.Single((await host.NodeInstancesAsync(instance.Id).ConfigureAwait(false)).Where(static node => node.NodeId == "join"));
        Assert.Empty(instance.JoinStates);

        // 一个分支被决策导向没有出边的节点（不到汇合）：汇合的两条入边永远凑不齐 → 故障
        var dead = WorkflowDefinitionBuilder.Create("conf-dead-join")
            .AddStart().AddParallel("fork")
            .AddNode("left", RecordActivity.TypeName).AddNode("right", RecordActivity.TypeName).AddDecision("route").AddNode("sink", RecordActivity.TypeName)
            .AddJoin("join").AddEnd()
            .AddTransition("start", "fork").AddTransition("fork", "left").AddTransition("fork", "right")
            .AddTransition("left", "join").AddTransition("right", "route").AddTransition("route", "join", "false").AddDefaultTransition("route", "sink")
            .AddTransition("join", "end")
            .Build();
        await host.PublishAsync(dead).ConfigureAwait(false);
        var faulted = (await host.StartAsync("conf-dead-join").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Faulted, faulted.Status);
        Assert.Null(faulted.FaultNodeInstanceId);
    }

    /// <summary>
    /// 用例 4：用户任务（Any）。
    /// </summary>
    /// <remarks>
    /// 待办按指派人查到；非指派人完成被拒；结果不在允许范围被拒；通过后继续；事件记录任务创建与完成。
    /// </remarks>
    [Fact]
    public async Task UserTask_Any_PendingCompleteAndGuards()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await host.PublishAsync(Approval("conf-task-any", UserTaskCompletionPolicy.Any, "alice", "bob")).ConfigureAwait(false);

        var instance = (await host.StartAsync("conf-task-any", starterId: "starter").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        var alice = Assert.Single(await host.PendingAsync("alice").ConfigureAwait(false));
        var bob = Assert.Single(await host.PendingAsync("bob").ConfigureAwait(false));
        Assert.Equal(("approve", instance.Id, "alice", "Approve 7"), (alice.NodeId, alice.InstanceId, alice.AssigneeId, alice.Title));
        Assert.Empty(await host.PendingAsync("carol").ConfigureAwait(false));
        Assert.Equal(2, log.EventsOf(WorkflowTopics.TaskCreated).Count(data => data.InstanceId == instance.Id));

        Assert.Equal(WorkflowErrors.TaskDenied, (await host.CompleteAsync(alice.Id, "carol", UserTaskOutcomes.Approved).ConfigureAwait(false)).Error?.Code);
        Assert.Equal(WorkflowErrors.InputInvalid, (await host.CompleteAsync(alice.Id, "alice", "maybe").ConfigureAwait(false)).Error?.Code);
        Assert.Equal(WorkflowErrors.BookmarkNotFound, (await host.CompleteAsync("missing", "alice", UserTaskOutcomes.Approved).ConfigureAwait(false)).Error?.Code);

        var completed = (await host.CompleteAsync(alice.Id, "alice", UserTaskOutcomes.Approved, new JsonObject { ["note"] = "ok" }, "fine").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Completed, completed.Status);
        Assert.Equal("approved", completed.Outcome);
        Assert.Equal("ok", completed.Variables["note"]!.GetValue<string>());
        Assert.Empty(await host.PendingAsync("bob").ConfigureAwait(false));
        Assert.Empty(await host.BookmarksAsync(instance.Id).ConfigureAwait(false));
        Assert.Equal(["approved"], log.Outcomes);
        var completedEvent = Assert.Single(log.EventsOf(WorkflowTopics.TaskCompleted));
        Assert.Equal(("alice", "approved", "fine"), (completedEvent.ActorId, completedEvent.Outcome, completedEvent.Reason));

        // 完成后再完成同一任务：书签已消费
        Assert.Equal(WorkflowErrors.BookmarkNotFound, (await host.CompleteAsync(bob.Id, "bob", UserTaskOutcomes.Approved).ConfigureAwait(false)).Error?.Code);
    }

    /// <summary>
    /// 用例 5：会签（All）要全部通过，拒绝立即结束。
    /// </summary>
    /// <remarks>
    /// 顺序（Sequential）一次一个。
    /// </remarks>
    [Fact]
    public async Task UserTask_AllAndSequential_Policies()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await host.PublishAsync(Approval("conf-task-all", UserTaskCompletionPolicy.All, "alice", "bob")).ConfigureAwait(false);
        await host.PublishAsync(Approval("conf-task-seq", UserTaskCompletionPolicy.Sequential, "alice", "bob")).ConfigureAwait(false);

        var all = (await host.StartAsync("conf-task-all").ConfigureAwait(false)).ValueOrThrow();
        var aliceTask = Assert.Single(await host.PendingAsync("alice").ConfigureAwait(false));
        var afterAlice = (await host.CompleteAsync(aliceTask.Id, "alice", UserTaskOutcomes.Approved, new JsonObject { ["alice"] = 1 }).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Running, afterAlice.Status);
        Assert.Equal(1, afterAlice.Variables["alice"]!.GetValue<int>());
        var bobTask = Assert.Single(await host.PendingAsync("bob").ConfigureAwait(false));
        var afterBob = (await host.CompleteAsync(bobTask.Id, "bob", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((WorkflowInstanceStatus.Completed, "approved"), (afterBob.Status, afterBob.Outcome));

        var rejected = (await host.StartAsync("conf-task-all").ConfigureAwait(false)).ValueOrThrow();
        var bobReject = Assert.Single((await host.PendingAsync("bob").ConfigureAwait(false)).Where(task => task.InstanceId == rejected.Id));
        var afterReject = (await host.CompleteAsync(bobReject.Id, "bob", UserTaskOutcomes.Rejected).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((WorkflowInstanceStatus.Completed, "rejected"), (afterReject.Status, afterReject.Outcome));
        Assert.Empty((await host.PendingAsync("alice").ConfigureAwait(false)).Where(task => task.InstanceId == rejected.Id));

        var sequential = (await host.StartAsync("conf-task-seq").ConfigureAwait(false)).ValueOrThrow();
        Assert.Single((await host.PendingAsync("alice").ConfigureAwait(false)).Where(task => task.InstanceId == sequential.Id));
        Assert.Empty((await host.PendingAsync("bob").ConfigureAwait(false)).Where(task => task.InstanceId == sequential.Id));
        var first = Assert.Single((await host.PendingAsync("alice").ConfigureAwait(false)).Where(task => task.InstanceId == sequential.Id));
        Assert.Equal(WorkflowInstanceStatus.Running, (await host.CompleteAsync(first.Id, "alice", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow().Status);
        var second = Assert.Single((await host.PendingAsync("bob").ConfigureAwait(false)).Where(task => task.InstanceId == sequential.Id));
        Assert.Equal(WorkflowInstanceStatus.Completed, (await host.CompleteAsync(second.Id, "bob", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow().Status);
    }

    /// <summary>
    /// 用例 6：转办把任务交给目标（目标已是指派人被拒）。
    /// </summary>
    /// <remarks>
    /// 加签立即给新人任务（已有的被拒）。
    /// </remarks>
    [Fact]
    public async Task UserTask_TransferAndAddAssignees()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await host.PublishAsync(Approval("conf-task-transfer", UserTaskCompletionPolicy.All, "alice", "bob")).ConfigureAwait(false);
        var instance = (await host.StartAsync("conf-task-transfer").ConfigureAwait(false)).ValueOrThrow();
        var alice = Assert.Single(await host.PendingAsync("alice").ConfigureAwait(false));

        Assert.Equal(WorkflowErrors.AssigneeExists, (await host.TransferAsync(alice.Id, "alice", "bob").ConfigureAwait(false)).Error?.Code);
        Assert.Equal(WorkflowErrors.TaskDenied, (await host.TransferAsync(alice.Id, "bob", "carol").ConfigureAwait(false)).Error?.Code);
        Assert.True((await host.TransferAsync(alice.Id, "alice", "carol").ConfigureAwait(false)).IsSuccess);
        Assert.Empty(await host.PendingAsync("alice").ConfigureAwait(false));
        var carol = Assert.Single(await host.PendingAsync("carol").ConfigureAwait(false));
        Assert.Equal(alice.Id, carol.Id);
        var transferred = Assert.Single(log.EventsOf(WorkflowTopics.TaskTransferred));
        Assert.Equal(("alice", "carol"), (transferred.ActorId, transferred.AssigneeId));

        Assert.Equal(WorkflowErrors.AssigneeExists, (await host.AddAssigneesAsync(carol.Id, "carol", "bob").ConfigureAwait(false)).Error?.Code);
        Assert.True((await host.AddAssigneesAsync(carol.Id, "carol", "dave", "bob").ConfigureAwait(false)).IsSuccess);
        var dave = Assert.Single(await host.PendingAsync("dave").ConfigureAwait(false));
        Assert.Equal(instance.Id, dave.InstanceId);

        // 会签：carol、bob、dave 三人都通过才完成
        Assert.Equal(WorkflowInstanceStatus.Running, (await host.CompleteAsync(carol.Id, "carol", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow().Status);
        Assert.Equal(WorkflowInstanceStatus.Running, (await host.CompleteAsync(dave.Id, "dave", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow().Status);
        var bob = Assert.Single(await host.PendingAsync("bob").ConfigureAwait(false));
        Assert.Equal(WorkflowInstanceStatus.Completed, (await host.CompleteAsync(bob.Id, "bob", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow().Status);
    }

    /// <summary>
    /// 用例 7：延时以定时书签挂起，到期轮询后完成。
    /// </summary>
    /// <remarks>
    /// 用户任务超时以 <c>timeout</c> 结果完成。
    /// </remarks>
    [Fact]
    public async Task Delay_AndNodeTimeout_ResumeByPolling()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var delay = WorkflowDefinitionBuilder.Create("conf-delay")
            .AddStart().AddDelay("wait", TimeSpan.FromMilliseconds(100)).AddNode("after", RecordActivity.TypeName).AddEnd()
            .AddTransition("start", "wait").AddTransition("wait", "after").AddTransition("after", "end")
            .Build();
        await host.PublishAsync(delay).ConfigureAwait(false);
        var timeout = WorkflowDefinitionBuilder.Create("conf-timeout")
            .AddStart()
            .AddUserTask("approve", null, node => node.WithAssigneeRule(AssigneeRuleTypes.User, "alice").WithTimeout(TimeSpan.FromMilliseconds(100)))
            .AddDecision("check").AddEnd("timed", outcome: "timed").AddEnd("done", outcome: "done")
            .AddTransition("start", "approve").AddTransition("approve", "check")
            .AddTransition("check", "timed", "outcome == 'timeout'").AddDefaultTransition("check", "done")
            .Build();
        await host.PublishAsync(timeout).ConfigureAwait(false);

        var waiting = (await host.StartAsync("conf-delay").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Running, waiting.Status);
        Assert.Equal(BookmarkKinds.Timer, Assert.Single(await host.BookmarksAsync(waiting.Id).ConfigureAwait(false)).Kind);
        var tasked = (await host.StartAsync("conf-timeout").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal([BookmarkKinds.NodeTimeout, BookmarkKinds.UserTask], (await host.BookmarksAsync(tasked.Id).ConfigureAwait(false)).Select(static bookmark => bookmark.Kind).Order(StringComparer.Ordinal));

        await WaitUntilAsync(async () =>
        {
            await host.PollAsync().ConfigureAwait(false);
            return (await host.InstanceAsync(waiting.Id).ConfigureAwait(false)).IsFinal && (await host.InstanceAsync(tasked.Id).ConfigureAwait(false)).IsFinal;
        }).ConfigureAwait(false);

        Assert.Equal(WorkflowInstanceStatus.Completed, (await host.InstanceAsync(waiting.Id).ConfigureAwait(false)).Status);
        Assert.Contains(log.Executed, entry => entry.InstanceId == waiting.Id && entry.NodeId == "after");
        var timedOut = await host.InstanceAsync(tasked.Id).ConfigureAwait(false);
        Assert.Equal((WorkflowInstanceStatus.Completed, "timed"), (timedOut.Status, timedOut.Outcome));
        Assert.Empty(await host.PendingAsync("alice").ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 8：信号按名字与关联消费书签，载荷成为输出。
    /// </summary>
    /// <remarks>
    /// 关联不匹配的不消费。
    /// </remarks>
    [Fact]
    public async Task Signal_ResumesMatchingBookmarks()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var definition = WorkflowDefinitionBuilder.Create("conf-signal")
            .AddStart()
            .AddNode("wait", ActivityTypes.WaitSignal, configure: node => node.WithProperty(WaitSignalActivity.SignalNameProperty, "paid"))
            .AddEnd()
            .AddTransition("start", "wait").AddTransition("wait", "end")
            .Build();
        await host.PublishAsync(definition).ConfigureAwait(false);

        var noCorrelation = await host.StartAsync("conf-signal").ConfigureAwait(false);
        Assert.Equal(WorkflowInstanceStatus.Faulted, noCorrelation.ValueOrThrow().Status);

        var one = (await host.StartAsync("conf-signal", correlationId: "order-1").ConfigureAwait(false)).ValueOrThrow();
        var two = (await host.StartAsync("conf-signal", correlationId: "order-2").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(1, (await host.SignalAsync("paid", new JsonObject { ["amount"] = 9 }, "order-1").ConfigureAwait(false)).ValueOrThrow());
        var first = await host.InstanceAsync(one.Id).ConfigureAwait(false);
        Assert.Equal(WorkflowInstanceStatus.Completed, first.Status);
        Assert.Equal(9, first.Variables["amount"]!.GetValue<int>());
        Assert.Equal(WorkflowInstanceStatus.Running, (await host.InstanceAsync(two.Id).ConfigureAwait(false)).Status);
        Assert.Equal(0, (await host.SignalAsync("other").ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal(1, (await host.SignalAsync("paid").ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal(WorkflowInstanceStatus.Completed, (await host.InstanceAsync(two.Id).ConfigureAwait(false)).Status);
    }

    /// <summary>
    /// 用例 9：子流程等待回调并取回子实例变量。
    /// </summary>
    /// <remarks>
    /// ForEach 顺序逐项启动子流程并汇总结果。
    /// </remarks>
    [Fact]
    public async Task SubWorkflow_AndForEach_RunChildren()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var child = WorkflowDefinitionBuilder.Create("conf-child")
            .AddStart()
            .AddNode("double", ActivityTypes.SetVariable, configure: node => node.WithProperty(SetVariableActivity.ExpressionsProperty, new JsonObject { ["result"] = "item * 2" }))
            .AddEnd()
            .AddTransition("start", "double").AddTransition("double", "end")
            .Build();
        await host.PublishAsync(child).ConfigureAwait(false);
        var parent = WorkflowDefinitionBuilder.Create("conf-parent")
            .AddStart()
            .AddNode("sub", ActivityTypes.SubWorkflow, configure: node => node
                .WithProperty(SubWorkflowActivity.DefinitionNameProperty, "conf-child")
                .WithProperty(SubWorkflowActivity.VariablesProperty, new JsonObject { ["item"] = 21 })
                .WithProperty(SubWorkflowActivity.ResultVariableProperty, "child"))
            .AddNode("each", ActivityTypes.ForEach, configure: node => node
                .WithProperty(ForEachActivity.ItemsProperty, "items")
                .WithProperty(SubWorkflowActivity.DefinitionNameProperty, "conf-child")
                .WithProperty(SubWorkflowActivity.ResultVariableProperty, "results"))
            .AddEnd()
            .AddTransition("start", "sub").AddTransition("sub", "each").AddTransition("each", "end")
            .Build();
        await host.PublishAsync(parent).ConfigureAwait(false);

        var instance = (await host.StartAsync("conf-parent", new JsonObject { ["items"] = new JsonArray(1, 2, 3) }, correlationId: "doc-9").ConfigureAwait(false)).ValueOrThrow();
        await WaitUntilAsync(async () => (await host.InstanceAsync(instance.Id).ConfigureAwait(false)).IsFinal).ConfigureAwait(false);
        var done = await host.InstanceAsync(instance.Id).ConfigureAwait(false);
        Assert.Equal(WorkflowInstanceStatus.Completed, done.Status);
        Assert.Equal(42, done.Variables["child"]!["result"]!.GetValue<decimal>());
        Assert.Equal([2m, 4m, 6m], ((JsonArray)done.Variables["results"]!).Select(static item => item!["result"]!.GetValue<decimal>()));

        var children = await host.ListInstancesAsync(new WorkflowInstanceQuery { ParentInstanceId = instance.Id }).ConfigureAwait(false);
        Assert.Equal(4, children.Count);
        Assert.All(children, child => Assert.Equal((WorkflowInstanceStatus.Completed, "doc-9", 1), (child.Status, child.CorrelationId, child.Depth)));
    }

    /// <summary>
    /// 用例 10：重试策略按退避重试（到期轮询）。
    /// </summary>
    /// <remarks>
    /// 用尽后故障；<c>RetryAsync</c> 从故障节点再来；继续出错以 <c>error</c> 流出并记 <c>lastError</c>。
    /// </remarks>
    [Fact]
    public async Task Faults_RetryPolicy_ManualRetry_AndContinueOnError()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var retrying = WorkflowDefinitionBuilder.Create("conf-retry")
            .AddStart()
            .AddNode("flaky", RecordActivity.TypeName, configure: node => node.WithProperty(RecordActivity.FailTimesProperty, 3).WithRetry(2, TimeSpan.FromMilliseconds(50), 1))
            .AddEnd()
            .AddTransition("start", "flaky").AddTransition("flaky", "end")
            .Build();
        await host.PublishAsync(retrying).ConfigureAwait(false);
        var continuing = WorkflowDefinitionBuilder.Create("conf-continue")
            .AddStart()
            .AddNode("broken", RecordActivity.TypeName, configure: node => node.WithProperty(RecordActivity.FailTimesProperty, 9).WithContinueOnError())
            .AddDecision("check").AddEnd("errored", outcome: "errored").AddEnd("fine", outcome: "fine")
            .AddTransition("start", "broken").AddTransition("broken", "check")
            .AddTransition("check", "errored", "outcome == 'error' && lastError.code == 'WORKFLOW_UNEXPECTED_001'").AddDefaultTransition("check", "fine")
            .Build();
        await host.PublishAsync(continuing).ConfigureAwait(false);

        var instance = (await host.StartAsync("conf-retry").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(BookmarkKinds.Retry, Assert.Single(await host.BookmarksAsync(instance.Id).ConfigureAwait(false)).Kind);
        Assert.Contains(log.EventsOf(WorkflowTopics.NodeFaulted), data => data.InstanceId == instance.Id && data.WillRetry == true);
        await WaitUntilAsync(async () =>
        {
            await host.PollAsync().ConfigureAwait(false);
            return (await host.InstanceAsync(instance.Id).ConfigureAwait(false)).IsFinal;
        }).ConfigureAwait(false);
        var faulted = await host.InstanceAsync(instance.Id).ConfigureAwait(false);
        Assert.Equal((WorkflowInstanceStatus.Faulted, "flaky"), (faulted.Status, faulted.FaultNodeId));
        Assert.Equal([1, 2], log.Executed.Where(entry => entry.InstanceId == instance.Id).Select(static entry => entry.Attempt));

        // 手工重试：第 3 次仍失败（FailTimes = 3，重试策略已用尽）→ 再故障；第 4 次成功
        var retried = (await host.EngineAsync(engine => engine.RetryAsync(instance.Id, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Faulted, retried.Status);
        var recovered = (await host.EngineAsync(engine => engine.RetryAsync(instance.Id, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Completed, recovered.Status);
        Assert.Equal([1, 2, 3, 4], log.Executed.Where(entry => entry.InstanceId == instance.Id).Select(static entry => entry.Attempt));
        Assert.Equal(WorkflowErrors.InstanceStatus, (await host.EngineAsync(engine => engine.RetryAsync(instance.Id, CancellationToken.None)).ConfigureAwait(false)).Error?.Code);

        var continued = (await host.StartAsync("conf-continue").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((WorkflowInstanceStatus.Completed, "errored"), (continued.Status, continued.Outcome));
        Assert.Equal(WorkflowErrors.ActivityFailed, continued.Variables[WorkflowEngine.LastErrorVariable]!["code"]!.GetValue<string>());
    }

    /// <summary>
    /// 用例 11：挂起的实例不接收到期书签（轮询后推），恢复后接收。
    /// </summary>
    /// <remarks>
    /// 取消清掉书签并补偿，终止不补偿；已结束的幂等；状态不允许的报冲突。
    /// </remarks>
    [Fact]
    public async Task Control_SuspendResumeCancelTerminate()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var definition = WorkflowDefinitionBuilder.Create("conf-control")
            .WithCompensation()
            .AddStart().AddNode("done", RecordActivity.TypeName).AddDelay("wait", TimeSpan.FromMilliseconds(50)).AddNode("after", RecordActivity.TypeName).AddEnd()
            .AddTransition("start", "done").AddTransition("done", "wait").AddTransition("wait", "after").AddTransition("after", "end")
            .Build();
        await host.PublishAsync(definition).ConfigureAwait(false);

        var suspended = (await host.StartAsync("conf-control").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Suspended, (await host.EngineAsync(engine => engine.SuspendAsync(suspended.Id, "hold", CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow().Status);
        Assert.Equal(WorkflowErrors.InstanceStatus, (await host.EngineAsync(engine => engine.SuspendAsync(suspended.Id, null, CancellationToken.None)).ConfigureAwait(false)).Error?.Code);
        await Task.Delay(100).ConfigureAwait(false);
        await host.PollAsync().ConfigureAwait(false);
        Assert.Equal(WorkflowInstanceStatus.Suspended, (await host.InstanceAsync(suspended.Id).ConfigureAwait(false)).Status);
        var deferred = Assert.Single(await host.BookmarksAsync(suspended.Id).ConfigureAwait(false));
        Assert.True(deferred.DueAt > DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(WorkflowInstanceStatus.Running, (await host.EngineAsync(engine => engine.ResumeInstanceAsync(suspended.Id, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow().Status);

        var canceled = (await host.StartAsync("conf-control").ConfigureAwait(false)).ValueOrThrow();
        var afterCancel = (await host.EngineAsync(engine => engine.CancelAsync(canceled.Id, "stop", CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((WorkflowInstanceStatus.Canceled, "stop"), (afterCancel.Status, afterCancel.CancellationReason));
        Assert.Empty(await host.BookmarksAsync(canceled.Id).ConfigureAwait(false));
        Assert.Equal(["done"], log.Compensated);
        Assert.Contains((await host.NodeInstancesAsync(canceled.Id).ConfigureAwait(false)), node => node.NodeId == "done" && node.Status == WorkflowNodeInstanceStatus.Compensated);
        Assert.Contains((await host.NodeInstancesAsync(canceled.Id).ConfigureAwait(false)), node => node.NodeId == "wait" && node.Status == WorkflowNodeInstanceStatus.Canceled);
        Assert.Equal(WorkflowInstanceStatus.Canceled, (await host.EngineAsync(engine => engine.CancelAsync(canceled.Id, null, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow().Status);
        Assert.Equal(WorkflowErrors.InstanceStatus, (await host.EngineAsync(engine => engine.SuspendAsync(canceled.Id, null, CancellationToken.None)).ConfigureAwait(false)).Error?.Code);

        var terminated = (await host.StartAsync("conf-control").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Terminated, (await host.EngineAsync(engine => engine.TerminateAsync(terminated.Id, "kill", CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow().Status);
        Assert.Equal(["done"], log.Compensated);
        Assert.Equal(WorkflowErrors.InstanceNotFound, (await host.EngineAsync(engine => engine.CancelAsync("missing", null, CancellationToken.None)).ConfigureAwait(false)).Error?.Code);
        Assert.Contains(log.EventsOf(WorkflowTopics.InstanceCanceled), data => data.InstanceId == canceled.Id && data.Reason == "stop");
        Assert.Contains(log.EventsOf(WorkflowTopics.InstanceTerminated), data => data.InstanceId == terminated.Id);
    }

    /// <summary>
    /// 用例 12：定义生命周期。
    /// </summary>
    /// <remarks>
    /// 不合法的草稿发布失败；发布后不能改；新版本是草稿；停用后不能启动；删除只允许草稿；结束节点带结果时结果处理器失败即故障。
    /// </remarks>
    [Fact]
    public async Task Definitions_Lifecycle_AndOutcomeHandlers()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var broken = WorkflowDefinitionBuilder.Create("conf-lifecycle")
            .AddStart().AddNode("x", "NoSuchActivity").AddEnd()
            .AddTransition("start", "x").AddTransition("x", "end").AddTransition("end", "start")
            .Build();
        var (draft, published) = await host.InScopeAsync(async sp =>
        {
            var catalog = sp.GetRequiredService<IWorkflowDefinitionCatalog>();
            var created = (await catalog.CreateAsync(broken, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            Assert.Equal((1, WorkflowDefinitionStatus.Draft), (created.Version, created.Status));
            var rejected = await catalog.PublishAsync(created.Id, CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(WorkflowErrors.DefinitionInvalid, rejected.Error?.Code);
            Assert.Contains("NoSuchActivity", rejected.Error!.Details!["errors"]!.ToString(), StringComparison.Ordinal);

            var fixedDraft = created with
            {
                Nodes = [new WorkflowNode { Id = "start", ActivityType = ActivityTypes.Start }, new WorkflowNode { Id = "end", ActivityType = ActivityTypes.End, Properties = new JsonObject { [EndActivity.OutcomeProperty] = "{{ verdict }}" } }],
                Transitions = [new WorkflowTransition { Id = "s-e", SourceNodeId = "start", TargetNodeId = "end" }],
            };
            var updated = (await catalog.UpdateAsync(fixedDraft, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            var live = (await catalog.PublishAsync(updated.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            Assert.Equal(WorkflowDefinitionStatus.Published, live.Status);
            Assert.NotNull(live.PublishedAt);
            Assert.Equal(WorkflowErrors.DefinitionStatus, (await catalog.UpdateAsync(live, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
            Assert.Equal(WorkflowErrors.DefinitionStatus, (await catalog.DeleteAsync(live.Id, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
            var next = (await catalog.NewVersionAsync("conf-lifecycle", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            Assert.Equal((2, WorkflowDefinitionStatus.Draft), (next.Version, next.Status));
            Assert.Equal(live.Id, (await catalog.FindPublishedAsync("conf-lifecycle", null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().Id);
            Assert.False((await catalog.FindPublishedAsync("conf-lifecycle", 2, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
            return (next, live);
        }).ConfigureAwait(false);

        var ok = (await host.StartAsync("conf-lifecycle", new JsonObject { ["verdict"] = "accepted" }).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((WorkflowInstanceStatus.Completed, "accepted"), (ok.Status, ok.Outcome));
        var failingHandler = (await host.StartAsync("conf-lifecycle", new JsonObject { ["verdict"] = RecordingOutcomeHandler.FailingOutcome }).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((WorkflowInstanceStatus.Faulted, "CONFORMANCE_CONFLICT_001"), (failingHandler.Status, failingHandler.Fault?.Code));
        Assert.Equal(["accepted", RecordingOutcomeHandler.FailingOutcome], log.Outcomes);

        await host.InScopeAsync(async sp =>
        {
            var catalog = sp.GetRequiredService<IWorkflowDefinitionCatalog>();
            Assert.Equal(WorkflowDefinitionStatus.Disabled, (await catalog.DisableAsync(published.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Status);
            Assert.Equal(WorkflowErrors.DefinitionStatus, (await catalog.DisableAsync(published.Id, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
            Assert.True((await catalog.DeleteAsync(draft.Id, CancellationToken.None).ConfigureAwait(false)).IsSuccess);
            Assert.Equal(WorkflowErrors.DefinitionNotFound, (await catalog.PublishAsync("missing", CancellationToken.None).ConfigureAwait(false)).Error?.Code);
            return true;
        }).ConfigureAwait(false);
        Assert.Equal(WorkflowErrors.DefinitionNotFound, (await host.StartAsync("conf-lifecycle").ConfigureAwait(false)).Error?.Code);
    }

    /// <summary>
    /// 用例 13：一个波次执行的节点数超过上限时实例故障（死循环保护），且不可重试。
    /// </summary>
    [Fact]
    public async Task RunawayLoop_FaultsInstance()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log), [new KeyValuePair<string, string?>($"{WorkflowOptions.ConfigurationSection}:MaxNodeExecutionsPerBurst", "20")]).ConfigureAwait(false);
        var definition = WorkflowDefinitionBuilder.Create("conf-loop")
            .AddStart().AddNode("a", RecordActivity.TypeName).AddNode("b", RecordActivity.TypeName)
            .AddTransition("start", "a").AddTransition("a", "b").AddTransition("b", "a")
            .Build();
        await host.PublishAsync(definition).ConfigureAwait(false);

        var instance = (await host.StartAsync("conf-loop").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        Assert.Null(instance.FaultNodeInstanceId);
        Assert.True(log.Executed.Count(entry => entry.InstanceId == instance.Id) <= 20);
        Assert.Equal(WorkflowErrors.InstanceStatus, (await host.EngineAsync(engine => engine.RetryAsync(instance.Id, CancellationToken.None)).ConfigureAwait(false)).Error?.Code);
    }

    /// <summary>
    /// 用例 14：租户隔离。
    /// </summary>
    /// <remarks>
    /// 租户启动的实例，平台的调用查不到它的任务、发不到它的信号；租户自己的待办只有自己的。
    /// </remarks>
    [Fact]
    public async Task Tenant_IsolatesTasksAndSignals()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var waiting = WorkflowDefinitionBuilder.Create("conf-tenant-signal")
            .AddStart().AddNode("wait", ActivityTypes.WaitSignal, configure: node => node.WithProperty(WaitSignalActivity.SignalNameProperty, "conf.tenant.go").WithProperty(WaitSignalActivity.AcceptAnyCorrelationProperty, true)).AddEnd()
            .AddTransition("start", "wait").AddTransition("wait", "end")
            .Build();
        await host.PublishManyAsync(null, Approval("conf-tenant", UserTaskCompletionPolicy.Any, "alice"), waiting).ConfigureAwait(false);
        await host.PublishManyAsync("t1", Approval("conf-tenant", UserTaskCompletionPolicy.Any, "alice"), waiting).ConfigureAwait(false);

        var tenantInstance = (await host.StartAsync("conf-tenant", tenantId: "t1").ConfigureAwait(false)).ValueOrThrow();
        var plainInstance = (await host.StartAsync("conf-tenant").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal("t1", tenantInstance.TenantId);
        Assert.Equal("t1", Assert.Single(await host.BookmarksAsync(tenantInstance.Id, "t1").ConfigureAwait(false)).TenantId);

        Assert.Equal([plainInstance.Id], (await host.PendingAsync("alice").ConfigureAwait(false)).Select(static task => task.InstanceId));
        Assert.Equal([tenantInstance.Id], (await host.PendingAsync("alice", "t1").ConfigureAwait(false)).Select(static task => task.InstanceId));
        var tenantTask = Assert.Single(await host.BookmarksAsync(tenantInstance.Id, "t1").ConfigureAwait(false));
        Assert.Equal(WorkflowErrors.BookmarkNotFound, (await host.CompleteAsync(tenantTask.Id, "alice", UserTaskOutcomes.Approved).ConfigureAwait(false)).Error?.Code);

        // 信号只到调用方分区：平台的信号恢复平台的实例，t1 的不动
        var tenantWaiting = (await host.StartAsync("conf-tenant-signal", tenantId: "t1").ConfigureAwait(false)).ValueOrThrow();
        var plainWaiting = (await host.StartAsync("conf-tenant-signal").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(1, (await host.SignalAsync("conf.tenant.go").ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal(WorkflowInstanceStatus.Completed, (await host.InstanceAsync(plainWaiting.Id).ConfigureAwait(false)).Status);
        Assert.Equal(WorkflowInstanceStatus.Running, (await host.InstanceAsync(tenantWaiting.Id, "t1").ConfigureAwait(false)).Status);
    }

    /// <summary>
    /// 用例 15：事件网关。
    /// </summary>
    /// <remarks>
    /// 先到的信号决定出路并带出载荷，其余书签清掉；没有信号时按超时流出。
    /// </remarks>
    [Fact]
    public async Task EventGateway_TakesFirstSignalOrTimeout()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var definition = WorkflowDefinitionBuilder.Create("conf-gateway")
            .AddStart()
            .AddEventGateway("wait", ["approved", "rejected"], TimeSpan.FromMilliseconds(100), acceptAnyCorrelation: true)
            .AddDecision("check")
            .AddEnd("ok", outcome: "ok").AddEnd("no", outcome: "no").AddEnd("late", outcome: "late")
            .AddTransition("start", "wait").AddTransition("wait", "check")
            .AddTransition("check", "ok", "outcome == 'approved'").AddTransition("check", "no", "outcome == 'rejected'").AddDefaultTransition("check", "late")
            .Build();
        await host.PublishAsync(definition).ConfigureAwait(false);
        var signaled = (await host.StartAsync("conf-gateway").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal([BookmarkKinds.Signal, BookmarkKinds.Signal, BookmarkKinds.Timer], (await host.BookmarksAsync(signaled.Id).ConfigureAwait(false)).Select(static bookmark => bookmark.Kind).Order(StringComparer.Ordinal));
        Assert.Equal(1, (await host.SignalAsync("rejected", new JsonObject { ["why"] = "budget" }).ConfigureAwait(false)).ValueOrThrow());
        var decided = await host.InstanceAsync(signaled.Id).ConfigureAwait(false);
        Assert.Equal((WorkflowInstanceStatus.Completed, "no"), (decided.Status, decided.Outcome));
        Assert.Equal("rejected", decided.Variables[EventGatewayActivity.SignalOutput]!.GetValue<string>());
        Assert.Equal("budget", decided.Variables[EventGatewayActivity.PayloadOutput]!["why"]!.GetValue<string>());
        Assert.Empty(await host.BookmarksAsync(signaled.Id).ConfigureAwait(false));
        var timed = (await host.StartAsync("conf-gateway").ConfigureAwait(false)).ValueOrThrow();
        await WaitUntilAsync(async () =>
        {
            await host.PollAsync().ConfigureAwait(false);
            return (await host.InstanceAsync(timed.Id).ConfigureAwait(false)).IsFinal;
        }).ConfigureAwait(false);
        var timedOut = await host.InstanceAsync(timed.Id).ConfigureAwait(false);
        Assert.Equal((WorkflowInstanceStatus.Completed, "late", EventGatewayActivity.TimeoutOutcome), (timedOut.Status, timedOut.Outcome, timedOut.Variables[EventGatewayActivity.SignalOutput]!.GetValue<string>()));
    }

    /// <summary>
    /// 用例 16：HTTP 活动。
    /// </summary>
    /// <remarks>
    /// 模板 URL 与请求体、响应解析为对象、输出映射取值；非 2xx 默认故障，关掉后以 error 标签继续。
    /// </remarks>
    [Fact]
    public async Task Http_SendsRequests_AndMapsResponses()
    {
        var log = new ConformanceLog();
        var handler = new StubHttpHandler();
        await using var host = await StartAsync(services =>
        {
            Register(services, log);
            services.AddSingleton(new WorkflowHttpClient(new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }));
        }).ConfigureAwait(false);
        var definition = WorkflowDefinitionBuilder.Create("conf-http")
            .AddStart()
            .AddHttp("create", "https://api.test/orders/{{ orderId }}", "POST", configure: node => node
                .WithProperty(HttpActivity.BodyProperty, "{\"qty\": {{ qty }}}")
                .WithProperty(HttpActivity.ContentTypeProperty, "application/json")
                .WithProperty(HttpActivity.HeadersProperty, new JsonObject { ["X-Trace"] = "{{ orderId }}" })
                .WithProperty(HttpActivity.ResultVariableProperty, "created")
                .WithOutputMapping("total", "outputs.created.body.total"))
            .AddHttp("soft", "https://api.test/fail", configure: node => node.WithProperty(HttpActivity.FailOnErrorStatusProperty, false).WithProperty(HttpActivity.ResultVariableProperty, "soft"))
            .AddHttp("hard", "https://api.test/fail")
            .AddEnd()
            .AddTransition("start", "create").AddTransition("create", "soft").AddTransition("soft", "hard").AddTransition("hard", "end")
            .Build();
        await host.PublishAsync(definition).ConfigureAwait(false);
        var instance = (await host.StartAsync("conf-http", new JsonObject { ["orderId"] = "o-7", ["qty"] = 3 }).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        Assert.True(instance.FaultNodeId == "hard", instance.FaultNodeId + ": " + instance.Fault?.Code + " " + instance.Fault?.Message);
        Assert.Equal(("POST", "https://api.test/orders/o-7", "{\"qty\": 3}", "o-7"), handler.Requests[0]);
        Assert.Equal((200, 21), (instance.Variables["created"]!["status"]!.GetValue<int>(), instance.Variables["created"]!["body"]!["total"]!.GetValue<int>()));
        Assert.Equal(21, instance.Variables["total"]!.GetValue<int>());
        Assert.Equal(500, instance.Variables["soft"]!["status"]!.GetValue<int>());
        var nodes = await host.NodeInstancesAsync(instance.Id).ConfigureAwait(false);
        Assert.Equal("error", nodes.Single(static node => node.NodeId == "soft").Outputs[UserTaskPayloadKeys.Outcome]!.GetValue<string>());
        var invalid = WorkflowDefinitionBuilder.Create("conf-http-bad").AddStart().AddHttp("h", "https://api.test", "TRACE").AddTransition("start", "h").Build();
        var created = await host.InScopeAsync(sp => sp.GetRequiredService<IWorkflowDefinitionCatalog>().CreateAsync(invalid, CancellationToken.None)).ConfigureAwait(false);
        var published = await host.InScopeAsync(sp => sp.GetRequiredService<IWorkflowDefinitionCatalog>().PublishAsync([created.ValueOrThrow().Id], CancellationToken.None)).ConfigureAwait(false);
        Assert.Equal(WorkflowErrors.DefinitionInvalid, published[0].Error?.Code);
    }

    /// <summary>
    /// 用例 17：补偿活动逆序补偿指定的已完成节点后继续。
    /// </summary>
    /// <remarks>
    /// 故障补偿在实例故障时补偿全部已完成节点。
    /// </remarks>
    [Fact]
    public async Task Compensate_ActivityAndOnFault()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var explicitly = WorkflowDefinitionBuilder.Create("conf-compensate")
            .AddStart().AddNode("a", RecordActivity.TypeName).AddNode("b", RecordActivity.TypeName).AddCompensate("undo", ["a"]).AddNode("c", RecordActivity.TypeName).AddEnd()
            .AddTransition("start", "a").AddTransition("a", "b").AddTransition("b", "undo").AddTransition("undo", "c").AddTransition("c", "end")
            .Build();
        await host.PublishAsync(explicitly).ConfigureAwait(false);
        var compensated = (await host.StartAsync("conf-compensate").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Completed, compensated.Status);
        Assert.Equal(["a"], log.Compensated);
        var nodes = await host.NodeInstancesAsync(compensated.Id).ConfigureAwait(false);
        Assert.Equal(WorkflowNodeInstanceStatus.Compensated, nodes.Single(static node => node.NodeId == "a").Status);
        Assert.Equal(WorkflowNodeInstanceStatus.Completed, nodes.Single(static node => node.NodeId == "b").Status);
        Assert.Contains(nodes.Single(static node => node.NodeId == "undo").State[CompensateActivity.CompensatedState]!.AsArray(), item => item!.GetValue<string>() == nodes.Single(static node => node.NodeId == "a").Id);
        var onFault = WorkflowDefinitionBuilder.Create("conf-compensate-fault")
            .WithCompensateOnFault()
            .AddStart().AddNode("a", RecordActivity.TypeName).AddNode("b", RecordActivity.TypeName).AddNode("boom", RecordActivity.TypeName, configure: node => node.WithProperty(RecordActivity.FailTimesProperty, 9)).AddEnd()
            .AddTransition("start", "a").AddTransition("a", "b").AddTransition("b", "boom").AddTransition("boom", "end")
            .Build();
        await host.PublishAsync(onFault).ConfigureAwait(false);
        var faulted = (await host.StartAsync("conf-compensate-fault").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Faulted, faulted.Status);
        Assert.Equal(["a", "b", "a"], log.Compensated);
        Assert.All((await host.NodeInstancesAsync(faulted.Id).ConfigureAwait(false)).Where(static node => node.NodeId is "a" or "b"), static node => Assert.Equal(WorkflowNodeInstanceStatus.Compensated, node.Status));
    }

    /// <summary>
    /// 用例 18：输出映射以节点输出为作用域求值写入变量。
    /// </summary>
    /// <remarks>
    /// 表达式不合法在发布时拒绝，求值失败即节点故障。
    /// </remarks>
    [Fact]
    public async Task OutputMappings_WriteVariables_AndFailLoudly()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var definition = WorkflowDefinitionBuilder.Create("conf-mapping")
            .AddStart()
            .AddNode("a", RecordActivity.TypeName, configure: node => node.WithProperty(RecordActivity.OutputsProperty, new JsonObject { ["y"] = 2 }).WithOutputMapping("doubled", "outputs.y * 2").WithOutputMapping("tagged", "'y=' + toString(outputs.y)"))
            .AddNode("b", RecordActivity.TypeName, configure: node => node.WithOutputMapping("broken", "nothing.here"))
            .AddEnd()
            .AddTransition("start", "a").AddTransition("a", "b").AddTransition("b", "end")
            .Build();
        await host.PublishAsync(definition).ConfigureAwait(false);
        var instance = (await host.StartAsync("conf-mapping").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((WorkflowInstanceStatus.Faulted, "b"), (instance.Status, instance.FaultNodeId));
        Assert.Equal(WorkflowErrors.ExpressionInvalid, instance.Fault?.Code);
        Assert.Equal((4, "y=2"), (instance.Variables["doubled"]!.GetValue<decimal>(), instance.Variables["tagged"]!.GetValue<string>()));
        var invalid = WorkflowDefinitionBuilder.Create("conf-mapping-bad").AddStart().AddNode("a", RecordActivity.TypeName, configure: node => node.WithOutputMapping("x", "outputs.y +")).AddTransition("start", "a").Build();
        var created = await host.InScopeAsync(sp => sp.GetRequiredService<IWorkflowDefinitionCatalog>().CreateAsync(invalid, CancellationToken.None)).ConfigureAwait(false);
        var published = await host.InScopeAsync(sp => sp.GetRequiredService<IWorkflowDefinitionCatalog>().PublishAsync([created.ValueOrThrow().Id], CancellationToken.None)).ConfigureAwait(false);
        Assert.Equal(WorkflowErrors.DefinitionInvalid, published[0].Error?.Code);
    }

    /// <summary>
    /// 用例 19：实例级超时到期按定义的动作故障或终止，书签清空并发布超时事件。
    /// </summary>
    [Fact]
    public async Task InstanceTimeout_FaultsOrTerminates()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await host.PublishAsync(TimingOut("conf-timeout-fault", WorkflowTimeoutAction.Fault)).ConfigureAwait(false);
        await host.PublishAsync(TimingOut("conf-timeout-terminate", WorkflowTimeoutAction.Terminate)).ConfigureAwait(false);
        var faulting = (await host.StartAsync("conf-timeout-fault").ConfigureAwait(false)).ValueOrThrow();
        var terminating = (await host.StartAsync("conf-timeout-terminate").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal([BookmarkKinds.InstanceTimeout, BookmarkKinds.Timer], (await host.BookmarksAsync(faulting.Id).ConfigureAwait(false)).Select(static bookmark => bookmark.Kind).Order(StringComparer.Ordinal));
        await WaitUntilAsync(async () =>
        {
            await host.PollAsync().ConfigureAwait(false);
            return (await host.InstanceAsync(faulting.Id).ConfigureAwait(false)).IsFinal && (await host.InstanceAsync(terminating.Id).ConfigureAwait(false)).IsFinal;
        }).ConfigureAwait(false);
        var faulted = await host.InstanceAsync(faulting.Id).ConfigureAwait(false);
        Assert.Equal((WorkflowInstanceStatus.Faulted, WorkflowErrors.ActivityFailed), (faulted.Status, faulted.Fault?.Code));
        Assert.Empty(await host.BookmarksAsync(faulting.Id).ConfigureAwait(false));
        var terminated = await host.InstanceAsync(terminating.Id).ConfigureAwait(false);
        Assert.Equal((WorkflowInstanceStatus.Terminated, "timeout"), (terminated.Status, terminated.CancellationReason));
        Assert.Equal(2, log.EventsOf(WorkflowTopics.InstanceTimedOut).Count(data => data.InstanceId == faulting.Id || data.InstanceId == terminating.Id));
        // 正常完成的实例不会再被超时书签打扰
        var quick = WorkflowDefinitionBuilder.Create("conf-timeout-quick").WithTimeout(TimeSpan.FromMilliseconds(50)).AddStart().AddNode("a", RecordActivity.TypeName).AddEnd().AddTransition("start", "a").AddTransition("a", "end").Build();
        await host.PublishAsync(quick).ConfigureAwait(false);
        var done = (await host.StartAsync("conf-timeout-quick").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Completed, done.Status);
        Assert.Empty(await host.BookmarksAsync(done.Id).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 20：延迟启动在到期前只有开始节点的定时书签。
    /// </summary>
    /// <remarks>
    /// 重用策略按关联 ID 拒绝或返回已有实例。
    /// </remarks>
    [Fact]
    public async Task DelayedStart_AndReusePolicy()
    {
        var log = new ConformanceLog();
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        await using var host = await StartAsync(services =>
        {
            Register(services, log);
            services.AddSingleton<TimeProvider>(clock);
        }).ConfigureAwait(false);
        var definition = WorkflowDefinitionBuilder.Create("conf-start")
            .AddStart().AddNode("a", RecordActivity.TypeName).AddDelay("wait", TimeSpan.FromHours(1)).AddEnd()
            .AddTransition("start", "a").AddTransition("a", "wait").AddTransition("wait", "end")
            .Build();
        await host.PublishAsync(definition).ConfigureAwait(false);
        var delayed = (await host.EngineAsync(engine => engine.StartAsync(new WorkflowStartRequest { Name = "conf-start", StartAt = clock.GetUtcNow().AddMinutes(1) }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Running, delayed.Status);
        var startBookmark = Assert.Single(await host.BookmarksAsync(delayed.Id).ConfigureAwait(false));
        Assert.Equal((BookmarkKinds.Timer, "start"), (startBookmark.Kind, startBookmark.NodeId));
        Assert.DoesNotContain(log.Executed, entry => entry.InstanceId == delayed.Id);
        // 到期前轮询不启动；推进到启动时刻后轮询才启动
        await host.PollAsync().ConfigureAwait(false);
        Assert.DoesNotContain(log.Executed, entry => entry.InstanceId == delayed.Id);
        clock.Advance(TimeSpan.FromMinutes(1));
        await WaitUntilAsync(async () =>
        {
            await host.PollAsync().ConfigureAwait(false);
            return log.Executed.Any(entry => entry.InstanceId == delayed.Id && entry.NodeId == "a");
        }).ConfigureAwait(false);
        Assert.Equal("wait", Assert.Single(await host.BookmarksAsync(delayed.Id).ConfigureAwait(false)).NodeId);
        var first = (await host.StartAsync("conf-start", correlationId: "dup-1").ConfigureAwait(false)).ValueOrThrow();
        var rejected = await host.EngineAsync(engine => engine.StartAsync(new WorkflowStartRequest { Name = "conf-start", CorrelationId = "dup-1", ReusePolicy = WorkflowReusePolicy.RejectActive }, CancellationToken.None)).ConfigureAwait(false);
        Assert.Equal(WorkflowErrors.ActiveInstanceExists, rejected.Error?.Code);
        var returned = (await host.EngineAsync(engine => engine.StartAsync(new WorkflowStartRequest { Name = "conf-start", CorrelationId = "dup-1", ReusePolicy = WorkflowReusePolicy.ReturnActive }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(first.Id, returned.Id);
        var duplicate = (await host.EngineAsync(engine => engine.StartAsync(new WorkflowStartRequest { Name = "conf-start", CorrelationId = "dup-1" }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow();
        Assert.NotEqual(first.Id, duplicate.Id);
        Assert.True((await host.EngineAsync(engine => engine.StartAsync(new WorkflowStartRequest { Name = "conf-start", ReusePolicy = WorkflowReusePolicy.RejectActive }, CancellationToken.None)).ConfigureAwait(false)).IsSuccess);
    }

    /// <summary>
    /// 用例 21：信号启动为声明了该信号的已发布定义启动实例。
    /// </summary>
    /// <remarks>
    /// 定时启动在发布时排定义级书签，到期由轮询启动并再次排期，停用后不再排。
    /// </remarks>
    [Fact]
    public async Task SignalStart_AndCronStart()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var bySignal = WorkflowDefinitionBuilder.Create("conf-signal-start")
            .AddNode("start", ActivityTypes.Start, configure: node => node.WithProperty(StartActivity.SignalNameProperty, "order.created"))
            .AddNode("a", RecordActivity.TypeName).AddEnd()
            .AddTransition("start", "a").AddTransition("a", "end")
            .Build();
        await host.PublishAsync(bySignal).ConfigureAwait(false);
        Assert.Equal(1, (await host.SignalAsync("order.created", new JsonObject { ["amount"] = 5 }, "ord-9").ConfigureAwait(false)).ValueOrThrow());
        var started = Assert.Single(log.EventsOf(WorkflowTopics.InstanceStarted).Where(static data => data.DefinitionName == "conf-signal-start"));
        var instance = await host.InstanceAsync(started.InstanceId).ConfigureAwait(false);
        Assert.Equal((WorkflowInstanceStatus.Completed, "ord-9", 5), (instance.Status, instance.CorrelationId, instance.Variables[WorkflowEngine.SignalVariable]!["amount"]!.GetValue<int>()));
        Assert.Equal(0, (await host.SignalAsync("order.deleted").ConfigureAwait(false)).ValueOrThrow());
        var byCron = WorkflowDefinitionBuilder.Create("conf-cron-start")
            .AddNode("start", ActivityTypes.Start, configure: node => node.WithProperty(StartActivity.CronProperty, "* * * * * *"))
            .AddNode("a", RecordActivity.TypeName).AddEnd()
            .AddTransition("start", "a").AddTransition("a", "end")
            .Build();
        var published = await host.PublishAsync(byCron).ConfigureAwait(false);
        var timer = Assert.Single(await host.BookmarksAsync(published.Id).ConfigureAwait(false));
        Assert.Equal((BookmarkKinds.DefinitionTimer, published.Id), (timer.Kind, timer.InstanceId));
        await WaitUntilAsync(async () =>
        {
            await host.PollAsync().ConfigureAwait(false);
            return log.EventsOf(WorkflowTopics.InstanceStarted).Any(static data => data.DefinitionName == "conf-cron-start");
        }).ConfigureAwait(false);
        var rescheduled = Assert.Single(await host.BookmarksAsync(published.Id).ConfigureAwait(false));
        Assert.NotEqual(timer.Id, rescheduled.Id);
        Assert.True(rescheduled.DueAt > timer.DueAt);
        var disabled = await host.InScopeAsync(sp => sp.GetRequiredService<IWorkflowDefinitionCatalog>().DisableAsync([published.Id], CancellationToken.None)).ConfigureAwait(false);
        Assert.True(disabled[0].IsSuccess);
        Assert.Empty(await host.BookmarksAsync(published.Id).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 22：改变量、跳转到指定节点、迁移到兼容的新版本。
    /// </summary>
    /// <remarks>
    /// 不兼容的版本与不存在的节点被拒绝。
    /// </remarks>
    [Fact]
    public async Task SetVariables_Jump_AndMigrate()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var v1 = WorkflowDefinitionBuilder.Create("conf-migrate")
            .AddStart()
            .AddUserTask("first", null, node => node.WithAssigneeRule(AssigneeRuleTypes.User, "alice"))
            .AddUserTask("second", null, node => node.WithAssigneeRule(AssigneeRuleTypes.User, "bob"))
            .AddEnd()
            .AddTransition("start", "first").AddTransition("first", "second").AddTransition("second", "end")
            .Build();
        var published = await host.PublishAsync(v1).ConfigureAwait(false);
        var instance = (await host.StartAsync("conf-migrate", new JsonObject { ["k"] = 1 }).ConfigureAwait(false)).ValueOrThrow();
        var changed = (await host.EngineAsync(engine => engine.SetVariablesAsync(new WorkflowVariableUpdate(instance.Id, new JsonObject { ["m"] = 2 }, ["k"]), CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow();
        Assert.False(changed.Variables.ContainsKey("k"));
        Assert.Equal(2, changed.Variables["m"]!.GetValue<int>());
        var jumped = (await host.EngineAsync(engine => engine.JumpAsync(new WorkflowJump(instance.Id, "second", "skip", new JsonObject { ["j"] = true }, "admin"), CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Running, jumped.Status);
        Assert.Empty(await host.PendingAsync("alice").ConfigureAwait(false));
        Assert.Single(await host.PendingAsync("bob").ConfigureAwait(false));
        var nodes = await host.NodeInstancesAsync(instance.Id).ConfigureAwait(false);
        Assert.Equal(WorkflowNodeInstanceStatus.Canceled, nodes.Single(static node => node.NodeId == "first").Status);
        Assert.Contains(log.EventsOf(WorkflowTopics.InstanceJumped), data => data.InstanceId == instance.Id && data.NodeId == "second" && data.ActorId == "admin");
        Assert.Equal(WorkflowErrors.InputInvalid, (await host.EngineAsync(engine => engine.JumpAsync(new WorkflowJump(instance.Id, "nowhere"), CancellationToken.None)).ConfigureAwait(false)).Error?.Code);
        // 新版本保留 second 节点：可迁移；去掉 second 的版本：不兼容
        var v2 = (await host.InScopeAsync(sp => sp.GetRequiredService<IWorkflowDefinitionCatalog>().NewVersionAsync("conf-migrate", CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow();
        var v2Published = (await host.InScopeAsync(sp => sp.GetRequiredService<IWorkflowDefinitionCatalog>().PublishAsync([v2.Id], CancellationToken.None)).ConfigureAwait(false))[0].ValueOrThrow();
        var migrated = (await host.EngineAsync(engine => engine.MigrateAsync(new WorkflowMigration(instance.Id, v2Published.Id), CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((v2Published.Id, 2), (migrated.DefinitionId, migrated.DefinitionVersion));
        Assert.Contains(log.EventsOf(WorkflowTopics.InstanceMigrated), data => data.InstanceId == instance.Id && data.DefinitionVersion == 2);
        var v3 = WorkflowDefinitionBuilder.Create("conf-migrate").AddStart().AddUserTask("first", null, node => node.WithAssigneeRule(AssigneeRuleTypes.User, "alice")).AddEnd().AddTransition("start", "first").AddTransition("first", "end").Build();
        var v3Published = await host.PublishAsync(v3).ConfigureAwait(false);
        Assert.Equal(WorkflowErrors.InputInvalid, (await host.EngineAsync(engine => engine.MigrateAsync(new WorkflowMigration(instance.Id, v3Published.Id), CancellationToken.None)).ConfigureAwait(false)).Error?.Code);
        Assert.Equal(WorkflowErrors.DefinitionNotFound, (await host.EngineAsync(engine => engine.MigrateAsync(new WorkflowMigration(instance.Id, published.Id + "-missing"), CancellationToken.None)).ConfigureAwait(false)).Error?.Code);
        // 迁移后仍能按新版本完成
        var task = Assert.Single(await host.PendingAsync("bob").ConfigureAwait(false));
        Assert.Equal(WorkflowInstanceStatus.Completed, (await host.CompleteAsync(task.Id, "bob", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow().Status);
        Assert.Equal(WorkflowErrors.InstanceStatus, (await host.EngineAsync(engine => engine.SetVariablesAsync(new WorkflowVariableUpdate(instance.Id, new JsonObject { ["x"] = 1 }), CancellationToken.None)).ConfigureAwait(false)).Error?.Code);
    }

    /// <summary>
    /// 用例 23：清理删除早于时刻结束的实例及其节点与书签，运行中的不动。
    /// </summary>
    /// <remarks>
    /// 延时节点支持到时刻与 cron。
    /// </remarks>
    [Fact]
    public async Task Purge_AndDelayVariants()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var linear = WorkflowDefinitionBuilder.Create("conf-purge").AddStart().AddNode("a", RecordActivity.TypeName).AddEnd().AddTransition("start", "a").AddTransition("a", "end").Build();
        await host.PublishAsync(linear).ConfigureAwait(false);
        var waiting = WorkflowDefinitionBuilder.Create("conf-purge-wait").AddStart().AddDelay("wait", TimeSpan.FromHours(1)).AddEnd().AddTransition("start", "wait").AddTransition("wait", "end").Build();
        await host.PublishAsync(waiting).ConfigureAwait(false);
        var ended = new List<string>();
        for (var index = 0; index < 3; index++)
        {
            ended.Add((await host.StartAsync("conf-purge").ConfigureAwait(false)).ValueOrThrow().Id);
        }

        var running = (await host.StartAsync("conf-purge-wait").ConfigureAwait(false)).ValueOrThrow();
        var purged = (await host.EngineAsync(engine => engine.PurgeAsync(new WorkflowPurgeRequest { EndedBefore = DateTimeOffset.UtcNow.AddSeconds(1), DefinitionName = "conf-purge" }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(3, purged);
        foreach (var id in ended)
        {
            Assert.False(await host.InstanceExistsAsync(id).ConfigureAwait(false));
        }

        Assert.True(await host.InstanceExistsAsync(running.Id).ConfigureAwait(false));
        Assert.Equal(0, (await host.EngineAsync(engine => engine.PurgeAsync(new WorkflowPurgeRequest { EndedBefore = DateTimeOffset.UtcNow.AddSeconds(1) }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow());
        var until = WorkflowDefinitionBuilder.Create("conf-delay-until")
            .AddStart().AddNode("wait", ActivityTypes.Delay, configure: node => node.WithProperty(DelayActivity.UntilProperty, "'2000-01-01T00:00:00Z'")).AddNode("a", RecordActivity.TypeName).AddEnd()
            .AddTransition("start", "wait").AddTransition("wait", "a").AddTransition("a", "end")
            .Build();
        await host.PublishAsync(until).ConfigureAwait(false);
        var cron = WorkflowDefinitionBuilder.Create("conf-delay-cron")
            .AddStart().AddNode("wait", ActivityTypes.Delay, configure: node => node.WithProperty(DelayActivity.CronProperty, "* * * * * *")).AddNode("a", RecordActivity.TypeName).AddEnd()
            .AddTransition("start", "wait").AddTransition("wait", "a").AddTransition("a", "end")
            .Build();
        await host.PublishAsync(cron).ConfigureAwait(false);
        var untilInstance = (await host.StartAsync("conf-delay-until").ConfigureAwait(false)).ValueOrThrow();
        var cronInstance = (await host.StartAsync("conf-delay-cron").ConfigureAwait(false)).ValueOrThrow();
        Assert.True(Assert.Single(await host.BookmarksAsync(cronInstance.Id).ConfigureAwait(false)).DueAt <= DateTimeOffset.UtcNow.AddSeconds(1.5));
        await WaitUntilAsync(async () =>
        {
            await host.PollAsync().ConfigureAwait(false);
            return (await host.InstanceAsync(untilInstance.Id).ConfigureAwait(false)).IsFinal && (await host.InstanceAsync(cronInstance.Id).ConfigureAwait(false)).IsFinal;
        }).ConfigureAwait(false);
        Assert.Equal(WorkflowInstanceStatus.Completed, (await host.InstanceAsync(untilInstance.Id).ConfigureAwait(false)).Status);
        Assert.Equal(WorkflowInstanceStatus.Completed, (await host.InstanceAsync(cronInstance.Id).ConfigureAwait(false)).Status);
        var descriptors = await host.InScopeAsync(sp => ValueTask.FromResult(sp.GetRequiredService<IWorkflowDefinitionCatalog>().ActivityTypes())).ConfigureAwait(false);
        var http = Assert.Single(descriptors, static descriptor => descriptor.Type == ActivityTypes.Http);
        Assert.Equal(BuiltInActivityDescriptors.IntegrationCategory, http.Category);
        Assert.Contains(http.Inputs, static input => input.Name == HttpActivity.UrlProperty && input.Required);
        Assert.Contains(descriptors, static descriptor => descriptor.Type == RecordActivity.TypeName);
    }

    /// <summary>
    /// 用例 24：两个并发的拒绝重复启动只建出一个实例。
    /// </summary>
    /// <remarks>
    /// 探针把第一个启动卡在查重之后、写入之前，等第二个启动也走到查重处或在实例锁上等待时再放行。查重与写入原子时第二个启动看到第一个建出的实例，以 <c>WORKFLOW_CONFLICT_005</c> 失败，同一关联 ID 下只有一个未结束的实例。
    /// </remarks>
    [Fact]
    public async Task ReusePolicy_ConcurrentRejectActiveStarts_CreateOneInstance()
    {
        var log = new ConformanceLog();
        var probe = new StartRaceProbe();
        await using var host = await StartAsync(services =>
        {
            Register(services, log);
            probe.Install(services);
        }).ConfigureAwait(false);
        await host.PublishAsync(WorkflowDefinitionBuilder.Create("conf-start-race")
            .AddStart().AddDelay("wait", TimeSpan.FromHours(1)).AddEnd()
            .AddTransition("start", "wait").AddTransition("wait", "end")
            .Build()).ConfigureAwait(false);
        var request = new WorkflowStartRequest { Name = "conf-start-race", CorrelationId = "race-1", ReusePolicy = WorkflowReusePolicy.RejectActive };

        var first = StartRaceProbe.Run(StartRaceProbe.Racer.First, () => host.EngineAsync(engine => engine.StartAsync(request, CancellationToken.None)).AsTask());
        await probe.FirstQueried.WaitAsync(WaitTimeout).ConfigureAwait(false);
        var second = StartRaceProbe.Run(StartRaceProbe.Racer.Second, () => host.EngineAsync(engine => engine.StartAsync(request, CancellationToken.None)).AsTask());
        await probe.SecondArrived.WaitAsync(WaitTimeout).ConfigureAwait(false);
        probe.Release();
        var results = await Task.WhenAll(first, second).ConfigureAwait(false);

        Assert.Single(results, static result => result.IsSuccess);
        Assert.Single(results, static result => result.Error?.Code == WorkflowErrors.ActiveInstanceExists);
        var instances = await host.ListInstancesAsync(new WorkflowInstanceQuery { DefinitionName = new WorkflowName("conf-start-race"), CorrelationIds = ["race-1"] }).ConfigureAwait(false);
        Assert.Equal(results.Single(static result => result.IsSuccess).ValueOrThrow().Id, Assert.Single(instances).Id);
    }

    /// <summary>
    /// 用例 25：定义按分区隔离。
    /// </summary>
    /// <remarks>
    /// 平台与各租户各是一个分区：同名定义各自从版本 1 编号；按名字或 ID 启动、目录的查找、列表、按 ID 的改动与新版本都看不到别的分区的定义；平台的定义对租户不可见（没有全局定义），租户的定义对平台也不可见；迁移只在同一分区里；重用策略只在同一分区里查重。
    /// </remarks>
    [Fact]
    public async Task Definitions_AreIsolatedPerPartition()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);

        // 同名定义各自编号：t1 发布 v1，t2 只有 v1 草稿
        var t1 = await AsTenantAsync(host, "t1", sp => PublishAsync(sp, Labeled("conf-tenant-def", "t1"))).ConfigureAwait(false);
        var t2 = await AsTenantAsync(host, "t2", async sp => (await sp.GetRequiredService<IWorkflowDefinitionCatalog>().CreateAsync(Labeled("conf-tenant-def", "t2"), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow()).ConfigureAwait(false);
        Assert.Equal((1, "t1"), (t1.Version, t1.TenantId));
        Assert.Equal((1, "t2"), (t2.Version, t2.TenantId));

        // t2 启动不到 t1 的定义：按名字、按 ID 都是不存在
        Assert.Equal(WorkflowErrors.DefinitionNotFound, (await AsTenantAsync(host, "t2", sp => StartByNameAsync(sp, "conf-tenant-def")).ConfigureAwait(false)).Error?.Code);
        Assert.Equal(WorkflowErrors.DefinitionNotFound, (await AsTenantAsync(host, "t2", sp => sp.GetRequiredService<IWorkflowEngine>().StartAsync(new WorkflowStartRequest { DefinitionId = t1.Id }, CancellationToken.None)).ConfigureAwait(false)).Error?.Code);
        var started = (await AsTenantAsync(host, "t1", sp => StartByNameAsync(sp, "conf-tenant-def")).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((t1.Id, "t1"), (started.DefinitionId, started.TenantId));

        // t2 的目录看不到、改不动 t1 的定义；新版本以 t2 自己的最新版本为底；不能把定义建到别的租户名下
        await AsTenantAsync(host, "t2", async sp =>
        {
            var catalog = sp.GetRequiredService<IWorkflowDefinitionCatalog>();
            Assert.False((await catalog.FindPublishedAsync("conf-tenant-def", null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
            Assert.False((await catalog.FindPublishedAsync("conf-tenant-def", 1, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
            Assert.False((await catalog.FindAsync(t1.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
            var listed = (await catalog.QueryAsync(new WorkflowDefinitionQuery { Name = new WorkflowName("conf-tenant-def") }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            Assert.Equal([t2.Id], listed.Items.Select(static definition => definition.Id));
            Assert.Equal(1L, listed.Total);
            Assert.Equal(WorkflowErrors.DefinitionNotFound, (await catalog.DisableAsync(t1.Id, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
            Assert.Equal(WorkflowErrors.DefinitionNotFound, (await catalog.ArchiveAsync(t1.Id, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
            Assert.Equal(WorkflowErrors.DefinitionNotFound, (await catalog.UpdateAsync(t1 with { DisplayName = "taken" }, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
            Assert.Equal(WorkflowErrors.DefinitionNotFound, (await catalog.CreateAsync(Labeled("conf-tenant-def", "forged") with { TenantId = "t1" }, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
            var next = (await catalog.NewVersionAsync("conf-tenant-def", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            Assert.Equal((2, "t2", "t2"), (next.Version, next.TenantId, next.Nodes.Single(static node => node.Id == "a").Properties[RecordActivity.LabelProperty]!.GetValue<string>()));
            return true;
        }).ConfigureAwait(false);
        Assert.Equal(WorkflowDefinitionStatus.Published, (await AsTenantAsync(host, "t1", async sp => (await sp.GetRequiredService<IWorkflowDefinitionCatalog>().FindAsync(t1.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow()).ConfigureAwait(false)).Status);

        // 平台的定义对租户不可见：租户按名字、按 ID 都启动不到；租户发布同名定义从版本 1 编号，与平台的互不相干
        var platform = await AsTenantAsync(host, null, sp => PublishAsync(sp, Labeled("conf-tenant-shared", "platform"))).ConfigureAwait(false);
        Assert.Equal((1, (string?)null), (platform.Version, platform.TenantId));
        Assert.Equal(WorkflowErrors.DefinitionNotFound, (await AsTenantAsync(host, "t2", sp => StartByNameAsync(sp, "conf-tenant-shared")).ConfigureAwait(false)).Error?.Code);
        Assert.Equal(WorkflowErrors.DefinitionNotFound, (await AsTenantAsync(host, "t2", sp => sp.GetRequiredService<IWorkflowEngine>().StartAsync(new WorkflowStartRequest { DefinitionId = platform.Id }, CancellationToken.None)).ConfigureAwait(false)).Error?.Code);
        var own = await AsTenantAsync(host, "t2", sp => PublishAsync(sp, Labeled("conf-tenant-shared", "t2"))).ConfigureAwait(false);
        Assert.Equal((1, "t2"), (own.Version, own.TenantId));
        Assert.Equal(own.Id, (await AsTenantAsync(host, "t2", sp => StartByNameAsync(sp, "conf-tenant-shared")).ConfigureAwait(false)).ValueOrThrow().DefinitionId);
        var platformStarted = (await AsTenantAsync(host, null, sp => StartByNameAsync(sp, "conf-tenant-shared")).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((platform.Id, (string?)null), (platformStarted.DefinitionId, platformStarted.TenantId));

        // 平台同样看不到、改不动租户的定义，也不能把定义建到租户名下
        await AsTenantAsync(host, null, async sp =>
        {
            var catalog = sp.GetRequiredService<IWorkflowDefinitionCatalog>();
            Assert.Equal(0, (await catalog.QueryAsync(new WorkflowDefinitionQuery { Keyword = "conf-tenant-def" }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Total);
            Assert.False((await catalog.FindAsync(own.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
            Assert.Equal(WorkflowErrors.DefinitionNotFound, (await catalog.DisableAsync(own.Id, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
            Assert.Equal(WorkflowErrors.DefinitionNotFound, (await catalog.CreateAsync(Labeled("conf-tenant-def", "forged") with { TenantId = "t1" }, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
            Assert.Equal(WorkflowErrors.DefinitionNotFound, (await sp.GetRequiredService<IWorkflowEngine>().StartAsync(new WorkflowStartRequest { DefinitionId = t1.Id }, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
            return true;
        }).ConfigureAwait(false);

        // 迁移只在同一分区里：t1 的实例不能迁到平台的同名定义上
        var waitingPlatform = await AsTenantAsync(host, null, sp => PublishAsync(sp, Waiting("conf-tenant-migrate"))).ConfigureAwait(false);
        var waitingOwn = await AsTenantAsync(host, "t1", sp => PublishAsync(sp, Waiting("conf-tenant-migrate"))).ConfigureAwait(false);
        var running = (await AsTenantAsync(host, "t1", sp => sp.GetRequiredService<IWorkflowEngine>().StartAsync(new WorkflowStartRequest { DefinitionId = waitingOwn.Id }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((WorkflowInstanceStatus.Running, "t1"), (running.Status, running.TenantId));
        Assert.Equal(WorkflowErrors.DefinitionNotFound, (await AsTenantAsync(host, "t1", sp => sp.GetRequiredService<IWorkflowEngine>().MigrateAsync(new WorkflowMigration(running.Id, waitingPlatform.Id), CancellationToken.None)).ConfigureAwait(false)).Error?.Code);

        // 重用策略只在同一分区里查重：别的租户同一关联 ID 的实例既不挡住启动，也不会被当成已有实例返回
        var waitingT2 = await AsTenantAsync(host, "t2", sp => PublishAsync(sp, Waiting("conf-tenant-migrate"))).ConfigureAwait(false);
        var reuse = new WorkflowStartRequest { Name = "conf-tenant-migrate", CorrelationId = "conf-doc", ReusePolicy = WorkflowReusePolicy.RejectActive };
        var inT1 = (await AsTenantAsync(host, "t1", sp => sp.GetRequiredService<IWorkflowEngine>().StartAsync(reuse, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow();
        var inT2 = (await AsTenantAsync(host, "t2", sp => sp.GetRequiredService<IWorkflowEngine>().StartAsync(reuse with { ReusePolicy = WorkflowReusePolicy.ReturnActive }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow();
        Assert.NotEqual(inT1.Id, inT2.Id);
        Assert.Equal(("t1", "t2", waitingT2.Id), (inT1.TenantId, inT2.TenantId, inT2.DefinitionId));
        Assert.Equal(WorkflowErrors.ActiveInstanceExists, (await AsTenantAsync(host, "t1", sp => sp.GetRequiredService<IWorkflowEngine>().StartAsync(reuse, CancellationToken.None)).ConfigureAwait(false)).Error?.Code);
    }

    /// <summary>
    /// 用例 26：调用只看得到、操作得了自己分区的实例与书签。
    /// </summary>
    /// <remarks>
    /// 租户 t2 与平台对 t1 的实例：按实例 ID 的控制、按书签 ID 的恢复、用户任务与外部任务的操作都按不存在处理，查询只给自己分区的，清理只清自己分区的；t1 照常操作自己的实例。平台不再看得到、操作得了租户的实例。
    /// </remarks>
    [Fact]
    public async Task Instances_AreIsolatedPerPartition()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var approvalDefinition = Approval("conf-tenant-instance", UserTaskCompletionPolicy.Any, "alice");
        var externalDefinition = WorkflowDefinitionBuilder.Create("conf-tenant-external")
            .AddStart().AddExternalTask("pay", "conf.tenant.pay").AddEnd()
            .AddTransition("start", "pay").AddTransition("pay", "end")
            .Build();
        foreach (var tenant in new[] { null, "t1", "t2" })
        {
            await AsTenantAsync(host, tenant, sp => PublishAsync(sp, approvalDefinition)).ConfigureAwait(false);
            await AsTenantAsync(host, tenant, sp => PublishAsync(sp, externalDefinition)).ConfigureAwait(false);
        }

        var t1 = (await AsTenantAsync(host, "t1", sp => StartByNameAsync(sp, approvalDefinition.Name.Value)).ConfigureAwait(false)).ValueOrThrow();
        var t2 = (await AsTenantAsync(host, "t2", sp => StartByNameAsync(sp, approvalDefinition.Name.Value)).ConfigureAwait(false)).ValueOrThrow();
        var platform = (await AsTenantAsync(host, null, sp => StartByNameAsync(sp, approvalDefinition.Name.Value)).ConfigureAwait(false)).ValueOrThrow();
        var t1Task = Assert.Single(await host.BookmarksAsync(t1.Id, "t1").ConfigureAwait(false));
        var t1External = (await AsTenantAsync(host, "t1", sp => StartByNameAsync(sp, externalDefinition.Name.Value)).ConfigureAwait(false)).ValueOrThrow();
        var fetched = Assert.Single((await AsTenantAsync(host, "t1", sp => sp.GetRequiredService<IWorkflowExternalTaskService>().FetchAsync("conf.tenant.pay", "w1", TimeSpan.FromMinutes(5), 10, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow());

        // t2 与平台对 t1 的实例：按实例 ID 的控制一律不存在
        foreach (var outsider in new[] { "t2", null })
        {
            var mine = outsider is null ? platform : t2;
            await AsTenantAsync(host, outsider, async sp =>
            {
                var engine = sp.GetRequiredService<IWorkflowEngine>();
                Assert.Equal(WorkflowErrors.InstanceNotFound, (await engine.SuspendAsync(t1.Id, "x", CancellationToken.None).ConfigureAwait(false)).Error?.Code);
                Assert.Equal(WorkflowErrors.InstanceNotFound, (await engine.ResumeInstanceAsync(t1.Id, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
                Assert.Equal(WorkflowErrors.InstanceNotFound, (await engine.CancelAsync(t1.Id, "x", CancellationToken.None).ConfigureAwait(false)).Error?.Code);
                Assert.Equal(WorkflowErrors.InstanceNotFound, (await engine.TerminateAsync(t1.Id, "x", CancellationToken.None).ConfigureAwait(false)).Error?.Code);
                Assert.Equal(WorkflowErrors.InstanceNotFound, (await engine.RetryAsync(t1.Id, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
                Assert.Equal(WorkflowErrors.InstanceNotFound, (await engine.SetVariablesAsync(new WorkflowVariableUpdate(t1.Id, new JsonObject { ["x"] = 1 }), CancellationToken.None).ConfigureAwait(false)).Error?.Code);
                Assert.Equal(WorkflowErrors.InstanceNotFound, (await engine.JumpAsync(new WorkflowJump(t1.Id, "end"), CancellationToken.None).ConfigureAwait(false)).Error?.Code);
                Assert.Equal(WorkflowErrors.InstanceNotFound, (await engine.MigrateAsync(new WorkflowMigration(t1.Id, mine.DefinitionId), CancellationToken.None).ConfigureAwait(false)).Error?.Code);

                // 按书签 ID 恢复、用户任务与外部任务的操作都看不到 t1 的书签
                Assert.Equal(WorkflowErrors.BookmarkNotFound, (await engine.ResumeAsync(t1Task.Id, null, null, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
                var tasks = sp.GetRequiredService<IWorkflowTaskService>();
                Assert.Equal(WorkflowErrors.BookmarkNotFound, (await tasks.CompleteAsync(new UserTaskCompletion(t1Task.Id, "alice", UserTaskOutcomes.Approved, null, null), CancellationToken.None).ConfigureAwait(false)).Error?.Code);
                Assert.Equal(WorkflowErrors.BookmarkNotFound, (await tasks.TransferAsync(new UserTaskTransfer(t1Task.Id, "alice", "bob"), CancellationToken.None).ConfigureAwait(false)).Error?.Code);
                Assert.Equal(WorkflowErrors.BookmarkNotFound, (await tasks.AddAssigneesAsync(new UserTaskAddition(t1Task.Id, "alice", ["bob"]), CancellationToken.None).ConfigureAwait(false)).Error?.Code);
                Assert.Equal([mine.Id], (await tasks.PendingAsync("alice", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static task => task.InstanceId));
                var externals = sp.GetRequiredService<IWorkflowExternalTaskService>();
                Assert.Equal(WorkflowErrors.BookmarkNotFound, (await externals.CompleteAsync(new ExternalTaskCompletion(fetched.Id, "w1", null), CancellationToken.None).ConfigureAwait(false)).Error?.Code);
                Assert.Equal(WorkflowErrors.BookmarkNotFound, (await externals.FailAsync(new ExternalTaskFailure(fetched.Id, "w1", "boom"), CancellationToken.None).ConfigureAwait(false)).Error?.Code);

                // 实例查询只给自己分区的：按 ID 无值，列表与计数只有自己的，节点实例不存在
                var byName = new WorkflowInstanceQuery { DefinitionName = approvalDefinition.Name };
                Assert.False((await engine.FindInstanceAsync(t1.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
                Assert.True((await engine.FindInstanceAsync(mine.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
                Assert.Equal([mine.Id], (await engine.ListInstancesAsync(byName, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Select(static instance => instance.Id));
                Assert.Equal(1L, (await engine.CountInstancesAsync(byName, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
                Assert.Equal(WorkflowErrors.InstanceNotFound, (await engine.NodeInstancesAsync(t1.Id, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
                Assert.NotEmpty((await engine.NodeInstancesAsync(mine.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
                return true;
            }).ConfigureAwait(false);
        }

        Assert.Equal(WorkflowInstanceStatus.Running, (await host.InstanceAsync(t1.Id, "t1").ConfigureAwait(false)).Status);
        Assert.Equal(WorkflowInstanceStatus.Running, (await host.InstanceAsync(t1External.Id, "t1").ConfigureAwait(false)).Status);
        Assert.False((await host.InstanceAsync(t1.Id, "t1").ConfigureAwait(false)).Variables.ContainsKey("x"));

        // 本租户照常操作：t1 挂起、终止自己的实例
        Assert.Equal(WorkflowInstanceStatus.Suspended, (await AsTenantAsync(host, "t1", sp => sp.GetRequiredService<IWorkflowEngine>().SuspendAsync(t1.Id, "pause", CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow().Status);
        Assert.Equal(WorkflowInstanceStatus.Terminated, (await AsTenantAsync(host, "t1", sp => sp.GetRequiredService<IWorkflowEngine>().TerminateAsync(t1.Id, "done", CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow().Status);

        // 清理只清自己分区的：t2 与平台清不到 t1 已结束的实例，t1 自己清得到
        var purgeAll = new WorkflowPurgeRequest { EndedBefore = DateTimeOffset.UtcNow.AddMinutes(1), DefinitionName = approvalDefinition.Name };
        Assert.Equal(0, (await AsTenantAsync(host, "t2", sp => sp.GetRequiredService<IWorkflowEngine>().PurgeAsync(purgeAll, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal(0, (await AsTenantAsync(host, null, sp => sp.GetRequiredService<IWorkflowEngine>().PurgeAsync(purgeAll, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal(WorkflowInstanceStatus.Terminated, (await host.InstanceAsync(t1.Id, "t1").ConfigureAwait(false)).Status);
        Assert.Equal(1, (await AsTenantAsync(host, "t1", sp => sp.GetRequiredService<IWorkflowEngine>().PurgeAsync(purgeAll, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow());
        Assert.False(await host.InstanceExistsAsync(t1.Id, "t1").ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 27：业务回滚时引擎的写入一起回滚。
    /// </summary>
    /// <remarks>
    /// 在调用方的调用里发起流程，之后业务失败、调用回滚：实例、节点实例与书签都不留（<see cref="JoinsCallerTransaction"/> 的 Provider）；调用提交时都在。不加入事务的 Provider（内存实现）写入立即生效，回滚不撤销，套件照实断言这一点。宿主装了发件箱时，事件随调用写进发件箱：回滚的调用发出的事件不会送达，提交的会（ADR-0096 第六节）。
    /// </remarks>
    [Fact]
    public async Task Transaction_BusinessRollback_DiscardsTheWorkflowWrites()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await host.PublishAsync(Approval("conf-tx-start", UserTaskCompletionPolicy.Any, "alice")).ConfigureAwait(false);

        string? rolledBack = null;
        await host.AsTenantAsync(null, async sp =>
        {
            rolledBack = (await StartByNameAsync(sp, "conf-tx-start").ConfigureAwait(false)).ValueOrThrow().Id;
            // 调用里读得到自己刚写的（读己之写）
            Assert.True((await sp.GetRequiredService<IWorkflowEngine>().FindInstanceAsync(rolledBack, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
            return true;
        }, rollback: true).ConfigureAwait(false);
        Assert.Equal(!JoinsCallerTransaction, await host.InstanceExistsAsync(rolledBack!).ConfigureAwait(false));
        Assert.Equal(JoinsCallerTransaction ? 0 : 1, (await host.BookmarksAsync(rolledBack!).ConfigureAwait(false)).Count);
        Assert.Equal(JoinsCallerTransaction ? 0 : 1, (await host.NodeInstancesAsync(rolledBack!).ConfigureAwait(false)).Count(static node => node.Status == WorkflowNodeInstanceStatus.Suspended));

        var committed = await host.AsTenantAsync(null, async sp => (await StartByNameAsync(sp, "conf-tx-start").ConfigureAwait(false)).ValueOrThrow().Id).ConfigureAwait(false);
        Assert.True(await host.InstanceExistsAsync(committed).ConfigureAwait(false));
        Assert.Single(await host.BookmarksAsync(committed).ConfigureAwait(false));

        // 装了发件箱：事件与引擎写入同一事务，回滚的调用发出的事件从没提交，不会送达；没装时直接发布，可能早于提交（RFC-0021「事件」），这里不断言
        if (host.Services.GetService<OutboxDispatcher>() is not null)
        {
            Assert.DoesNotContain(log.EventsOf(WorkflowTopics.InstanceStarted), data => data.InstanceId == rolledBack);
            Assert.Contains(log.EventsOf(WorkflowTopics.InstanceStarted), data => data.InstanceId == committed);
            Assert.Contains(log.EventsOf(WorkflowTopics.TaskCreated), data => data.InstanceId == committed);
        }
    }

    /// <summary>
    /// 用例 28：办理任务随业务提交或回滚。
    /// </summary>
    /// <remarks>
    /// 在调用里办理最后一个任务，流程走到结果结束节点完成；业务随后失败、调用回滚：实例仍在运行、任务仍在待办（加入事务的 Provider）。调用提交时实例完成、结果处理器收到结论。
    /// </remarks>
    [Fact]
    public async Task Transaction_TaskCompletion_CommitsOrRollsBackWithTheBusiness()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await host.PublishAsync(Approval("conf-tx-complete", UserTaskCompletionPolicy.Any, "alice")).ConfigureAwait(false);
        var instance = (await host.StartAsync("conf-tx-complete").ConfigureAwait(false)).ValueOrThrow();
        var task = Assert.Single(await host.PendingAsync("alice").ConfigureAwait(false));

        await host.AsTenantAsync(null, async sp =>
        {
            var completed = (await sp.GetRequiredService<IWorkflowTaskService>().CompleteAsync(new UserTaskCompletion(task.Id, "alice", UserTaskOutcomes.Approved, null, null), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            Assert.Equal(WorkflowInstanceStatus.Completed, completed.Status);
            return true;
        }, rollback: true).ConfigureAwait(false);
        if (JoinsCallerTransaction)
        {
            Assert.Equal(WorkflowInstanceStatus.Running, (await host.InstanceAsync(instance.Id).ConfigureAwait(false)).Status);
            Assert.Equal([task.Id], (await host.PendingAsync("alice").ConfigureAwait(false)).Select(static pending => pending.Id));
            var outcome = await host.AsTenantAsync(null, async sp => (await sp.GetRequiredService<IWorkflowTaskService>().CompleteAsync(new UserTaskCompletion(task.Id, "alice", UserTaskOutcomes.Approved, null, null), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow()).ConfigureAwait(false);
            Assert.Equal(WorkflowInstanceStatus.Completed, outcome.Status);
        }

        var done = await host.InstanceAsync(instance.Id).ConfigureAwait(false);
        Assert.Equal((WorkflowInstanceStatus.Completed, UserTaskOutcomes.Approved), (done.Status, done.Outcome));
        Assert.Empty(await host.PendingAsync("alice").ConfigureAwait(false));
        Assert.Contains(UserTaskOutcomes.Approved, log.Outcomes);
    }

    /// <summary>
    /// 用例 29：实例锁持有到调用结束，回滚后立即释放、不泄漏。
    /// </summary>
    /// <remarks>
    /// 调用里挂起一个实例后，别的调用在它结束之前改这个实例拿不到锁（<c>WORKFLOW_TIMEOUT_001</c>），不会读到提交之前的旧状态再改写；调用回滚后锁立即释放，别的调用照常操作。
    /// </remarks>
    [Fact]
    public async Task Transaction_InstanceLock_IsHeldUntilTheInvocationEnds_AndReleasedOnRollback()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await host.PublishAsync(Waiting("conf-tx-lock")).ConfigureAwait(false);
        var instance = (await host.StartAsync("conf-tx-lock").ConfigureAwait(false)).ValueOrThrow();

        await host.AsTenantAsync(null, async sp =>
        {
            Assert.Equal(WorkflowInstanceStatus.Suspended, (await sp.GetRequiredService<IWorkflowEngine>().SuspendAsync(instance.Id, "hold", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Status);
            // 同一调用里再操作同一实例：本作用域持有的锁空闲，复用它
            Assert.Equal(WorkflowInstanceStatus.Running, (await sp.GetRequiredService<IWorkflowEngine>().ResumeInstanceAsync(instance.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Status);
            // 别的调用（自己的作用域）等不到锁
            Assert.Equal(WorkflowErrors.LockTimeout, (await host.EngineAsync(engine => engine.TerminateAsync(instance.Id, "intruder", CancellationToken.None)).ConfigureAwait(false)).Error?.Code);
            return true;
        }, rollback: true).ConfigureAwait(false);

        Assert.Equal(WorkflowInstanceStatus.Running, (await host.InstanceAsync(instance.Id).ConfigureAwait(false)).Status);
        Assert.Equal(WorkflowInstanceStatus.Terminated, (await host.EngineAsync(engine => engine.TerminateAsync(instance.Id, "after", CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow().Status);
    }

    /// <summary>
    /// 用例 30：只读调用里的引擎写入在执行之前即被拒绝。
    /// </summary>
    /// <remarks>
    /// 只读的调用（动词 <c>get</c>）里启动流程：Provider 在取锁、执行活动之前返回 <c>PERSISTENCE_UNEXPECTED_001</c>，活动没有执行、什么也没写下，调用本身照常结束；查询照常。加入事务与不加入事务的 Provider 同一断言（ADR-0099）：数据库实现看工作单元，内存实现看调用的操作契约，宿主装没装发件箱都一样。
    /// </remarks>
    [Fact]
    public async Task Transaction_ReadOnlyInvocation_RejectsEngineWritesBeforeRunningActivities()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await host.PublishAsync(WorkflowDefinitionBuilder.Create("conf-tx-readonly").AddStart().AddNode("a", RecordActivity.TypeName).AddDelay("wait", TimeSpan.FromHours(1)).AddEnd()
            .AddTransition("start", "a").AddTransition("a", "wait").AddTransition("wait", "end").Build()).ConfigureAwait(false);
        var existing = (await host.StartAsync("conf-tx-readonly").ConfigureAwait(false)).ValueOrThrow();
        var executed = log.Executed.Count;

        // 自己开只读调用而不用夹具的 AsTenantAsync：要看调用本身的结果——写入被提前拒绝，什么也没发出，调用照常成功
        var started = default(Result<WorkflowInstance>);
        var ran = false;
        await using var scope = host.Services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None);
        context.SetOperation(new ReadOnlyProbeOperation());
        var invoked = await host.Services.GetRequiredService<App>().InvokeAsync(context, async invocation =>
        {
            Assert.True((await invocation.Services.GetRequiredService<IWorkflowEngine>().FindInstanceAsync(existing.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
            started = await StartByNameAsync(invocation.Services, "conf-tx-readonly").ConfigureAwait(false);
            ran = true;
        }).ConfigureAwait(false);
        Assert.True(ran);
        Assert.Equal("PERSISTENCE_UNEXPECTED_001", started.Error?.Code);
        Assert.True(invoked.IsSuccess);
        Assert.Equal(executed, log.Executed.Count);
        Assert.Single(await host.ListInstancesAsync(new WorkflowInstanceQuery { DefinitionName = new WorkflowName("conf-tx-readonly") }).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 31：到期书签轮询覆盖平台与每个启用的租户分区。
    /// </summary>
    /// <remarks>
    /// 平台、<c>t1</c>、<c>t2</c> 各有一个到期的延时：一次次轮询把它们都恢复到完成，每个都在自己的分区里；停用的租户 <c>t-off</c> 不轮询，它的延时等租户重新启用再接续。
    /// </remarks>
    [Fact]
    public async Task Poller_CoversThePlatformAndEveryEnabledTenant()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var definition = WorkflowDefinitionBuilder.Create("conf-poll-tenants").AddStart().AddDelay("wait", TimeSpan.FromMilliseconds(50)).AddEnd()
            .AddTransition("start", "wait").AddTransition("wait", "end").Build();
        var tenants = new[] { null, "t1", "t2", ConformanceTenants.Disabled };
        var started = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tenant in tenants)
        {
            // 每个分区各发布一次：定义按分区隔离，跨分区没有批量
            await host.PublishManyAsync(tenant, definition).ConfigureAwait(false);
            started[tenant ?? string.Empty] = (await host.StartAsync("conf-poll-tenants", tenantId: tenant).ConfigureAwait(false)).ValueOrThrow().Id;
        }

        await WaitUntilAsync(async () =>
        {
            await host.PollAsync().ConfigureAwait(false);
            foreach (var tenant in new[] { null, "t1", "t2" })
            {
                if (!(await host.InstanceAsync(started[tenant ?? string.Empty], tenant).ConfigureAwait(false)).IsFinal)
                {
                    return false;
                }
            }

            return true;
        }).ConfigureAwait(false);
        foreach (var tenant in new[] { null, "t1", "t2" })
        {
            var done = await host.InstanceAsync(started[tenant ?? string.Empty], tenant).ConfigureAwait(false);
            Assert.Equal((WorkflowInstanceStatus.Completed, tenant), (done.Status, done.TenantId));
        }

        await host.PollAsync().ConfigureAwait(false);
        Assert.Equal(WorkflowInstanceStatus.Running, (await host.InstanceAsync(started[ConformanceTenants.Disabled], ConformanceTenants.Disabled).ConfigureAwait(false)).Status);
    }

    /// <summary>
    /// 用例 32：实例查询的分页与清理的条数越界以校验错误返回。
    /// </summary>
    /// <remarks>
    /// 错误码 <see cref="WorkflowErrors.InputInvalid"/>，不在构造时抛出、不进存储也不开事务；上下边界照常执行，计数不看分页，被拒绝的清理什么都不删。
    /// </remarks>
    [Fact]
    public async Task InstanceQueryAndPurge_OutOfRangeLimits_AreValidationErrors()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await host.PublishAsync(Labeled("conf-paging", "a")).ConfigureAwait(false);
        var instance = (await host.StartAsync("conf-paging").ConfigureAwait(false)).ValueOrThrow();
        var byName = new WorkflowInstanceQuery { DefinitionName = new WorkflowName("conf-paging") };

        foreach (var (skip, limit) in new[] { (0, 0), (0, WorkflowInstanceQuery.MaxLimit + 1), (-1, 10) })
        {
            var rejected = await host.EngineAsync(engine => engine.ListInstancesAsync(byName with { Skip = skip, Limit = limit }, CancellationToken.None)).ConfigureAwait(false);
            Assert.Equal(WorkflowErrors.InputInvalid, rejected.Error?.Code);
            Assert.Equal(WorkflowMessages.InstanceQueryPageInvalid, rejected.Error?.MessageKey);
        }

        Assert.Equal([instance.Id], (await host.EngineAsync(engine => engine.ListInstancesAsync(byName with { Limit = 1 }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow().Select(static found => found.Id));
        Assert.Equal([instance.Id], (await host.EngineAsync(engine => engine.ListInstancesAsync(byName with { Limit = WorkflowInstanceQuery.MaxLimit }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow().Select(static found => found.Id));
        Assert.Equal(1, (await host.EngineAsync(engine => engine.CountInstancesAsync(byName with { Skip = -1, Limit = 0 }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow());

        foreach (var limit in new[] { 0, WorkflowInstanceQuery.MaxLimit + 1 })
        {
            var rejected = await host.EngineAsync(engine => engine.PurgeAsync(new WorkflowPurgeRequest { EndedBefore = DateTimeOffset.UtcNow.AddSeconds(1), DefinitionName = "conf-paging", Limit = limit }, CancellationToken.None)).ConfigureAwait(false);
            Assert.Equal(WorkflowErrors.InputInvalid, rejected.Error?.Code);
            Assert.Equal(WorkflowMessages.PurgeLimitInvalid, rejected.Error?.MessageKey);
        }

        // 被拒绝的清理什么都没删
        Assert.True(await host.InstanceExistsAsync(instance.Id).ConfigureAwait(false));
        Assert.Equal(1, (await host.EngineAsync(engine => engine.PurgeAsync(new WorkflowPurgeRequest { EndedBefore = DateTimeOffset.UtcNow.AddSeconds(1), DefinitionName = "conf-paging", Limit = 1 }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow());
    }

    /// <summary>
    /// 用例 33：目录按条件列定义摘要的一页与总数。
    /// </summary>
    /// <remarks>
    /// 分页越界以 <see cref="WorkflowErrors.InputInvalid"/>（消息键 <c>workflow.definition.query_page_invalid</c>）返回，不在构造时抛出；关键字、分类与状态在目录建出、发布后的定义上生效，名字升序、版本降序，总数不看分页（ADR-0096）。
    /// </remarks>
    [Fact]
    public async Task Catalog_Query_PagesSummaries_AndRejectsOutOfRangePages()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var expense = await host.PublishAsync(Labeled("conf-catalog-expense", "a") with { DisplayName = "Expense", Category = "finance" }).ConfigureAwait(false);
        var leave = await host.PublishAsync(Labeled("conf-catalog-leave", "a") with { DisplayName = "Leave", Category = "hr" }).ConfigureAwait(false);
        var draft = await host.AsTenantAsync(null, async sp => (await sp.GetRequiredService<IWorkflowDefinitionCatalog>().NewVersionAsync("conf-catalog-expense", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow()).ConfigureAwait(false);

        await host.AsTenantAsync(null, async sp =>
        {
            var catalog = sp.GetRequiredService<IWorkflowDefinitionCatalog>();
            foreach (var invalid in new[] { new WorkflowDefinitionQuery { Limit = 0 }, new WorkflowDefinitionQuery { Limit = WorkflowDefinitionQuery.MaxLimit + 1 }, new WorkflowDefinitionQuery { Skip = -1 } })
            {
                var rejected = await catalog.QueryAsync(invalid, CancellationToken.None).ConfigureAwait(false);
                Assert.Equal(WorkflowErrors.InputInvalid, rejected.Error?.Code);
                Assert.Equal(WorkflowMessages.DefinitionQueryPageInvalid, rejected.Error?.MessageKey);
            }

            var all = (await catalog.QueryAsync(new WorkflowDefinitionQuery { Keyword = "conf-catalog", Limit = WorkflowDefinitionQuery.MaxLimit }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            Assert.Equal([draft.Id, expense.Id, leave.Id], all.Items.Select(static summary => summary.Id));
            Assert.Equal(3, all.Total);
            Assert.Equal((2, WorkflowDefinitionStatus.Draft, "finance"), (all.Items[0].Version, all.Items[0].Status, all.Items[0].Category));

            var page = (await catalog.QueryAsync(new WorkflowDefinitionQuery { Keyword = "CONF-CATALOG", Skip = 1, Limit = 1 }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            Assert.Equal(expense.Id, Assert.Single(page.Items).Id);
            Assert.Equal(3, page.Total);

            var published = (await catalog.QueryAsync(new WorkflowDefinitionQuery { Keyword = "expense", Status = WorkflowDefinitionStatus.Published }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            Assert.Equal((expense.Id, 1L), (Assert.Single(published.Items).Id, published.Total));
            Assert.Equal([leave.Id], (await catalog.QueryAsync(new WorkflowDefinitionQuery { Category = "hr" }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Items.Select(static summary => summary.Id));
            return true;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 34：实例查询只取顶层、按一组关联 ID 与关键字过滤。
    /// </summary>
    /// <remarks>
    /// 子流程继承父实例的发起人与关联 ID，只取顶层把它们排除（「我发起的」、按单据查进度）；关键字匹配实例标题或关联 ID（同 4.x，ADR-0099），标题是启动时给的显示名，没给取定义的显示名。关联 ID 集合超过上限或有空白是 <see cref="WorkflowErrors.InputInvalid"/>（消息键 <c>workflow.instance.query_correlations_invalid</c>），列表与计数都校验；空集合列表为空、计数为 0（ADR-0096）。
    /// </remarks>
    [Fact]
    public async Task Instances_Query_TopLevelCorrelationSetAndTitleKeyword()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await host.PublishAsync(Approval("conf-q-child", UserTaskCompletionPolicy.Any, "alice")).ConfigureAwait(false);
        await host.PublishAsync(WorkflowDefinitionBuilder.Create("conf-q-parent", "Parent Flow")
            .AddStart()
            .AddNode("sub", ActivityTypes.SubWorkflow, configure: node => node.WithProperty(SubWorkflowActivity.DefinitionNameProperty, "conf-q-child"))
            .AddEnd()
            .AddTransition("start", "sub").AddTransition("sub", "end")
            .Build()).ConfigureAwait(false);

        async Task<WorkflowInstance> StartParentAsync(string correlationId, string starterId, string? displayName)
            => (await host.EngineAsync(engine => engine.StartAsync(new WorkflowStartRequest { Name = "conf-q-parent", CorrelationId = correlationId, StarterId = starterId, DisplayName = displayName }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow();

        var expense = await StartParentAsync("doc-1", "alice", "Expense 50% Claim").ConfigureAwait(false);
        var untitled = await StartParentAsync("doc-2", "alice", null).ConfigureAwait(false);
        var leave = await StartParentAsync("doc-3", "bob", "Leave_Request").ConfigureAwait(false);
        await WaitUntilAsync(async () => (await host.ListInstancesAsync(new WorkflowInstanceQuery { DefinitionName = new WorkflowName("conf-q-child") }).ConfigureAwait(false)).Count == 3).ConfigureAwait(false);

        // 子流程继承发起人与关联 ID：不只取顶层时它们也在结果里
        Assert.Equal(4, (await host.ListInstancesAsync(new WorkflowInstanceQuery { StarterId = "alice" }).ConfigureAwait(false)).Count);
        Assert.Equal([untitled.Id, expense.Id], (await host.ListInstancesAsync(new WorkflowInstanceQuery { StarterId = "alice", TopLevelOnly = true }).ConfigureAwait(false)).Select(static instance => instance.Id));
        Assert.Equal(2, (await host.EngineAsync(engine => engine.CountInstancesAsync(new WorkflowInstanceQuery { StarterId = "alice", TopLevelOnly = true }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow());

        // 一组关联 ID：重复与不存在的不影响，只取顶层时每个单据一个实例
        Assert.Equal([leave.Id, expense.Id], (await host.ListInstancesAsync(new WorkflowInstanceQuery { CorrelationIds = ["doc-1", "doc-3", "doc-1", "doc-none"], TopLevelOnly = true }).ConfigureAwait(false)).Select(static instance => instance.Id));
        Assert.Equal(4, (await host.EngineAsync(engine => engine.CountInstancesAsync(new WorkflowInstanceQuery { CorrelationIds = ["doc-1", "doc-3"] }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow());

        // 标题关键字：启动时给的显示名，没给取定义的显示名；% 与 _ 按字面
        Assert.Equal("Parent Flow", untitled.DisplayName);
        Assert.Equal([expense.Id], (await host.ListInstancesAsync(new WorkflowInstanceQuery { Keyword = "claim" }).ConfigureAwait(false)).Select(static instance => instance.Id));
        Assert.Equal([untitled.Id], (await host.ListInstancesAsync(new WorkflowInstanceQuery { Keyword = "PARENT flow" }).ConfigureAwait(false)).Select(static instance => instance.Id));
        Assert.Equal([leave.Id], (await host.ListInstancesAsync(new WorkflowInstanceQuery { Keyword = "e_r" }).ConfigureAwait(false)).Select(static instance => instance.Id));
        Assert.Empty(await host.ListInstancesAsync(new WorkflowInstanceQuery { Keyword = "50_" }).ConfigureAwait(false));
        Assert.Empty(await host.ListInstancesAsync(new WorkflowInstanceQuery { Keyword = "e＿r" }).ConfigureAwait(false));
        Assert.Equal(3, (await host.ListInstancesAsync(new WorkflowInstanceQuery { Keyword = "conf-q-child" }).ConfigureAwait(false)).Count);
        Assert.Empty(await host.ListInstancesAsync(new WorkflowInstanceQuery { Keyword = "conf-q-child", TopLevelOnly = true }).ConfigureAwait(false));

        // 关联 ID 也被关键字匹配：子流程继承关联 ID，只取顶层时每个单据一个实例；标题与关联 ID 不跨字段
        Assert.Equal(2, (await host.ListInstancesAsync(new WorkflowInstanceQuery { Keyword = "DOC-1" }).ConfigureAwait(false)).Count);
        Assert.Equal([expense.Id], (await host.ListInstancesAsync(new WorkflowInstanceQuery { Keyword = "doc-1", TopLevelOnly = true }).ConfigureAwait(false)).Select(static instance => instance.Id));
        Assert.Equal(3, (await host.EngineAsync(engine => engine.CountInstancesAsync(new WorkflowInstanceQuery { Keyword = "doc-", TopLevelOnly = true }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow());
        Assert.Empty(await host.ListInstancesAsync(new WorkflowInstanceQuery { Keyword = "flowdoc" }).ConfigureAwait(false));
        Assert.Empty(await host.ListInstancesAsync(new WorkflowInstanceQuery { Keyword = "flow doc" }).ConfigureAwait(false));

        // 关联 ID 集合越界：超过上限或有空白是校验错误，列表与计数都校验；空集合列表为空、计数为 0
        var tooMany = Enumerable.Range(0, WorkflowInstanceQuery.MaxCorrelationIds + 1).Select(static index => $"doc-{index}").ToList();
        foreach (var invalid in new[] { new WorkflowInstanceQuery { CorrelationIds = tooMany }, new WorkflowInstanceQuery { CorrelationIds = ["doc-1", " "] } })
        {
            var listed = await host.EngineAsync(engine => engine.ListInstancesAsync(invalid, CancellationToken.None)).ConfigureAwait(false);
            var counted = await host.EngineAsync(engine => engine.CountInstancesAsync(invalid, CancellationToken.None)).ConfigureAwait(false);
            Assert.Equal((WorkflowErrors.InputInvalid, WorkflowMessages.InstanceQueryCorrelationsInvalid), (listed.Error?.Code, listed.Error?.MessageKey));
            Assert.Equal((WorkflowErrors.InputInvalid, WorkflowMessages.InstanceQueryCorrelationsInvalid), (counted.Error?.Code, counted.Error?.MessageKey));
        }

        // 恰好上限个照常查询：doc-1 到 doc-3 的父实例与子实例
        Assert.Equal(6, (await host.EngineAsync(engine => engine.ListInstancesAsync(new WorkflowInstanceQuery { CorrelationIds = tooMany.Take(WorkflowInstanceQuery.MaxCorrelationIds).ToList() }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow().Count);
        Assert.Empty((await host.EngineAsync(engine => engine.ListInstancesAsync(new WorkflowInstanceQuery { CorrelationIds = [] }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal(0, (await host.EngineAsync(engine => engine.CountInstancesAsync(new WorkflowInstanceQuery { CorrelationIds = [] }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow());
    }

    /// <summary>
    /// 用例 35：引擎按实例列出全部等待点（书签）的摘要。
    /// </summary>
    /// <remarks>
    /// 并行分支同时停在用户任务（两个指派人加节点超时）、信号与延时上：摘要列出全部种类，创建时间升序，用户任务的摘要 ID 就是任务 ID；书签被消费后随之消失。只看调用方分区的实例：别的分区与不存在的实例都是 <see cref="WorkflowErrors.InstanceNotFound"/>（ADR-0096）。
    /// </remarks>
    [Fact]
    public async Task Bookmarks_ListEveryWaitPointOfTheInstance_InTheCallersPartition()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await host.PublishAsync(WorkflowDefinitionBuilder.Create("conf-waits")
            .AddStart()
            .AddParallel("fork")
            .AddUserTask("approve", "Approve", static node => node.WithAssigneeRule(AssigneeRuleTypes.User, "alice", "bob").WithTimeout(TimeSpan.FromHours(1)))
            .AddNode("signal", ActivityTypes.WaitSignal, configure: static node => node.WithProperty(WaitSignalActivity.SignalNameProperty, "conf-waits-go").WithProperty(WaitSignalActivity.AcceptAnyCorrelationProperty, true))
            .AddDelay("wait", TimeSpan.FromHours(1))
            .AddJoin("join")
            .AddEnd()
            .AddTransition("start", "fork").AddTransition("fork", "approve").AddTransition("fork", "signal").AddTransition("fork", "wait")
            .AddTransition("approve", "join").AddTransition("signal", "join").AddTransition("wait", "join").AddTransition("join", "end")
            .Build(), "t1").ConfigureAwait(false);
        var instance = (await host.StartAsync("conf-waits", tenantId: "t1").ConfigureAwait(false)).ValueOrThrow();

        async ValueTask<Result<IReadOnlyList<WorkflowBookmarkSummary>>> ListAsync(string? tenantId, string instanceId)
            => await host.AsTenantAsync(tenantId, sp => sp.GetRequiredService<IWorkflowEngine>().BookmarksAsync(instanceId, CancellationToken.None)).ConfigureAwait(false);

        var waits = (await ListAsync("t1", instance.Id).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal([BookmarkKinds.NodeTimeout, BookmarkKinds.Signal, BookmarkKinds.Timer, BookmarkKinds.UserTask, BookmarkKinds.UserTask], waits.Select(static wait => wait.Kind).Order(StringComparer.Ordinal));
        Assert.All(waits, wait => Assert.Equal(instance.Id, wait.InstanceId));
        Assert.True(waits.Zip(waits.Skip(1)).All(static pair => pair.First.CreatedAt <= pair.Second.CreatedAt));
        Assert.Equal(["alice", "bob"], waits.Where(static wait => wait.Kind == BookmarkKinds.UserTask).Select(static wait => wait.Key).Order(StringComparer.Ordinal));
        Assert.Equal(("signal", "conf-waits-go"), waits.Where(static wait => wait.Kind == BookmarkKinds.Signal).Select(static wait => (wait.NodeId, wait.Key)).Single());
        Assert.All(waits.Where(static wait => wait.Kind is BookmarkKinds.Timer or BookmarkKinds.NodeTimeout), static wait => Assert.NotNull(wait.DueAt));
        // 用户任务的摘要 ID 就是任务 ID
        var tasks = await host.AsTenantAsync("t1", async sp => (await sp.GetRequiredService<IWorkflowTaskService>().PendingByInstanceAsync(instance.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow()).ConfigureAwait(false);
        Assert.Equal(tasks.Select(static task => task.Id).Order(StringComparer.Ordinal), waits.Where(static wait => wait.Kind == BookmarkKinds.UserTask).Select(static wait => wait.Id).Order(StringComparer.Ordinal));

        // 别的分区与不存在的实例都按不存在处理
        foreach (var outsider in new[] { "t2", null })
        {
            Assert.Equal(WorkflowErrors.InstanceNotFound, (await ListAsync(outsider, instance.Id).ConfigureAwait(false)).Error?.Code);
        }

        Assert.Equal(WorkflowErrors.InstanceNotFound, (await ListAsync("t1", "conf-missing").ConfigureAwait(false)).Error?.Code);

        // 消费之后随之消失：Any 策略一人办理，用户任务与节点超时撤下；再发信号，只剩延时
        var task = tasks.Single(static task => task.AssigneeId == "alice");
        await host.AsTenantAsync("t1", async sp => (await sp.GetRequiredService<IWorkflowTaskService>().CompleteAsync(new UserTaskCompletion(task.Id, "alice", UserTaskOutcomes.Approved, null, null), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow()).ConfigureAwait(false);
        Assert.Equal([BookmarkKinds.Signal, BookmarkKinds.Timer], (await ListAsync("t1", instance.Id).ConfigureAwait(false)).ValueOrThrow().Select(static wait => wait.Kind).Order(StringComparer.Ordinal));
        Assert.Equal(1, (await host.AsTenantAsync("t1", sp => sp.GetRequiredService<IWorkflowEngine>().SignalAsync("conf-waits-go", null, null, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal([BookmarkKinds.Timer], (await ListAsync("t1", instance.Id).ConfigureAwait(false)).ValueOrThrow().Select(static wait => wait.Kind));
    }

    /// <summary>
    /// 用例 36：目录与实例查询的排序经引擎下推，排序字段越界是校验错误。
    /// </summary>
    /// <remarks>
    /// 排序字段不在白名单里（请求里给了越界的数字）以 <see cref="WorkflowErrors.InputInvalid"/> 返回，消息键 <c>workflow.definition.query_sort_invalid</c> / <c>workflow.instance.query_sort_invalid</c>，不在构造时抛出；实例计数不看排序。定义按标题升降序、按创建时间区间过滤；实例的排序方向缺省降序，按标题升降序（ADR-0102）。
    /// </remarks>
    [Fact]
    public async Task Queries_SortThroughTheEngine_AndRejectUnknownSortFields()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var bravo = await host.PublishAsync(Labeled("conf-sort-a", "a") with { DisplayName = "bravo" }).ConfigureAwait(false);
        var alpha = await host.PublishAsync(Labeled("conf-sort-b", "a") with { DisplayName = "alpha" }).ConfigureAwait(false);

        await host.AsTenantAsync(null, async sp =>
        {
            var catalog = sp.GetRequiredService<IWorkflowDefinitionCatalog>();
            var rejected = await catalog.QueryAsync(new WorkflowDefinitionQuery { SortBy = (WorkflowDefinitionSortField)99 }, CancellationToken.None).ConfigureAwait(false);
            Assert.Equal((WorkflowErrors.InputInvalid, WorkflowMessages.DefinitionQuerySortInvalid), (rejected.Error?.Code, rejected.Error?.MessageKey));

            var byTitle = new WorkflowDefinitionQuery { Keyword = "conf-sort", SortBy = WorkflowDefinitionSortField.Title };
            Assert.Equal([alpha.Id, bravo.Id], (await catalog.QueryAsync(byTitle, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Items.Select(static summary => summary.Id));
            Assert.Equal([bravo.Id, alpha.Id], (await catalog.QueryAsync(byTitle with { Descending = true }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Items.Select(static summary => summary.Id));
            Assert.Equal([bravo.Id, alpha.Id], (await catalog.QueryAsync(new WorkflowDefinitionQuery { Keyword = "conf-sort" }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Items.Select(static summary => summary.Id));
            var future = (await catalog.QueryAsync(new WorkflowDefinitionQuery { Keyword = "conf-sort", CreatedFrom = DateTimeOffset.UtcNow.AddHours(1) }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            Assert.Equal(0, future.Total);
            Assert.Equal(2, (await catalog.QueryAsync(new WorkflowDefinitionQuery { Keyword = "conf-sort", CreatedFrom = DateTimeOffset.UtcNow.AddHours(-1), CreatedTo = DateTimeOffset.UtcNow.AddHours(1) }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Total);
            return true;
        }).ConfigureAwait(false);

        async Task<WorkflowInstance> StartTitledAsync(string displayName)
            => (await host.EngineAsync(engine => engine.StartAsync(new WorkflowStartRequest { Name = "conf-sort-a", DisplayName = displayName }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow();

        var charlie = await StartTitledAsync("charlie").ConfigureAwait(false);
        var first = await StartTitledAsync("alpha").ConfigureAwait(false);
        var middle = await StartTitledAsync("bravo").ConfigureAwait(false);
        var invalid = new WorkflowInstanceQuery { SortBy = (WorkflowInstanceSortField)99 };
        var listed = await host.EngineAsync(engine => engine.ListInstancesAsync(invalid, CancellationToken.None)).ConfigureAwait(false);
        Assert.Equal((WorkflowErrors.InputInvalid, WorkflowMessages.InstanceQuerySortInvalid), (listed.Error?.Code, listed.Error?.MessageKey));
        Assert.Equal(3, (await host.EngineAsync(engine => engine.CountInstancesAsync(invalid, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow());

        // 排序方向缺省降序（实例列表最新的在前）；给了升序才从小到大
        var byTitleQuery = new WorkflowInstanceQuery { DefinitionName = new WorkflowName("conf-sort-a"), SortBy = WorkflowInstanceSortField.Title };
        Assert.Equal([charlie.Id, middle.Id, first.Id], (await host.ListInstancesAsync(byTitleQuery).ConfigureAwait(false)).Select(static instance => instance.Id));
        Assert.Equal([first.Id, middle.Id, charlie.Id], (await host.ListInstancesAsync(byTitleQuery with { Descending = false }).ConfigureAwait(false)).Select(static instance => instance.Id));
        Assert.Equal([middle.Id, first.Id, charlie.Id], (await host.ListInstancesAsync(new WorkflowInstanceQuery { DefinitionName = new WorkflowName("conf-sort-a") }).ConfigureAwait(false)).Select(static instance => instance.Id));
    }

    /// <summary>
    /// 用例 37：按状态排序是生命周期的先后，经目录与引擎改了状态之后位置随之变化。
    /// </summary>
    /// <remarks>
    /// 同 4.x 按状态枚举的整数值排（ADR-0103）：定义草稿、已发布、已停用、已归档；实例运行中、已挂起、已完成、已取消、故障、已终止。定义经目录发布、停用、归档，实例经引擎挂起、取消、终止、恢复；升序是生命周期的顺序，降序反过来，同状态按次序键。名字的字母序、状态名的字母序都与生命周期不同，排错了用例就失败。
    /// </remarks>
    [Fact]
    public async Task SortByStatus_FollowsTheLifecycle_AsTheCatalogAndEngineChangeStates()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);

        // 定义：名字 a、b、c、d 分别停在已归档、已停用、草稿、已发布——名字序、状态名序（Archived、Disabled、Draft、Published）都是 a b c d，生命周期是 c d b a
        var (draft, published, disabled, archived) = await host.AsTenantAsync(null, async sp =>
        {
            var catalog = sp.GetRequiredService<IWorkflowDefinitionCatalog>();
            async Task<string> CreateAsync(string name) => (await catalog.CreateAsync(Waiting(name), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Id;
            var a = await CreateAsync("conf-life-a").ConfigureAwait(false);
            var b = await CreateAsync("conf-life-b").ConfigureAwait(false);
            var c = await CreateAsync("conf-life-c").ConfigureAwait(false);
            var d = await CreateAsync("conf-life-d").ConfigureAwait(false);
            Assert.All(await catalog.PublishAsync([a, b, d], CancellationToken.None).ConfigureAwait(false), static published => Assert.True(published.IsSuccess));

            (await catalog.DisableAsync(b, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            (await catalog.ArchiveAsync(a, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            return (c, d, b, a);
        }).ConfigureAwait(false);

        async Task<List<string>> DefinitionsByStatusAsync(bool descending)
            => await host.AsTenantAsync(null, async sp => (await sp.GetRequiredService<IWorkflowDefinitionCatalog>().QueryAsync(new WorkflowDefinitionQuery { Keyword = "conf-life-", SortBy = WorkflowDefinitionSortField.Status, Descending = descending }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Items.Select(static summary => summary.Id).ToList()).ConfigureAwait(false);

        Assert.Equal([draft, published, disabled, archived], await DefinitionsByStatusAsync(false).ConfigureAwait(false));
        Assert.Equal([archived, disabled, published, draft], await DefinitionsByStatusAsync(true).ConfigureAwait(false));

        // 草稿发布之后与 d 同在已发布，同状态按名字升序
        await host.AsTenantAsync(null, async sp => (await sp.GetRequiredService<IWorkflowDefinitionCatalog>().PublishAsync(draft, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow()).ConfigureAwait(false);
        Assert.Equal([draft, published, disabled, archived], await DefinitionsByStatusAsync(false).ConfigureAwait(false));
        Assert.Equal([archived, disabled, draft, published], await DefinitionsByStatusAsync(true).ConfigureAwait(false));

        // 实例：四个停在延时上的实例，挂起、取消、终止各一个
        await host.PublishAsync(Waiting("conf-life-run")).ConfigureAwait(false);
        async Task<string> StartWaitingAsync() => (await host.StartAsync("conf-life-run").ConfigureAwait(false)).ValueOrThrow().Id;
        var running = await StartWaitingAsync().ConfigureAwait(false);
        var suspended = await StartWaitingAsync().ConfigureAwait(false);
        var terminated = await StartWaitingAsync().ConfigureAwait(false);
        var canceled = await StartWaitingAsync().ConfigureAwait(false);
        Assert.Equal(WorkflowInstanceStatus.Suspended, (await host.EngineAsync(engine => engine.SuspendAsync(suspended, "hold", CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow().Status);
        Assert.Equal(WorkflowInstanceStatus.Terminated, (await host.EngineAsync(engine => engine.TerminateAsync(terminated, "stop", CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow().Status);
        Assert.Equal(WorkflowInstanceStatus.Canceled, (await host.EngineAsync(engine => engine.CancelAsync(canceled, "drop", CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow().Status);

        async Task<List<string>> InstancesByStatusAsync(bool descending)
            => (await host.ListInstancesAsync(new WorkflowInstanceQuery { DefinitionName = new WorkflowName("conf-life-run"), SortBy = WorkflowInstanceSortField.Status, Descending = descending }).ConfigureAwait(false)).Select(static instance => instance.Id).ToList();

        // 运行中 < 已挂起 < 已取消 < 已终止（状态名的字母序是 Canceled、Running、Suspended、Terminated）
        Assert.Equal([running, suspended, canceled, terminated], await InstancesByStatusAsync(false).ConfigureAwait(false));
        Assert.Equal([terminated, canceled, suspended, running], await InstancesByStatusAsync(true).ConfigureAwait(false));

        // 恢复之后回到运行中，同状态按创建时间降序（次序键的方向固定）
        Assert.Equal(WorkflowInstanceStatus.Running, (await host.EngineAsync(engine => engine.ResumeInstanceAsync(suspended, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow().Status);
        Assert.Equal([suspended, running, canceled, terminated], await InstancesByStatusAsync(false).ConfigureAwait(false));
        Assert.Equal([terminated, canceled, suspended, running], await InstancesByStatusAsync(true).ConfigureAwait(false));
    }

    /// <summary>
    /// 带标签记录节点的定义：开始 → 记录 → 结束。
    /// </summary>
    private static WorkflowDefinition Labeled(string name, string label) => WorkflowDefinitionBuilder.Create(name)
        .AddStart().AddNode("a", RecordActivity.TypeName, configure: node => node.WithProperty(RecordActivity.LabelProperty, label)).AddEnd()
        .AddTransition("start", "a").AddTransition("a", "end")
        .Build();

    /// <summary>
    /// 停在一小时延时上的定义：开始 → 等待 → 结束。
    /// </summary>
    private static WorkflowDefinition Waiting(string name) => WorkflowDefinitionBuilder.Create(name)
        .AddStart().AddDelay("wait", TimeSpan.FromHours(1)).AddEnd()
        .AddTransition("start", "wait").AddTransition("wait", "end")
        .Build();

    /// <summary>
    /// 在给定作用域里建草稿并发布。
    /// </summary>
    private static async ValueTask<WorkflowDefinition> PublishAsync(IServiceProvider services, WorkflowDefinition definition)
    {
        var catalog = services.GetRequiredService<IWorkflowDefinitionCatalog>();
        var created = (await catalog.CreateAsync(definition, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        return (await catalog.PublishAsync(created.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
    }

    /// <summary>
    /// 在给定作用域里按名字启动最新已发布版本。
    /// </summary>
    private static ValueTask<Result<WorkflowInstance>> StartByNameAsync(IServiceProvider services, string name)
        => services.GetRequiredService<IWorkflowEngine>().StartAsync(new WorkflowStartRequest { Name = name }, CancellationToken.None);

    /// <summary>
    /// 以给定租户（空为平台）在一次经管道的调用里执行。
    /// </summary>
    private static ValueTask<T> AsTenantAsync<T>(WorkflowEngineHost host, string? tenantId, Func<IServiceProvider, ValueTask<T>> action)
        => host.AsTenantAsync(tenantId, action);

    /// <summary>
    /// 带实例级超时的定义：开始 → 记录 → 等一小时。
    /// </summary>
    private static WorkflowDefinition TimingOut(string name, WorkflowTimeoutAction action) => WorkflowDefinitionBuilder.Create(name)
        .WithTimeout(TimeSpan.FromMilliseconds(100), action)
        .AddStart().AddNode("a", RecordActivity.TypeName).AddDelay("wait", TimeSpan.FromHours(1)).AddEnd()
        .AddTransition("start", "a").AddTransition("a", "wait").AddTransition("wait", "end")
        .Build();

    /// <summary>
    /// 套件用的 HTTP 处理器：记录请求，<c>/orders/*</c> 返回 JSON，<c>/fail</c> 返回 500。
    /// </summary>
    private sealed class StubHttpHandler : HttpMessageHandler
    {
        /// <summary>
        /// 收到的请求：方法、URL、正文、<c>X-Trace</c> 头。
        /// </summary>
        public List<(string Method, string Url, string Body, string? Trace)> Requests { get; } = [];

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Requests.Add((request.Method.Method, request.RequestUri!.ToString(), body, request.Headers.TryGetValues("X-Trace", out var trace) ? string.Join(",", trace) : null));
            if (request.RequestUri.AbsolutePath.EndsWith("/fail", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError) { Content = new StringContent("boom") };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{\"total\": 21, \"ok\": true}", System.Text.Encoding.UTF8, "application/json") };
        }
    }

    /// <summary>
    /// 登记套件的记录器、活动与处理器。
    /// </summary>
    protected static void Register(IServiceCollection services, ConformanceLog log)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(log);
        services.AddSingleton(log);
        // 记录器登记成代理而不是发布者（ADR-0096 第六节）：Provider 以 TryAdd 登记代理与直接发布者，先登记的记录器不被顶替；
        // 发件箱以 Replace 换掉发布者也不影响——事件随事务写进发件箱，提交后由投递器送到这里。没装消息 Provider 的宿主由这里补上直接发布者
        services.AddSingleton<IMessageBroker>(log);
        services.TryAddScoped<IMessagePublisher, DirectPublisher>();
        // 轮询器从租户目录取租户分区：宿主没有装配多租户时用套件的目录
        services.TryAddSingleton<ITenantStore, ConformanceTenants>();
        services.AddActivity<RecordActivity>(RecordActivity.TypeName);
        services.AddWorkflowOutcomeHandler<RecordingOutcomeHandler>();
    }

    /// <summary>
    /// 一个"开始 → 用户任务 → 决策 → 结束（approved / rejected）"的定义。
    /// </summary>
    protected static WorkflowDefinition Approval(string name, UserTaskCompletionPolicy policy, params IReadOnlyList<string> assignees)
        => WorkflowDefinitionBuilder.Create(name)
            .AddVariable("amount", defaultValue: 7)
            .AddStart()
            .AddUserTask("approve", "Approve", node => node
                .WithAssigneeRule(AssigneeRuleTypes.User, assignees)
                .WithProperty(UserTaskActivity.CompletionPolicyProperty, policy.ToString())
                .WithProperty(UserTaskActivity.TitleProperty, "Approve {{ amount }}"))
            .AddDecision("check")
            .AddEnd("approved", outcome: UserTaskOutcomes.Approved).AddEnd("rejected", outcome: UserTaskOutcomes.Rejected)
            .AddTransition("start", "approve").AddTransition("approve", "check")
            .AddTransition("check", "approved", "outcome == 'approved'").AddDefaultTransition("check", "rejected")
            .Build();

    /// <summary>
    /// 轮询直到条件成立或超时。
    /// </summary>
    private async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow + WaitTimeout;
        while (!await condition().ConfigureAwait(false))
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The workflow did not reach the expected state in time.");
            }

            await Task.Delay(25).ConfigureAwait(false);
        }
    }
}
