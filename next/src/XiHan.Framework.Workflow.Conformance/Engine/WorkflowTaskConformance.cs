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
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.ExternalTasks;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Tasks;
using Xunit;

namespace XiHan.Framework.Workflow.Conformance.Engine;

/// <summary>
/// 用户任务增强与外部任务的一致性套件（RFC-0021）。
/// </summary>
/// <remarks>
/// 会签阈值、认领与释放、退回、催办、条件查询、任务元数据与周期提醒、外部任务的领取 / 完成 / 失败 / 锁。Provider 继承并实现 <see cref="StartAsync"/>：装配被测特性，并把套件的记录器、活动与处理器登记进容器。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class WorkflowTaskConformance
{
    /// <summary>
    /// 外部任务用例的主题。
    /// </summary>
    private const string PaymentTopic = "conf.pay";

    /// <summary>
    /// 启动装配了被测 Provider 的宿主。
    /// </summary>
    /// <remarks>
    /// <paramref name="configure"/> 登记套件的记录器与活动，必须在宿主构建前应用；<paramref name="configuration"/> 是额外配置。
    /// </remarks>
    protected abstract Task<WorkflowEngineHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null);

    /// <summary>
    /// 用例 1：会签阈值。
    /// </summary>
    /// <remarks>
    /// 人数阈值达到即通过；百分比按指派人数向上取整；剩余人数到不了阈值即拒绝；缺省阈值为全部，一人拒绝立即拒绝。
    /// </remarks>
    [Fact]
    public async Task Threshold_All_CompletesAtCountOrPercent()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await host.PublishAsync(Approval("conf-threshold-count", UserTaskCompletionPolicy.All, ["alice", "bob", "carol"], node => node.WithProperty(UserTaskActivity.ApprovalThresholdProperty, 2))).ConfigureAwait(false);
        await host.PublishAsync(Approval("conf-threshold-percent", UserTaskCompletionPolicy.All, ["alice", "bob", "carol"], node => node.WithProperty(UserTaskActivity.ApprovalThresholdProperty, "50%"))).ConfigureAwait(false);
        await host.PublishAsync(Approval("conf-threshold-all", UserTaskCompletionPolicy.All, ["alice", "bob", "carol"])).ConfigureAwait(false);

        // 人数阈值 2：两人通过即通过，第三人的书签随节点一起清掉
        var count = (await host.StartAsync("conf-threshold-count").ConfigureAwait(false)).ValueOrThrow();
        var alice = await TaskOfAsync(host, "alice", count.Id).ConfigureAwait(false);
        Assert.Equal(WorkflowInstanceStatus.Running, (await host.CompleteAsync(alice.Id, "alice", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow().Status);
        var bob = await TaskOfAsync(host, "bob", count.Id).ConfigureAwait(false);
        var approved = (await host.CompleteAsync(bob.Id, "bob", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((WorkflowInstanceStatus.Completed, "approved"), (approved.Status, approved.Outcome));
        Assert.Empty((await host.PendingAsync("carol").ConfigureAwait(false)).Where(task => task.InstanceId == count.Id));
        Assert.Empty(await host.BookmarksAsync(count.Id).ConfigureAwait(false));

        // 百分比 50% 的三人 → 向上取整为 2
        var percent = (await host.StartAsync("conf-threshold-percent").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Running, (await host.CompleteAsync((await TaskOfAsync(host, "carol", percent.Id).ConfigureAwait(false)).Id, "carol", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow().Status);
        var percentDone = (await host.CompleteAsync((await TaskOfAsync(host, "alice", percent.Id).ConfigureAwait(false)).Id, "alice", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((WorkflowInstanceStatus.Completed, "approved"), (percentDone.Status, percentDone.Outcome));

        // 人数阈值 2：一人拒绝还有两人能通过，继续等；第二人拒绝后剩一人到不了 2，拒绝
        var mixed = (await host.StartAsync("conf-threshold-count").ConfigureAwait(false)).ValueOrThrow();
        var firstReject = (await host.CompleteAsync((await TaskOfAsync(host, "alice", mixed.Id).ConfigureAwait(false)).Id, "alice", UserTaskOutcomes.Rejected).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Running, firstReject.Status);
        Assert.Equal(2, (await host.BookmarksAsync(mixed.Id).ConfigureAwait(false)).Count);
        var secondReject = (await host.CompleteAsync((await TaskOfAsync(host, "bob", mixed.Id).ConfigureAwait(false)).Id, "bob", UserTaskOutcomes.Rejected).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((WorkflowInstanceStatus.Completed, "rejected"), (secondReject.Status, secondReject.Outcome));
        Assert.Empty((await host.PendingAsync("carol").ConfigureAwait(false)).Where(task => task.InstanceId == mixed.Id));

        // 缺省阈值 = 全部：一人拒绝立即拒绝
        var strict = (await host.StartAsync("conf-threshold-all").ConfigureAwait(false)).ValueOrThrow();
        var strictDone = (await host.CompleteAsync((await TaskOfAsync(host, "bob", strict.Id).ConfigureAwait(false)).Id, "bob", UserTaskOutcomes.Rejected).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((WorkflowInstanceStatus.Completed, "rejected"), (strictDone.Status, strictDone.Outcome));
        Assert.Empty((await host.PendingAsync("alice").ConfigureAwait(false)).Where(task => task.InstanceId == strict.Id));
        Assert.Equal(["approved", "approved", "rejected", "rejected"], log.Outcomes);

        // 阈值取值不合法：发布时被定义校验拒绝
        var invalid = await host.InScopeAsync(async sp =>
        {
            var catalog = sp.GetRequiredService<IWorkflowDefinitionCatalog>();
            var created = (await catalog.CreateAsync(Approval("conf-threshold-invalid", UserTaskCompletionPolicy.All, ["alice"], node => node.WithProperty(UserTaskActivity.ApprovalThresholdProperty, "150%")), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            return await catalog.PublishAsync(created.Id, CancellationToken.None).ConfigureAwait(false);
        }).ConfigureAwait(false);
        Assert.Equal(WorkflowErrors.DefinitionInvalid, invalid.Error?.Code);
    }

    /// <summary>
    /// 用例 2：认领（Any）撤下其他指派人的书签，其他人既不能完成也不能认领。
    /// </summary>
    /// <remarks>
    /// 认领人加签后新人认领被拒（已被认领）；非认领人释放被拒；释放后其他人重新拿到待办；Sequential 不支持认领；事件记录认领与释放。
    /// </remarks>
    [Fact]
    public async Task Claim_Release_AndGuards()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await host.PublishAsync(Approval("conf-claim", UserTaskCompletionPolicy.Any, ["alice", "bob"])).ConfigureAwait(false);
        await host.PublishAsync(Approval("conf-claim-seq", UserTaskCompletionPolicy.Sequential, ["alice", "bob"])).ConfigureAwait(false);

        var instance = (await host.StartAsync("conf-claim").ConfigureAwait(false)).ValueOrThrow();
        var alice = await TaskOfAsync(host, "alice", instance.Id).ConfigureAwait(false);
        var bob = await TaskOfAsync(host, "bob", instance.Id).ConfigureAwait(false);
        Assert.Null(alice.ClaimedBy);

        Assert.Equal(WorkflowErrors.TaskDenied, (await ClaimAsync(host, alice.Id, "carol").ConfigureAwait(false)).Error?.Code);
        Assert.True((await ClaimAsync(host, alice.Id, "alice").ConfigureAwait(false)).IsSuccess);
        Assert.Empty((await host.PendingAsync("bob").ConfigureAwait(false)).Where(task => task.InstanceId == instance.Id));
        var claimed = await TaskOfAsync(host, "alice", instance.Id).ConfigureAwait(false);
        Assert.Equal((alice.Id, "alice"), (claimed.Id, claimed.ClaimedBy));
        Assert.Equal(WorkflowErrors.BookmarkNotFound, (await host.CompleteAsync(bob.Id, "bob", UserTaskOutcomes.Approved).ConfigureAwait(false)).Error?.Code);
        Assert.Equal(WorkflowErrors.BookmarkNotFound, (await ClaimAsync(host, bob.Id, "bob").ConfigureAwait(false)).Error?.Code);

        // 本人重复认领幂等；认领人加签后新人拿到书签但认领被拒；非认领人释放被拒
        Assert.True((await ClaimAsync(host, alice.Id, "alice").ConfigureAwait(false)).IsSuccess);
        Assert.True((await host.AddAssigneesAsync(alice.Id, "alice", "dave").ConfigureAwait(false)).IsSuccess);
        var dave = await TaskOfAsync(host, "dave", instance.Id).ConfigureAwait(false);
        Assert.Equal(WorkflowErrors.AssigneeExists, (await ClaimAsync(host, dave.Id, "dave").ConfigureAwait(false)).Error?.Code);
        Assert.Equal(WorkflowErrors.TaskDenied, (await ReleaseAsync(host, dave.Id, "dave").ConfigureAwait(false)).Error?.Code);

        // 释放：bob 重新拿到待办（新书签），dave 的保留，alice 的不再标认领
        Assert.True((await ReleaseAsync(host, alice.Id, "alice").ConfigureAwait(false)).IsSuccess);
        var bobAgain = await TaskOfAsync(host, "bob", instance.Id).ConfigureAwait(false);
        Assert.NotEqual(bob.Id, bobAgain.Id);
        Assert.Null((await TaskOfAsync(host, "alice", instance.Id).ConfigureAwait(false)).ClaimedBy);
        Assert.Equal(dave.Id, (await TaskOfAsync(host, "dave", instance.Id).ConfigureAwait(false)).Id);
        Assert.Equal(3, (await host.BookmarksAsync(instance.Id).ConfigureAwait(false)).Count);
        Assert.Equal(WorkflowErrors.TaskDenied, (await ReleaseAsync(host, alice.Id, "alice").ConfigureAwait(false)).Error?.Code);

        // 再认领后由认领人完成：Any 首个通过即完成
        Assert.True((await ClaimAsync(host, bobAgain.Id, "bob").ConfigureAwait(false)).IsSuccess);
        var completed = (await host.CompleteAsync(bobAgain.Id, "bob", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal((WorkflowInstanceStatus.Completed, "approved"), (completed.Status, completed.Outcome));
        Assert.Empty(await host.BookmarksAsync(instance.Id).ConfigureAwait(false));

        var claimedEvents = log.EventsOf(WorkflowTopics.TaskClaimed).Where(data => data.InstanceId == instance.Id).ToList();
        Assert.Equal(["alice", "alice", "bob"], claimedEvents.Select(static data => data.ActorId));
        Assert.Equal(alice.Id, claimedEvents[0].TaskId);
        var released = Assert.Single(log.EventsOf(WorkflowTopics.TaskReleased));
        Assert.Equal(("alice", alice.Id), (released.ActorId, released.TaskId));

        // Sequential 不支持认领
        var sequential = (await host.StartAsync("conf-claim-seq").ConfigureAwait(false)).ValueOrThrow();
        var first = await TaskOfAsync(host, "alice", sequential.Id).ConfigureAwait(false);
        Assert.Equal(WorkflowErrors.InputInvalid, (await ClaimAsync(host, first.Id, "alice").ConfigureAwait(false)).Error?.Code);
    }

    /// <summary>
    /// 用例 3：退回。
    /// </summary>
    /// <remarks>
    /// 缺省目标是本节点之前最近完成的用户任务，以跳转实现（本节点实例取消、目标节点新建实例、上游指派人重新拿到待办），节点历史记 <c>returned</c>；指定的目标不存在被拒；没有上游用户任务时退回 <c>Start</c> 之后的首个节点。
    /// </remarks>
    [Fact]
    public async Task Return_JumpsBack_AndRecordsHistory()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await host.PublishAsync(WorkflowDefinitionBuilder.Create("conf-return")
            .AddStart()
            .AddUserTask("first", "First", node => node.WithAssigneeRule(AssigneeRuleTypes.User, "alice"))
            .AddUserTask("second", "Second", node => node.WithAssigneeRule(AssigneeRuleTypes.User, "bob"))
            .AddEnd()
            .AddTransition("start", "first").AddTransition("first", "second").AddTransition("second", "end")
            .Build()).ConfigureAwait(false);
        await host.PublishAsync(WorkflowDefinitionBuilder.Create("conf-return-first")
            .AddStart()
            .AddNode("a", RecordActivity.TypeName)
            .AddUserTask("task", "Task", node => node.WithAssigneeRule(AssigneeRuleTypes.User, "alice"))
            .AddEnd()
            .AddTransition("start", "a").AddTransition("a", "task").AddTransition("task", "end")
            .Build()).ConfigureAwait(false);

        var instance = (await host.StartAsync("conf-return").ConfigureAwait(false)).ValueOrThrow();
        var first = await TaskOfAsync(host, "alice", instance.Id).ConfigureAwait(false);
        Assert.Equal(WorkflowInstanceStatus.Running, (await host.CompleteAsync(first.Id, "alice", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow().Status);
        var second = await TaskOfAsync(host, "bob", instance.Id).ConfigureAwait(false);
        Assert.Empty((await host.PendingAsync("alice").ConfigureAwait(false)).Where(task => task.InstanceId == instance.Id));

        Assert.Equal(WorkflowErrors.TaskDenied, (await ReturnAsync(host, second.Id, "carol").ConfigureAwait(false)).Error?.Code);
        Assert.Equal(WorkflowErrors.InputInvalid, (await ReturnAsync(host, second.Id, "bob", "nope").ConfigureAwait(false)).Error?.Code);
        Assert.Single(await host.BookmarksAsync(instance.Id).ConfigureAwait(false));

        var returned = (await ReturnAsync(host, second.Id, "bob", comment: "fix the amount").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Running, returned.Status);
        var firstAgain = await TaskOfAsync(host, "alice", instance.Id).ConfigureAwait(false);
        Assert.Equal("first", firstAgain.NodeId);
        Assert.NotEqual(first.Id, firstAgain.Id);
        Assert.Empty((await host.PendingAsync("bob").ConfigureAwait(false)).Where(task => task.InstanceId == instance.Id));
        Assert.Equal(WorkflowErrors.BookmarkNotFound, (await host.CompleteAsync(second.Id, "bob", UserTaskOutcomes.Approved).ConfigureAwait(false)).Error?.Code);

        var nodes = await host.NodeInstancesAsync(instance.Id).ConfigureAwait(false);
        var canceled = Assert.Single(nodes, node => node.NodeId == "second");
        Assert.Equal(WorkflowNodeInstanceStatus.Canceled, canceled.Status);
        var history = Assert.IsType<JsonArray>(canceled.State[UserTaskPayloadKeys.History]);
        var entry = Assert.IsType<JsonObject>(Assert.Single(history));
        Assert.Equal(("bob", "returned", "fix the amount"), (entry[UserTaskPayloadKeys.ActorId]?.GetValue<string>(), entry[UserTaskPayloadKeys.Outcome]?.GetValue<string>(), entry[UserTaskPayloadKeys.Comment]?.GetValue<string>()));
        Assert.Equal([WorkflowNodeInstanceStatus.Completed, WorkflowNodeInstanceStatus.Suspended], nodes.Where(node => node.NodeId == "first").Select(static node => node.Status));

        var returnedEvent = Assert.Single(log.EventsOf(WorkflowTopics.TaskReturned));
        Assert.Equal(("bob", "first", "fix the amount", second.Id), (returnedEvent.ActorId, returnedEvent.NodeId, returnedEvent.Reason, returnedEvent.TaskId));
        Assert.Single(log.EventsOf(WorkflowTopics.InstanceJumped));

        // 退回后重新走完
        Assert.Equal(WorkflowInstanceStatus.Running, (await host.CompleteAsync(firstAgain.Id, "alice", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow().Status);
        var secondAgain = await TaskOfAsync(host, "bob", instance.Id).ConfigureAwait(false);
        Assert.Equal(WorkflowInstanceStatus.Completed, (await host.CompleteAsync(secondAgain.Id, "bob", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow().Status);

        // 没有上游用户任务：退回 Start 之后的首个节点，它再执行一次
        var fromStart = (await host.StartAsync("conf-return-first").ConfigureAwait(false)).ValueOrThrow();
        var task = await TaskOfAsync(host, "alice", fromStart.Id).ConfigureAwait(false);
        Assert.Equal(WorkflowInstanceStatus.Running, (await ReturnAsync(host, task.Id, "alice").ConfigureAwait(false)).ValueOrThrow().Status);
        Assert.Equal(2, log.Executed.Count(executed => executed.InstanceId == fromStart.Id && executed.NodeId == "a"));
        Assert.NotEqual(task.Id, (await TaskOfAsync(host, "alice", fromStart.Id).ConfigureAwait(false)).Id);
        Assert.Equal("a", log.EventsOf(WorkflowTopics.TaskReturned).Single(data => data.InstanceId == fromStart.Id).NodeId);
    }

    /// <summary>
    /// 用例 4：催办只发事件、不改状态。
    /// </summary>
    /// <remarks>
    /// 催办人不必是指派人；任务不存在被拒。
    /// </remarks>
    [Fact]
    public async Task Urge_PublishesEvent()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await host.PublishAsync(Approval("conf-urge", UserTaskCompletionPolicy.Any, ["alice"])).ConfigureAwait(false);
        var instance = (await host.StartAsync("conf-urge", starterId: "starter").ConfigureAwait(false)).ValueOrThrow();
        var task = await TaskOfAsync(host, "alice", instance.Id).ConfigureAwait(false);

        Assert.True((await UrgeAsync(host, task.Id, "starter", "please hurry").ConfigureAwait(false)).IsSuccess);
        Assert.Equal(WorkflowErrors.BookmarkNotFound, (await UrgeAsync(host, "missing", "starter").ConfigureAwait(false)).Error?.Code);
        var urged = Assert.Single(log.EventsOf(WorkflowTopics.TaskUrged));
        Assert.Equal(("starter", "please hurry", "alice", task.Id, instance.Id), (urged.ActorId, urged.Reason, urged.AssigneeId, urged.TaskId, urged.InstanceId));

        Assert.Equal(task.Id, (await TaskOfAsync(host, "alice", instance.Id).ConfigureAwait(false)).Id);
        Assert.Equal(WorkflowInstanceStatus.Running, (await host.InstanceAsync(instance.Id).ConfigureAwait(false)).Status);
        Assert.Equal(WorkflowInstanceStatus.Completed, (await host.CompleteAsync(task.Id, "alice", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow().Status);
        Assert.Equal(WorkflowErrors.BookmarkNotFound, (await UrgeAsync(host, task.Id, "starter").ConfigureAwait(false)).Error?.Code);
    }

    /// <summary>
    /// 用例 5：条件查询。
    /// </summary>
    /// <remarks>
    /// 指派人与实例必填其一；按实例、节点、定义名、创建时间过滤；按创建时间升序分页；只含实例运行中的任务。
    /// </remarks>
    [Fact]
    public async Task Query_FiltersAndPages()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await host.PublishAsync(Approval("conf-query", UserTaskCompletionPolicy.All, ["alice", "bob"])).ConfigureAwait(false);
        var first = (await host.StartAsync("conf-query").ConfigureAwait(false)).ValueOrThrow();
        var second = (await host.StartAsync("conf-query").ConfigureAwait(false)).ValueOrThrow();
        var third = (await host.StartAsync("conf-query").ConfigureAwait(false)).ValueOrThrow();

        Assert.Equal(WorkflowErrors.InputInvalid, (await QueryAsync(host, new UserTaskQuery()).ConfigureAwait(false)).Error?.Code);

        var all = (await QueryAsync(host, new UserTaskQuery { AssigneeId = "alice" }).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal([first.Id, second.Id, third.Id], all.Select(static task => task.InstanceId));
        Assert.True(all.Zip(all.Skip(1)).All(static pair => pair.First.CreatedAt <= pair.Second.CreatedAt));
        var page = (await QueryAsync(host, new UserTaskQuery { AssigneeId = "alice", Skip = 1, Limit = 1 }).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(second.Id, Assert.Single(page).InstanceId);

        var byInstance = (await QueryAsync(host, new UserTaskQuery { InstanceId = second.Id }).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(["alice", "bob"], byInstance.Select(static task => task.AssigneeId).Order());
        Assert.Equal(2, (await QueryAsync(host, new UserTaskQuery { InstanceId = second.Id, NodeId = "approve" }).ConfigureAwait(false)).ValueOrThrow().Count);
        Assert.Empty((await QueryAsync(host, new UserTaskQuery { InstanceId = second.Id, NodeId = "other" }).ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal("alice", Assert.Single((await QueryAsync(host, new UserTaskQuery { AssigneeId = "alice", InstanceId = third.Id }).ConfigureAwait(false)).ValueOrThrow()).AssigneeId);
        Assert.Equal(3, (await QueryAsync(host, new UserTaskQuery { AssigneeId = "alice", DefinitionName = "conf-query" }).ConfigureAwait(false)).ValueOrThrow().Count);
        Assert.Empty((await QueryAsync(host, new UserTaskQuery { AssigneeId = "alice", DefinitionName = "conf-other" }).ConfigureAwait(false)).ValueOrThrow());
        Assert.Empty((await QueryAsync(host, new UserTaskQuery { AssigneeId = "alice", CreatedFrom = DateTimeOffset.UtcNow.AddHours(1) }).ConfigureAwait(false)).ValueOrThrow());
        Assert.Empty((await QueryAsync(host, new UserTaskQuery { AssigneeId = "alice", CreatedTo = DateTimeOffset.UtcNow.AddHours(-1) }).ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal(3, (await QueryAsync(host, new UserTaskQuery { AssigneeId = "alice", CreatedFrom = DateTimeOffset.UtcNow.AddHours(-1), CreatedTo = DateTimeOffset.UtcNow.AddHours(1) }).ConfigureAwait(false)).ValueOrThrow().Count);

        // 实例结束后它的任务不再出现
        Assert.Equal(WorkflowInstanceStatus.Running, (await host.CompleteAsync(all[0].Id, "alice", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow().Status);
        var bob = await TaskOfAsync(host, "bob", first.Id).ConfigureAwait(false);
        Assert.Equal(WorkflowInstanceStatus.Completed, (await host.CompleteAsync(bob.Id, "bob", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow().Status);
        Assert.Equal([second.Id, third.Id], (await QueryAsync(host, new UserTaskQuery { AssigneeId = "alice" }).ConfigureAwait(false)).ValueOrThrow().Select(static task => task.InstanceId));
        Assert.Empty((await QueryAsync(host, new UserTaskQuery { InstanceId = first.Id }).ConfigureAwait(false)).ValueOrThrow());
    }

    /// <summary>
    /// 用例 6：任务视图带优先级、截止时刻（相对秒数或固定时刻）与渲染后的表单键。
    /// </summary>
    /// <remarks>
    /// <c>ReminderAfterSeconds</c> 周期发布提醒事件，任务完成后不再提醒。
    /// </remarks>
    [Fact]
    public async Task TaskMetadata_PriorityDueFormKey_AndReminder()
    {
        var log = new ConformanceLog();
        await using var host = await StartAsync(services => Register(services, log)).ConfigureAwait(false);
        await host.PublishAsync(Approval("conf-meta", UserTaskCompletionPolicy.Any, ["alice"], node => node
            .WithProperty(UserTaskActivity.PriorityProperty, 5)
            .WithProperty(UserTaskActivity.DueAfterSecondsProperty, 3600)
            .WithProperty(UserTaskActivity.FormKeyProperty, "form-{{ amount }}")
            .WithProperty(UserTaskActivity.ReminderAfterSecondsProperty, JsonValue.Create(0.05)))).ConfigureAwait(false);
        await host.PublishAsync(Approval("conf-meta-due", UserTaskCompletionPolicy.Any, ["alice"], node => node
            .WithProperty(UserTaskActivity.DueAtProperty, "2030-01-02T03:04:05Z"))).ConfigureAwait(false);

        var before = DateTimeOffset.UtcNow;
        var instance = (await host.StartAsync("conf-meta").ConfigureAwait(false)).ValueOrThrow();
        var task = await TaskOfAsync(host, "alice", instance.Id).ConfigureAwait(false);
        Assert.Equal((5, "form-7"), (task.Priority, task.FormKey));
        Assert.NotNull(task.DueAt);
        Assert.InRange(task.DueAt.Value, before.AddMinutes(59), DateTimeOffset.UtcNow.AddMinutes(61));
        var bookmarks = await host.BookmarksAsync(instance.Id).ConfigureAwait(false);
        Assert.Contains(bookmarks, bookmark => bookmark.Kind == BookmarkKinds.Reminder);
        var created = Assert.Single(log.EventsOf(WorkflowTopics.TaskCreated).Where(data => data.InstanceId == instance.Id));
        Assert.Equal(5, created.Payload?[UserTaskPayloadKeys.Priority]?.GetValue<int>());

        var fixedDue = (await host.StartAsync("conf-meta-due").ConfigureAwait(false)).ValueOrThrow();
        var fixedTask = await TaskOfAsync(host, "alice", fixedDue.Id).ConfigureAwait(false);
        Assert.Equal(new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero), fixedTask.DueAt);
        Assert.Null(fixedTask.Priority);

        // 提醒：到期轮询各发一次，直到任务完成
        for (var round = 0; round < 2; round++)
        {
            await Task.Delay(80).ConfigureAwait(false);
            await host.PollAsync().ConfigureAwait(false);
        }

        var reminders = log.EventsOf(WorkflowTopics.TaskReminder).Where(data => data.InstanceId == instance.Id).ToList();
        Assert.True(reminders.Count >= 2, $"Expected at least two reminders but got {reminders.Count}.");
        Assert.All(reminders, reminder => Assert.Equal((task.Id, "alice"), (reminder.TaskId, reminder.AssigneeId)));

        Assert.Equal(WorkflowInstanceStatus.Completed, (await host.CompleteAsync(task.Id, "alice", UserTaskOutcomes.Approved).ConfigureAwait(false)).ValueOrThrow().Status);
        Assert.Empty(await host.BookmarksAsync(instance.Id).ConfigureAwait(false));
        var settled = log.EventsOf(WorkflowTopics.TaskReminder).Count(data => data.InstanceId == instance.Id);
        await Task.Delay(80).ConfigureAwait(false);
        await host.PollAsync().ConfigureAwait(false);
        Assert.Equal(settled, log.EventsOf(WorkflowTopics.TaskReminder).Count(data => data.InstanceId == instance.Id));
    }

    /// <summary>
    /// 用例 7：外部任务。
    /// </summary>
    /// <remarks>
    /// 领取上锁（锁未过期时别人领不到、完成与延长锁要求持锁）；完成合并输出；带 <c>RetryAfter</c> 的失败解锁后推、尝试次数加一；不带的失败使节点故障；锁过期由轮询解锁后别人可领；事件记录创建、完成与失败。
    /// </remarks>
    [Fact]
    public async Task ExternalTask_FetchCompleteFailAndLock()
    {
        var log = new ConformanceLog();
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        await using var host = await StartAsync(services =>
        {
            Register(services, log);
            services.AddSingleton<TimeProvider>(clock);
        }).ConfigureAwait(false);
        await host.PublishAsync(WorkflowDefinitionBuilder.Create("conf-external")
            .AddStart()
            .AddExternalTask("pay", PaymentTopic, new JsonObject { ["order"] = "{{ orderId }}", ["retries"] = 3 })
            .AddEnd()
            .AddTransition("start", "pay").AddTransition("pay", "end")
            .Build()).ConfigureAwait(false);

        // 领取与锁
        var instance = (await host.StartAsync("conf-external", new JsonObject { ["orderId"] = "o-1" }).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Single(log.EventsOf(WorkflowTopics.ExternalCreated).Where(data => data.InstanceId == instance.Id));
        Assert.Equal(WorkflowErrors.InputInvalid, (await FetchAsync(host, "w1", TimeSpan.Zero).ConfigureAwait(false)).Error?.Code);
        Assert.Equal(WorkflowErrors.InputInvalid, (await FetchAsync(host, "w1", TimeSpan.FromMinutes(1), 0).ConfigureAwait(false)).Error?.Code);
        var task = Assert.Single((await FetchAsync(host, "w1", TimeSpan.FromMinutes(1)).ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal((instance.Id, "pay", PaymentTopic, 1), (task.InstanceId, task.NodeId, task.Topic, task.Attempt));
        Assert.Equal(("o-1", 3, "o-1"), (task.Payload?["order"]?.GetValue<string>(), task.Payload?["retries"]?.GetValue<int>(), task.Variables?["orderId"]?.GetValue<string>()));
        Assert.True(task.LockedUntil > clock.GetUtcNow().AddSeconds(30));
        Assert.Empty((await FetchAsync(host, "w2", TimeSpan.FromMinutes(1)).ConfigureAwait(false)).ValueOrThrow());

        Assert.Equal(WorkflowErrors.ExternalTaskDenied, (await CompleteExternalAsync(host, task.Id, "w2").ConfigureAwait(false)).Error?.Code);
        Assert.Equal(WorkflowErrors.ExternalTaskDenied, (await ExtendLockAsync(host, task.Id, "w2").ConfigureAwait(false)).Error?.Code);
        Assert.True((await ExtendLockAsync(host, task.Id, "w1").ConfigureAwait(false)).IsSuccess);
        Assert.Equal(WorkflowErrors.BookmarkNotFound, (await CompleteExternalAsync(host, "missing", "w1").ConfigureAwait(false)).Error?.Code);

        var completed = (await CompleteExternalAsync(host, task.Id, "w1", new JsonObject { ["paid"] = true }).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Completed, completed.Status);
        Assert.True(completed.Variables["paid"]!.GetValue<bool>());
        Assert.Empty(await host.BookmarksAsync(instance.Id).ConfigureAwait(false));
        Assert.Equal(WorkflowErrors.BookmarkNotFound, (await CompleteExternalAsync(host, task.Id, "w1").ConfigureAwait(false)).Error?.Code);
        var completedEvent = Assert.Single(log.EventsOf(WorkflowTopics.ExternalCompleted));
        Assert.Equal((instance.Id, task.Id, "w1"), (completedEvent.InstanceId, completedEvent.TaskId, completedEvent.ActorId));

        // 失败：带 RetryAfter 解锁后推并加一次尝试，轮询后可再领；不带的使节点故障
        var retried = (await host.StartAsync("conf-external", new JsonObject { ["orderId"] = "o-2" }).ConfigureAwait(false)).ValueOrThrow();
        var attempt = Assert.Single((await FetchAsync(host, "w1", TimeSpan.FromMinutes(1)).ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal(WorkflowErrors.ExternalTaskDenied, (await FailAsync(host, attempt.Id, "w2", "boom", TimeSpan.FromMilliseconds(10)).ConfigureAwait(false)).Error?.Code);
        Assert.True((await FailAsync(host, attempt.Id, "w1", "boom", TimeSpan.FromMilliseconds(10)).ConfigureAwait(false)).IsSuccess);
        Assert.Equal(WorkflowInstanceStatus.Running, (await host.InstanceAsync(retried.Id).ConfigureAwait(false)).Status);
        clock.Advance(TimeSpan.FromMilliseconds(40));
        await host.PollAsync().ConfigureAwait(false);
        var again = Assert.Single((await FetchAsync(host, "w2", TimeSpan.FromMinutes(1)).ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal((retried.Id, 2, "o-2"), (again.InstanceId, again.Attempt, again.Payload?["order"]?.GetValue<string>()));
        Assert.True((await FailAsync(host, again.Id, "w2", "fatal").ConfigureAwait(false)).IsSuccess);
        var faulted = await host.InstanceAsync(retried.Id).ConfigureAwait(false);
        Assert.Equal((WorkflowInstanceStatus.Faulted, WorkflowErrors.ActivityFailed), (faulted.Status, faulted.Fault?.Code));
        Assert.Empty((await FetchAsync(host, "w1", TimeSpan.FromMinutes(1)).ConfigureAwait(false)).ValueOrThrow());
        var failures = log.EventsOf(WorkflowTopics.ExternalFailed).Where(data => data.InstanceId == retried.Id).ToList();
        Assert.Equal([("w1", "boom", true), ("w2", "fatal", false)], failures.Select(static data => (data.ActorId, data.Reason, data.WillRetry)));

        // 锁过期：轮询把任务重新挂起（解锁），别的工作者可领，原持有者不能再完成
        var expiring = (await host.StartAsync("conf-external", new JsonObject { ["orderId"] = "o-3" }).ConfigureAwait(false)).ValueOrThrow();
        var shortLock = Assert.Single((await FetchAsync(host, "w1", TimeSpan.FromMilliseconds(50)).ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal(expiring.Id, shortLock.InstanceId);
        Assert.Empty((await FetchAsync(host, "w2", TimeSpan.FromMinutes(1)).ConfigureAwait(false)).ValueOrThrow());
        clock.Advance(TimeSpan.FromMilliseconds(80));
        Assert.True(await host.PollAsync().ConfigureAwait(false) >= 1);
        var taken = Assert.Single((await FetchAsync(host, "w2", TimeSpan.FromMinutes(1)).ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal((expiring.Id, 1), (taken.InstanceId, taken.Attempt));
        Assert.Equal(WorkflowErrors.ExternalTaskDenied, (await CompleteExternalAsync(host, taken.Id, "w1").ConfigureAwait(false)).Error?.Code);
        Assert.Equal(WorkflowInstanceStatus.Completed, (await CompleteExternalAsync(host, taken.Id, "w2").ConfigureAwait(false)).ValueOrThrow().Status);
        Assert.Equal(3, log.EventsOf(WorkflowTopics.ExternalCreated).Count);
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
    /// <remarks>
    /// <paramref name="configure"/> 给用户任务节点加属性。
    /// </remarks>
    protected static WorkflowDefinition Approval(string name, UserTaskCompletionPolicy policy, IReadOnlyList<string> assignees, Action<WorkflowNodeBuilder>? configure = null)
        => WorkflowDefinitionBuilder.Create(name)
            .AddVariable("amount", defaultValue: 7)
            .AddStart()
            .AddUserTask("approve", "Approve", node =>
            {
                node.WithAssigneeRule(AssigneeRuleTypes.User, assignees)
                    .WithProperty(UserTaskActivity.CompletionPolicyProperty, policy.ToString())
                    .WithProperty(UserTaskActivity.TitleProperty, "Approve {{ amount }}");
                configure?.Invoke(node);
            })
            .AddDecision("check")
            .AddEnd("approved", outcome: UserTaskOutcomes.Approved).AddEnd("rejected", outcome: UserTaskOutcomes.Rejected)
            .AddTransition("start", "approve").AddTransition("approve", "check")
            .AddTransition("check", "approved", "outcome == 'approved'").AddDefaultTransition("check", "rejected")
            .Build();

    /// <summary>
    /// 某人在某实例上的唯一待办。
    /// </summary>
    private static async ValueTask<UserTask> TaskOfAsync(WorkflowEngineHost host, string assigneeId, string instanceId)
        => Assert.Single((await host.PendingAsync(assigneeId).ConfigureAwait(false)).Where(task => task.InstanceId == instanceId));

    /// <summary>
    /// 对任务服务执行一个操作。
    /// </summary>
    private static ValueTask<T> TasksAsync<T>(WorkflowEngineHost host, Func<IWorkflowTaskService, ValueTask<T>> action)
        => host.InScopeAsync(sp => action(sp.GetRequiredService<IWorkflowTaskService>()));

    /// <summary>
    /// 对外部任务服务执行一个操作。
    /// </summary>
    private static ValueTask<T> ExternalAsync<T>(WorkflowEngineHost host, Func<IWorkflowExternalTaskService, ValueTask<T>> action)
        => host.InScopeAsync(sp => action(sp.GetRequiredService<IWorkflowExternalTaskService>()));

    /// <summary>
    /// 认领。
    /// </summary>
    private static ValueTask<Result> ClaimAsync(WorkflowEngineHost host, string taskId, string actorId)
        => TasksAsync(host, service => service.ClaimAsync(new UserTaskClaim(taskId, actorId), CancellationToken.None));

    /// <summary>
    /// 释放认领。
    /// </summary>
    private static ValueTask<Result> ReleaseAsync(WorkflowEngineHost host, string taskId, string actorId)
        => TasksAsync(host, service => service.ReleaseAsync(new UserTaskRelease(taskId, actorId), CancellationToken.None));

    /// <summary>
    /// 退回。
    /// </summary>
    private static ValueTask<Result<WorkflowInstance>> ReturnAsync(WorkflowEngineHost host, string taskId, string actorId, string? targetNodeId = null, string? comment = null)
        => TasksAsync(host, service => service.ReturnAsync(new UserTaskReturn(taskId, actorId, targetNodeId, comment), CancellationToken.None));

    /// <summary>
    /// 催办。
    /// </summary>
    private static ValueTask<Result> UrgeAsync(WorkflowEngineHost host, string taskId, string actorId, string? comment = null)
        => TasksAsync(host, service => service.UrgeAsync(new UserTaskUrge(taskId, actorId, comment), CancellationToken.None));

    /// <summary>
    /// 条件查询。
    /// </summary>
    private static ValueTask<Result<IReadOnlyList<UserTask>>> QueryAsync(WorkflowEngineHost host, UserTaskQuery query)
        => TasksAsync(host, service => service.QueryAsync(query, CancellationToken.None));

    /// <summary>
    /// 领取外部任务。
    /// </summary>
    private static ValueTask<Result<IReadOnlyList<ExternalTask>>> FetchAsync(WorkflowEngineHost host, string workerId, TimeSpan lockDuration, int maxTasks = 10)
        => ExternalAsync(host, service => service.FetchAsync(PaymentTopic, workerId, lockDuration, maxTasks, CancellationToken.None));

    /// <summary>
    /// 完成外部任务。
    /// </summary>
    private static ValueTask<Result<WorkflowInstance>> CompleteExternalAsync(WorkflowEngineHost host, string taskId, string workerId, JsonObject? outputs = null)
        => ExternalAsync(host, service => service.CompleteAsync(new ExternalTaskCompletion(taskId, workerId, outputs), CancellationToken.None));

    /// <summary>
    /// 报告外部任务失败。
    /// </summary>
    private static ValueTask<Result> FailAsync(WorkflowEngineHost host, string taskId, string workerId, string error, TimeSpan? retryAfter = null)
        => ExternalAsync(host, service => service.FailAsync(new ExternalTaskFailure(taskId, workerId, error, retryAfter), CancellationToken.None));

    /// <summary>
    /// 延长外部任务的锁。
    /// </summary>
    private static ValueTask<Result> ExtendLockAsync(WorkflowEngineHost host, string taskId, string workerId)
        => ExternalAsync(host, service => service.ExtendLockAsync(new ExternalTaskLockExtension(taskId, workerId, TimeSpan.FromMinutes(1)), CancellationToken.None));
}
