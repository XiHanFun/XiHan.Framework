// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Assignees;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Abstractions.Tasks;
using Xunit;

namespace XiHan.Framework.Workflow.Conformance.Engine;

/// <summary>
/// 引擎的一致性套件（RFC-0021）：顺序流、决策、并行与汇合、用户任务的三种完成策略、转办与加签、延时与超时、信号、子流程与 ForEach、重试与继续、取消 / 终止 / 挂起 / 恢复、定义生命周期、事件主题、事件网关、HTTP、补偿、输出映射、实例级超时、延迟启动与重用策略、信号与定时启动、改变量 / 跳转 / 迁移、清理与延时变体。
/// Provider 继承并实现 <see cref="StartAsync"/>：装配被测特性，并把套件的记录器、活动与处理器登记进容器。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class WorkflowEngineConformance
{
    /// <summary>
    /// 启动装配了被测 Provider 的宿主；<paramref name="configure"/> 登记套件的记录器与活动，必须在宿主构建前应用；<paramref name="configuration"/> 是额外配置。
    /// </summary>
    protected abstract Task<WorkflowEngineHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null);

    /// <summary>
    /// 延时与超时用例等待的上限。
    /// </summary>
    protected virtual TimeSpan WaitTimeout => TimeSpan.FromSeconds(10);

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
            .AddNode("b", ActivityTypes.SetVariable, configure: node => node.WithProperty(SetVariableActivity.ExpressionsProperty, new JsonObject { ["z"] = "x + y", ["greeting"] = "'hi ' + upper(name)" }))
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
        Assert.NotNull(instance.EndedAt);
        Assert.Equal(("alice", "doc-1"), (instance.StarterId, instance.CorrelationId));

        var nodes = await host.NodeInstancesAsync(started.Id).ConfigureAwait(false);
        Assert.Equal(["start", "a", "b", "end"], nodes.Select(static node => node.NodeId));
        Assert.All(nodes, node => Assert.Equal(WorkflowNodeInstanceStatus.Completed, node.Status));
        Assert.Equal([(started.Id, "a", 1)], log.Executed);
        Assert.Empty(await host.BookmarksAsync(started.Id).ConfigureAwait(false));

        var topics = log.Events.Where(pending => pending.Data.InstanceId == started.Id).Select(static pending => pending.Topic).ToList();
        Assert.Equal(WorkflowTopics.InstanceStarted, topics[0]);
        Assert.Equal(WorkflowTopics.InstanceCompleted, topics[^1]);
        Assert.Equal(4, topics.Count(static topic => topic == WorkflowTopics.NodeExecuting));
        Assert.Equal(4, topics.Count(static topic => topic == WorkflowTopics.NodeExecuted));
    }

    /// <summary>
    /// 用例 2：决策按条件选一条出边，都不匹配走默认出边；条件里能用结果标签。
    /// </summary>
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
    /// 用例 3：并行网关让两个分支都执行；汇合等全部到达只执行一次；有分支死掉的汇合让实例故障。
    /// </summary>
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
    /// 用例 4：用户任务（Any）：待办按指派人查到；非指派人完成被拒；结果不在允许范围被拒；通过后继续；事件记录任务创建与完成。
    /// </summary>
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
    /// 用例 5：会签（All）要全部通过，拒绝立即结束；顺序（Sequential）一次一个。
    /// </summary>
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
    /// 用例 6：转办把任务交给目标（目标已是指派人被拒），加签立即给新人任务（已有的被拒）。
    /// </summary>
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
    /// 用例 7：延时以定时书签挂起，到期轮询后完成；用户任务超时以 <c>timeout</c> 结果完成。
    /// </summary>
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
    /// 用例 8：信号按名字与关联消费书签，载荷成为输出；关联不匹配的不消费。
    /// </summary>
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
    /// 用例 9：子流程等待回调并取回子实例变量；ForEach 顺序逐项启动子流程并汇总结果。
    /// </summary>
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

        var children = await Services(host).ListAsync(new WorkflowInstanceQuery { ParentInstanceId = instance.Id }, CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(4, children.ValueOrThrow().Count);
        Assert.All(children.ValueOrThrow(), child => Assert.Equal((WorkflowInstanceStatus.Completed, "doc-9", 1), (child.Status, child.CorrelationId, child.Depth)));
    }

    /// <summary>
    /// 用例 10：重试策略按退避重试（到期轮询）；用尽后故障；<c>RetryAsync</c> 从故障节点再来；继续出错以 <c>error</c> 流出并记 <c>lastError</c>。
    /// </summary>
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
    /// 用例 11：挂起的实例不接收到期书签（轮询后推），恢复后接收；取消清掉书签并补偿，终止不补偿；已结束的幂等；状态不允许的报冲突。
    /// </summary>
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
    /// 用例 12：定义生命周期：不合法的草稿发布失败；发布后不能改；新版本是草稿；停用后不能启动；删除只允许草稿；结束节点带结果时结果处理器失败即故障。
    /// </summary>
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
    /// 用例 14：租户隔离：带租户启动的实例，没有租户的调用方查不到它的任务、发不到它的信号。
    /// </summary>
    [Fact]
    public async Task Tenant_IsolatesTasksAndSignals()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await host.PublishAsync(Approval("conf-tenant", UserTaskCompletionPolicy.Any, "alice")).ConfigureAwait(false);
        var tenantInstance = (await host.StartAsync("conf-tenant", tenantId: "t1").ConfigureAwait(false)).ValueOrThrow();
        var plainInstance = (await host.StartAsync("conf-tenant").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal("t1", tenantInstance.TenantId);
        Assert.Equal("t1", Assert.Single(await host.BookmarksAsync(tenantInstance.Id).ConfigureAwait(false)).TenantId);

        var pending = await host.PendingAsync("alice").ConfigureAwait(false);
        Assert.Equal([plainInstance.Id], pending.Select(static task => task.InstanceId));
        var tenantTask = Assert.Single(await host.BookmarksAsync(tenantInstance.Id).ConfigureAwait(false));
        Assert.Equal(WorkflowErrors.BookmarkNotFound, (await host.CompleteAsync(tenantTask.Id, "alice", UserTaskOutcomes.Approved).ConfigureAwait(false)).Error?.Code);
    }

    /// <summary>
    /// 用例 15：事件网关：先到的信号决定出路并带出载荷，其余书签清掉；没有信号时按超时流出。
    /// </summary>
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
    /// 用例 16：HTTP 活动：模板 URL 与请求体、响应解析为对象、输出映射取值；非 2xx 默认故障，关掉后以 error 标签继续。
    /// </summary>
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
    /// 用例 17：补偿活动逆序补偿指定的已完成节点后继续；故障补偿在实例故障时补偿全部已完成节点。
    /// </summary>
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
    /// 用例 18：输出映射以节点输出为作用域求值写入变量；表达式不合法在发布时拒绝，求值失败即节点故障。
    /// </summary>
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
    /// 用例 20：延迟启动在到期前只有开始节点的定时书签；重用策略按关联 ID 拒绝或返回已有实例。
    /// </summary>
    [Fact]
    public async Task DelayedStart_AndReusePolicy()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        var definition = WorkflowDefinitionBuilder.Create("conf-start")
            .AddStart().AddNode("a", RecordActivity.TypeName).AddDelay("wait", TimeSpan.FromHours(1)).AddEnd()
            .AddTransition("start", "a").AddTransition("a", "wait").AddTransition("wait", "end")
            .Build();
        await host.PublishAsync(definition).ConfigureAwait(false);
        var delayed = (await host.EngineAsync(engine => engine.StartAsync(new WorkflowStartRequest { Name = "conf-start", StartAt = DateTimeOffset.UtcNow.AddMilliseconds(100) }, CancellationToken.None)).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Running, delayed.Status);
        var startBookmark = Assert.Single(await host.BookmarksAsync(delayed.Id).ConfigureAwait(false));
        Assert.Equal((BookmarkKinds.Timer, "start"), (startBookmark.Kind, startBookmark.NodeId));
        Assert.DoesNotContain(log.Executed, entry => entry.InstanceId == delayed.Id);
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
    /// 用例 21：信号启动为声明了该信号的已发布定义启动实例；定时启动在发布时排定义级书签，到期由轮询启动并再次排期，停用后不再排。
    /// </summary>
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
    /// 用例 22：改变量、跳转到指定节点、迁移到兼容的新版本；不兼容的版本与不存在的节点被拒绝。
    /// </summary>
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
    /// 用例 23：清理删除早于时刻结束的实例及其节点与书签，运行中的不动；延时节点支持到时刻与 cron。
    /// </summary>
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
        var store = Services(host);
        Assert.All(ended, async id => Assert.False((await store.FindAsync(id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue));
        Assert.True((await store.FindAsync(running.Id, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
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
        services.AddSingleton<IMessagePublisher>(log);
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
    /// 实例存储。
    /// </summary>
    private static IWorkflowInstanceStore Services(WorkflowEngineHost host) => host.Services.GetRequiredService<IWorkflowInstanceStore>();

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
