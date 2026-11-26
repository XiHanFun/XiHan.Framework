// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Scheduling.Conformance;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Scheduling.InMemory.Tests;

/// <summary>
/// 作业定义的说明与数据不含孤立的代理项
/// </summary>
/// <remarks>
/// RFC-0013「调度器 <c>IJobScheduler</c>」（ADR-0321）：代码作业的指纹散列这些文本的宽松 UTF-8，数据先经 JSON 写出器写成文本，孤立的代理项都换成 U+FFFD；只差在它上面的两份定义指纹相同，持久化的定义读回来也已换成 U+FFFD
/// </remarks>
public sealed class JobDefinitionSurrogateTests
{
    /// <summary>
    /// 用例的作业名
    /// </summary>
    private static readonly JobName _name = new("surrogates");

    [Fact]
    public async Task DefinitionsWithALoneSurrogate_AreInvalid_WhereverItIs()
    {
        await using var host = await StartAsync();
        var scheduler = host.Services.GetRequiredService<IJobScheduler>();
        var trigger = new JobTriggerDefinition("t", JobSchedule.Every(TimeSpan.FromHours(1)));

        // 改之前都写进存储：说明与数据经宽松编码之后与 U+FFFD 的那一份是同一段字节，指纹分不开
        Assert.Equal(Encoding.UTF8.GetBytes("a\uD800"), Encoding.UTF8.GetBytes("a\uDBFF"));
        JobDefinition[] definitions =
        [
            Manual() with { Description = "nightly \uD800" },
            Manual() with { Data = new JsonObject { ["k"] = "v\uDC00" } },
            Manual() with { Data = new JsonObject { ["k\uD800"] = "v" } },
            Manual() with { Data = new JsonObject { ["list"] = new JsonArray("a", new JsonObject { ["deep"] = "\uDE00\uD83D" }) } },
            Manual() with { Triggers = [trigger with { Description = "every hour\uDBFF" }] },
            Manual() with { Triggers = [trigger with { Data = new JsonObject { ["k"] = "\uDFFF" } }] },
        ];

        var results = await scheduler.ScheduleAsync(definitions, CancellationToken.None);

        Assert.All(results, static result =>
        {
            Assert.Equal((SchedulingErrors.DefinitionInvalid, SchedulingMessages.DefinitionInvalid), (result.Error?.Code, result.Error?.MessageKey));
            Assert.Equal("the definition has a lone surrogate in a description or the data", result.Error?.Details?["reason"]);
        });
        Assert.False((await scheduler.FindStatusAsync(_name, CancellationToken.None)).ValueOrThrow().HasValue);
    }

    [Fact]
    public async Task SurrogatePairs_AreValidText_AndAreScheduled()
    {
        await using var host = await StartAsync();
        var scheduler = host.Services.GetRequiredService<IJobScheduler>();
        var definition = Manual() with
        {
            Description = "nightly 😀",
            Data = new JsonObject { ["k😀"] = "v😀" },
            Triggers = [new JobTriggerDefinition("t", JobSchedule.Every(TimeSpan.FromHours(1))) { Description = "😀" }],
        };

        Assert.True((await scheduler.ScheduleAsync(definition, CancellationToken.None)).IsSuccess);
        var status = (await scheduler.FindStatusAsync(_name, CancellationToken.None)).ValueOrThrow().ValueOrThrow();
        Assert.Equal(("nightly 😀", "v😀"), (status.Job.Description, status.Job.Data["k😀"]!.GetValue<string>()));
    }

    /// <summary>
    /// 只能手动触发的作业定义
    /// </summary>
    private static JobDefinition Manual() => new(_name, typeof(RecordingJob));

    /// <summary>
    /// 启动只装配内存调度的宿主，轮询不在用例里运行
    /// </summary>
    private static Task<TestHost> StartAsync()
        => TestHost.StartAsync(
            static b => b.UseFeature<InMemorySchedulingFeature>(),
            [new("XiHan:Scheduling:PollIntervalSeconds", "3600")],
            static services =>
            {
                services.AddSingleton(new RecordingJobLog());
                services.AddJobType<RecordingJob>();
            });
}
