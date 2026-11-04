// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Events;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Scheduling.InMemory;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Definitions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Events;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Conformance.Engine;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 同时装了内存调度的宿主上，内存工作流特性照样通过引擎套件
/// </summary>
/// <remarks>
/// 调度经同一个消息发布者发布作业事件（来源 <see cref="SchedulingTopics.Source"/>），套件顶替的代理照常确认、不当工作流事件解析（ADR-0096 修订）。宿主启动时登记一个代码作业，启动后再登记一个运行时作业，用例开始前已发出两条 <see cref="SchedulingTopics.JobScheduled"/>；触发器一年到期一次、调度轮询周期设得很长，用例期间作业不运行。每个用例经 <see cref="SchedulingWarningGuard"/> 释放宿主：调度记了告警就失败
/// </remarks>
public sealed class InMemoryWorkflowEngineConformanceWithSchedulingTests : WorkflowEngineConformance
{
    /// <summary>
    /// 代码登记的作业
    /// </summary>
    private static readonly JobName _codeJob = new("conformance.code");

    /// <summary>
    /// 启动后运行时登记的作业
    /// </summary>
    private static readonly JobName _runtimeJob = new("conformance.runtime");

    /// <summary>
    /// 一年到期一次的触发器
    /// </summary>
    private static readonly JobSchedule _yearly = JobSchedule.FromCron("0 0 1 1 *");

    /// <inheritdoc />
    /// <remarks>
    /// 内存工作流没有事务：写入立即生效，调用方回滚不撤销
    /// </remarks>
    protected override bool JoinsCallerTransaction => false;

    /// <summary>
    /// 调度的作业事件照常确认，调度不记告警，工作流事件照旧记录
    /// </summary>
    /// <remarks>
    /// 修正前套件的代理把作业事件当工作流事件解析而抛异常，调度的发布端每条记一次告警
    /// </remarks>
    [Fact]
    public async Task SchedulingEvents_AreAcknowledged_WithoutPublisherWarnings()
    {
        var log = new ConformanceLog();
        var logs = new LogCapture();
        await using var host = await StartAsync(logs, services => Register(services, log), configuration: null);

        await host.PublishAsync(WorkflowDefinitionBuilder.Create("conf-with-scheduling")
            .AddStart().AddNode("a", RecordActivity.TypeName, configure: static node => node.WithProperty(RecordActivity.LabelProperty, "a")).AddEnd()
            .AddTransition("start", "a").AddTransition("a", "end")
            .Build());
        var instance = (await host.StartAsync("conf-with-scheduling")).ValueOrThrow();

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Contains(log.EventsOf(WorkflowTopics.InstanceCompleted), data => data.InstanceId == instance.Id);
        Assert.DoesNotContain(logs.Entries, SchedulingWarningGuard.IsSchedulingWarning);
        Assert.Equal([SchedulingTopics.JobScheduled, SchedulingTopics.JobScheduled], log.OtherTopics);
    }

    /// <inheritdoc />
    protected override Task<WorkflowEngineHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null)
        => StartAsync(new LogCapture(), configure, configuration);

    /// <summary>
    /// 启动装了内存调度的宿主，日志写进 <paramref name="logs"/>
    /// </summary>
    private static async Task<WorkflowEngineHost> StartAsync(LogCapture logs, Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration)
    {
        var settings = new List<KeyValuePair<string, string?>>
        {
            new($"{InMemoryWorkflowOptions.ConfigurationSection}:PollIntervalSeconds", "3600"),
            new($"{WorkflowOptions.ConfigurationSection}:LockTimeoutSeconds", "2"),
            new($"{SchedulerOptions.ConfigurationSection}:PollIntervalSeconds", "3600"),
        };
        if (configuration is not null)
        {
            settings.AddRange(configuration);
        }

        var host = await TestHost.StartAsync(static b => b.UseFeature<InMemorySchedulingFeature>().UseFeature<InMemoryWorkflowFeature>(), settings, services =>
        {
            services.AddSingleton<ILoggerFactory>(logs);
            services.AddJob<NoopJob>(_codeJob, _yearly);
            configure(services);
        });
        var guard = new SchedulingWarningGuard(host, logs);
        // 登记运行时作业前调度先把代码作业写进存储：两条作业事件都在这一步发出
        var scheduled = await host.Services.GetRequiredService<IJobScheduler>().ScheduleAsync(new JobDefinition(_runtimeJob, _yearly, typeof(NoopJob)), CancellationToken.None);
        if (scheduled.IsFailure)
        {
            await guard.DisposeAsync();
            Assert.Fail($"Scheduling the runtime job failed with '{scheduled.Error!.Code}'.");
        }

        var poller = host.Services.GetRequiredService<DueBookmarkPoller>();
        return new WorkflowEngineHost(host.Services, ct => poller.PollOnceAsync(100, ct), guard);
    }
}
