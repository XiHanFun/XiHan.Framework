// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Calendars;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Scheduling.Conformance;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Scheduling.InMemory.Tests;

/// <summary>
/// 代码登记的作业不合法即登记或启动失败
/// </summary>
/// <remarks>
/// RFC-0013「作业类型登记」（ADR-0335）：以前代码登记的作业照收，进入服务状态后每个周期写入存储都以 <c>SCHEDULING_VALIDATION_001</c> 失败、记一条日志、下个周期再试，按名字排在它后面的代码作业也一直写不进去。
/// 登记时判定得出的（说明与数据里的孤立代理项、触发器名重复）由 <c>AddJob</c> 抛出参数异常；日历可以在作业之后登记，留到调度器启动时核对，不合即启动失败
/// </remarks>
public sealed class CodeJobValidationTests
{
    /// <summary>
    /// 说明与数据里有孤立代理项时的原因，与运行时 <c>ScheduleAsync</c> 的 <c>reason</c> 相同
    /// </summary>
    private const string LoneSurrogate = "the definition has a lone surrogate in a description or the data";

    /// <summary>
    /// 用例的作业名
    /// </summary>
    private static readonly JobName _name = new("code.invalid");

    [Fact]
    public void AddJob_RejectsALoneSurrogate_AndDuplicateTriggers_AndRegistersNothing()
    {
        var services = new ServiceCollection();
        var hourly = new JobTriggerDefinition("t", JobSchedule.Every(TimeSpan.FromHours(1)));
        (JobDefinition Definition, string Reason)[] cases =
        [
            (Manual() with { Description = "nightly \uD800" }, LoneSurrogate),
            (Manual() with { Data = new JsonObject { ["list"] = new JsonArray(new JsonObject { ["k\uDC00"] = "v" }) } }, LoneSurrogate),
            (Manual() with { Triggers = [hourly with { Data = new JsonObject { ["k"] = "\uDE00\uD83D" } }] }, LoneSurrogate),
            (Manual() with { Triggers = [hourly, hourly with { Priority = 9 }] }, "trigger 't' is defined more than once"),
        ];

        foreach (var (definition, reason) in cases)
        {
            // 修之前登记照收，启动之后每个周期同步失败、记日志重试
            var exception = Assert.Throws<ArgumentException>(() => services.AddJob<RecordingJob>(definition));
            Assert.Equal("definition", exception.ParamName);
            Assert.StartsWith($"Job 'code.invalid' is invalid: {reason}.", exception.Message, StringComparison.Ordinal);
        }

        Assert.False(services.GetJobRegistry().Find(_name).HasValue);
    }

    [Fact]
    public async Task AnUnregisteredCalendar_FailsTheHostStart_WithTheJobNameAndReason()
    {
        // 登记的那一刻判定不了：日历可以在作业之后登记
        var failure = await Assert.ThrowsAsync<FrameworkException>(() => StartAsync(static services => services.AddJob<RecordingJob>(_name, JobSchedule.Every(TimeSpan.FromMinutes(1)) with { Calendar = "missing" })));

        Assert.Equal(KernelErrors.FeatureStartFailed, failure.Code);
        var inner = Assert.IsType<FrameworkException>(failure.InnerException);
        Assert.Equal(SchedulingErrors.DefinitionInvalid, inner.Code);
        Assert.Equal("Code-defined job 'code.invalid' is invalid: calendar 'missing' is not registered.", inner.Message);
    }

    [Fact]
    public async Task ADefinitionChangedAfterRegistration_FailsTheHostStart()
    {
        // 数据是可变的 JsonObject：登记之后才改坏的同样在启动时拦下，不留到同步
        var data = new JsonObject { ["k"] = "v" };
        var failure = await Assert.ThrowsAsync<FrameworkException>(() => StartAsync(services =>
        {
            services.AddJob<RecordingJob>(Manual() with { Data = data });
            data["k"] = "v\uD800";
        }));

        var inner = Assert.IsType<FrameworkException>(failure.InnerException);
        Assert.Equal((SchedulingErrors.DefinitionInvalid, $"Code-defined job 'code.invalid' is invalid: {LoneSurrogate}."), (inner.Code, inner.Message));
    }

    [Fact]
    public async Task ACalendarRegisteredAfterTheJob_IsFoundAtStart_AndTheJobIsWritten()
    {
        await using var host = await StartAsync(static services =>
        {
            services.AddJob<RecordingJob>(_name, JobSchedule.Every(TimeSpan.FromMinutes(1)) with { Calendar = "late" });
            services.AddJobCalendar("late", new WeeklyCalendar([DayOfWeek.Sunday]));
        });

        await host.Services.GetRequiredService<JobSchedulerEngine>().PollOnceAsync(CancellationToken.None);

        var status = (await host.Services.GetRequiredService<IJobScheduler>().FindStatusAsync(_name, CancellationToken.None)).ValueOrThrow().ValueOrThrow();
        Assert.Equal("late", Assert.Single(status.Triggers).Schedule.Calendar);
    }

    /// <summary>
    /// 只能手动触发的作业定义
    /// </summary>
    private static JobDefinition Manual() => new(_name, typeof(RecordingJob));

    /// <summary>
    /// 启动只装配内存调度的宿主，轮询不在用例里运行
    /// </summary>
    private static Task<TestHost> StartAsync(Action<IServiceCollection> register)
        => TestHost.StartAsync(
            static b => b.UseFeature<InMemorySchedulingFeature>(),
            [new("XiHan:Scheduling:PollIntervalSeconds", "3600")],
            services =>
            {
                services.AddSingleton(new RecordingJobLog());
                register(services);
            });
}
