// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Activities;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Tasks;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 活动里的时刻属性按表达式的时间文法读
/// </summary>
/// <remarks>
/// RFC-0021「活动」的「活动里的时间」（ADR-0296）。<c>Delay.Until</c> 与 <c>UserTask.DueAt</c> 以前交给平台的日期解析：只有时间的文本补上机器当天的日期，斜线日期、月份名也读得出
/// </remarks>
public sealed class ActivityTimeTests
{
    [Theory]
    [InlineData("'2030-01-02T03:04:05+08:00'", "2030-01-01T19:04:05.0000000+00:00")]
    [InlineData("'2030-01-02'", "2030-01-02T00:00:00.0000000+00:00")]
    [InlineData("'2030-01-02 03:04'", "2030-01-02T03:04:00.0000000+00:00")]
    [InlineData("'2030-01-02t03:04:05.123456789z'", "2030-01-02T03:04:05.1234567+00:00")]
    [InlineData("date('2030-01-02') ", "2030-01-02T00:00:00.0000000+00:00")]
    public async Task DelayUntil_ReadsTheTimeGrammar_AsUtc(string until, string due)
    {
        await using var host = await StartHostAsync();
        var instance = await RunAsync(host, "delay-until", ActivityTypes.Delay, node => node.WithProperty(DelayActivity.UntilProperty, until));

        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        var bookmarks = await BookmarksAsync(host, instance.Id);
        var timer = Assert.Single(bookmarks, static bookmark => bookmark.Kind == BookmarkKinds.Timer);
        Assert.NotNull(timer.DueAt);
        Assert.Equal(DateTimeOffset.Parse(due, System.Globalization.CultureInfo.InvariantCulture), timer.DueAt.Value);
        Assert.Equal(TimeSpan.Zero, timer.DueAt.Value.Offset);
    }

    [Theory]
    [InlineData("'12:30'")]
    [InlineData("'01/02/2030'")]
    [InlineData("'Jan 2, 2030'")]
    [InlineData("'2030-1-2'")]
    [InlineData("' 2030-01-02'")]
    [InlineData("'2030-01-02T03'")]
    [InlineData("20300102")]
    public async Task DelayUntil_RejectsOtherWritings_AsAnInvalidProperty(string until)
    {
        await using var host = await StartHostAsync();
        var instance = await RunAsync(host, "delay-until-invalid", ActivityTypes.Delay, node => node.WithProperty(DelayActivity.UntilProperty, until));

        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        var fault = Assert.IsType<WorkflowFault>(instance.Fault);
        Assert.Equal((WorkflowErrors.InputInvalid, WorkflowMessages.PropertyNotDateTime), (fault.Code, fault.MessageKey));
        Assert.Equal(DelayActivity.UntilProperty, fault.Arguments?["name"]);
    }

    [Theory]
    [InlineData("2030-01-02T03:04:05+08:00", "2030-01-01T19:04:05.0000000+00:00")]
    [InlineData("2030-01-02", "2030-01-02T00:00:00.0000000+00:00")]
    [InlineData("'2030-01-02 03:04'", "2030-01-02T03:04:00.0000000+00:00")]
    public async Task UserTaskDueAt_ReadsTheTimeGrammar_AsUtc(string dueAt, string due)
    {
        await using var host = await StartHostAsync();
        var instance = await RunAsync(host, "task-due", ActivityTypes.UserTask, node => node
            .WithProperty(UserTaskActivity.AssigneesProperty, new JsonArray("alice"))
            .WithProperty(UserTaskActivity.DueAtProperty, dueAt));

        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        await using var scope = host.Services.CreateAsyncScope();
        var page = (await scope.ServiceProvider.GetRequiredService<IWorkflowTaskService>().QueryAsync(new UserTaskQuery { AssigneeId = "alice" }, CancellationToken.None)).ValueOrThrow();
        var task = Assert.Single(page.Items);
        Assert.NotNull(task.DueAt);
        Assert.Equal(DateTimeOffset.Parse(due, System.Globalization.CultureInfo.InvariantCulture), task.DueAt.Value);
        Assert.Equal(TimeSpan.Zero, task.DueAt.Value.Offset);
    }

    [Fact]
    public async Task UserTaskDueAt_OnlyTime_IsAnExpression_NotTodayAtThatTime()
    {
        // 以前读成机器当天的 12:30；现在不合文法，当表达式求值，冒号不是表达式里的字符
        await using var host = await StartHostAsync();
        var instance = await RunAsync(host, "task-due-time", ActivityTypes.UserTask, node => node
            .WithProperty(UserTaskActivity.AssigneesProperty, new JsonArray("alice"))
            .WithProperty(UserTaskActivity.DueAtProperty, "12:30"));

        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        var fault = Assert.IsType<WorkflowFault>(instance.Fault);
        Assert.Equal((WorkflowErrors.ExpressionInvalid, WorkflowMessages.ExpressionUnexpectedCharacter), (fault.Code, fault.MessageKey));
    }

    [Fact]
    public async Task UserTaskDueAt_ThatEvaluatesToAnotherWriting_IsAnInvalidProperty()
    {
        await using var host = await StartHostAsync();
        var instance = await RunAsync(host, "task-due-slash", ActivityTypes.UserTask, node => node
            .WithProperty(UserTaskActivity.AssigneesProperty, new JsonArray("alice"))
            .WithProperty(UserTaskActivity.DueAtProperty, "'01/02/2030'"));

        Assert.Equal(WorkflowInstanceStatus.Faulted, instance.Status);
        var fault = Assert.IsType<WorkflowFault>(instance.Fault);
        Assert.Equal((WorkflowErrors.InputInvalid, WorkflowMessages.PropertyNotDateTime), (fault.Code, fault.MessageKey));
        Assert.Equal(UserTaskActivity.DueAtProperty, fault.Arguments?["name"]);
    }

    /// <summary>
    /// 起一个不轮询的内存宿主
    /// </summary>
    private static Task<TestHost> StartHostAsync()
        => TestHost.StartAsync(static b => b.UseFeature<InMemoryWorkflowFeature>(), [new($"{InMemoryWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "3600")]);

    /// <summary>
    /// 发布并启动一个只有一个活动节点的工作流
    /// </summary>
    private static async Task<WorkflowInstance> RunAsync(TestHost host, string name, string activityType, Action<WorkflowNodeBuilder> configure)
    {
        var definition = WorkflowDefinitionBuilder.Create(new WorkflowName(name))
            .AddStart()
            .AddNode("step", new ActivityType(activityType), configure: configure)
            .AddEnd()
            .AddTransition("start", "step").AddTransition("step", "end")
            .Build();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionCatalog>();
            var created = (await catalog.CreateAsync(definition, CancellationToken.None)).ValueOrThrow();
            (await catalog.PublishAsync(created.Id, CancellationToken.None)).ValueOrThrow();
        }

        await using var run = host.Services.CreateAsyncScope();
        return (await run.ServiceProvider.GetRequiredService<IWorkflowEngine>().StartAsync(new WorkflowStartRequest { Name = new WorkflowName(name) }, CancellationToken.None)).ValueOrThrow();
    }

    /// <summary>
    /// 实例的全部等待点
    /// </summary>
    private static async Task<IReadOnlyList<WorkflowBookmarkSummary>> BookmarksAsync(TestHost host, string instanceId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IWorkflowEngine>().BookmarksAsync(instanceId, CancellationToken.None)).ValueOrThrow();
    }
}
