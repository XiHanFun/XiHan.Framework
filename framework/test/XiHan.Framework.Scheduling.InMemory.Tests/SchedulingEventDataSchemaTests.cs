// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Events;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Scheduling.Conformance;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Scheduling.InMemory.Tests;

/// <summary>
/// 调度的事件带上按 <c>type</c> 登记的数据模式
/// </summary>
/// <remarks>
/// RFC-0013「引擎」的事件与 RFC-0009「框架事件的数据模式」（ADR-0323）：修之前调度发布的信封都不带 <c>dataschema</c>
/// </remarks>
public sealed class SchedulingEventDataSchemaTests
{
    /// <summary>
    /// 登记给 <c>scheduling.run.started</c> 的数据模式
    /// </summary>
    private const string RunStartedSchema = "https://example.com/schemas/scheduling-event.json";

    [Fact]
    public async Task RunEvents_CarryTheDataSchemaRegisteredForTheirType_AndOthersDoNot()
    {
        var log = new RecordingJobLog();
        var publisher = new RecordingPublisher();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemorySchedulingFeature>(),
            [new("XiHan:Scheduling:PollIntervalSeconds", "3600")],
            services =>
            {
                services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 11, 27, 0, 0, 0, TimeSpan.Zero)));
                services.AddSingleton(log);
                services.AddSingleton<IMessagePublisher>(publisher);
                services.AddMessageDataSchema(SchedulingTopics.RunStarted, RunStartedSchema);
                services.AddJob<RecordingJob>(new JobName("manual"), JobSchedule.Every(TimeSpan.FromHours(1)));
            });
        var engine = host.Services.GetRequiredService<JobSchedulerEngine>();

        Assert.True((await engine.TriggerAsync(new JobName("manual"), CancellationToken.None)).IsSuccess);

        var started = await publisher.WaitAsync(SchedulingTopics.RunStarted, TimeSpan.FromSeconds(10));
        var succeeded = await publisher.WaitAsync(SchedulingTopics.RunSucceeded, TimeSpan.FromSeconds(10));
        Assert.Equal((SchedulingTopics.Source, RunStartedSchema), (started.Source, started.DataSchema));
        Assert.Null(succeeded.DataSchema);
    }
}
