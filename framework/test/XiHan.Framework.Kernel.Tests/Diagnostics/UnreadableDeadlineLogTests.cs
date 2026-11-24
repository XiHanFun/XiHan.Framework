// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Kernel.Tests.Diagnostics;

/// <summary>
/// 截止时间读不出时的限频告警（ADR-0279）
/// </summary>
/// <remarks>
/// 窗口内只记一条、窗口过后的一条带被压掉的条数、请求头与插件回调分开计、并发下每个窗口只放行一条且计数不丢、内核把它登记为单例
/// </remarks>
public sealed class UnreadableDeadlineLogTests
{
    [Fact]
    public void ReportHeader_LogsOnce_ThenSuppressesWithinTheWindow()
    {
        var (log, logs, clock) = Create();

        log.ReportHeader("xihan-deadline", 19, "orders.create");
        log.ReportHeader("xihan-deadline", 10, "orders.get");
        clock.Advance(UnreadableDeadlineLog.Window - TimeSpan.FromTicks(1));
        log.ReportHeader("grpc-timeout", 3, null);

        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(typeof(UnreadableDeadlineLog).FullName, entry.Category);
        Assert.Equal("The value of request header 'xihan-deadline' (19 characters) could not be read as a deadline and is ignored; the invocation of operation 'orders.create' continues without it. Similar warnings suppressed since the previous one: 0.", entry.Message);
    }

    [Fact]
    public void ReportHeader_AfterTheWindow_LogsAgainWithTheSuppressedCount()
    {
        var (log, logs, clock) = Create();
        log.ReportHeader("xihan-deadline", 19, "orders.create");
        log.ReportHeader("xihan-deadline", 19, "orders.create");
        log.ReportHeader("xihan-deadline", 19, "orders.create");

        clock.Advance(UnreadableDeadlineLog.Window);
        log.ReportHeader("grpc-timeout", 3, null);
        // 新窗口从刚记的这一条算起：紧接着的又被压掉，再过一个窗口带出来的是 1 而不是累计的 3
        log.ReportHeader("xihan-deadline", 19, "orders.create");
        clock.Advance(UnreadableDeadlineLog.Window);
        log.ReportHeader("xihan-deadline", 7, null);

        var entries = logs.Entries;
        Assert.Equal(3, entries.Count);
        Assert.Equal("The value of request header 'grpc-timeout' (3 characters) could not be read as a deadline and is ignored; the invocation continues without it. Similar warnings suppressed since the previous one: 2.", entries[1].Message);
        Assert.EndsWith("Similar warnings suppressed since the previous one: 1.", entries[2].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginMetadata_IsLimitedApartFromHeaders()
    {
        var (log, logs, _) = Create();

        log.ReportHeader("xihan-deadline", 19, null);
        log.ReportPluginMetadata("xihan.samples.echo", 19);
        log.ReportPluginMetadata("xihan.samples.echo", 19);
        log.ReportHeader("xihan-deadline", 19, null);

        var entries = logs.Entries;
        Assert.Equal(2, entries.Count);
        Assert.Equal(LogLevel.Warning, entries[1].Level);
        Assert.Equal("The deadline in the callback metadata of plugin 'xihan.samples.echo' (19 characters) could not be read and is ignored; the callback continues without it. Similar warnings suppressed since the previous one: 0.", entries[1].Message);
    }

    [Fact]
    public async Task ConcurrentReports_LogOncePerWindow_AndLoseNoCount()
    {
        var (log, logs, clock) = Create();
        const int Workers = 8;
        const int PerWorker = 2_000;

        // 两个窗口各放一批并发的报告：每个窗口只有一条放行，其余都算进被压掉的条数
        for (var window = 0; window < 2; window++)
        {
            using var start = new ManualResetEventSlim();
            var workers = Enumerable.Range(0, Workers).Select(_ => Task.Run(() =>
            {
                start.Wait();
                for (var index = 0; index < PerWorker; index++)
                {
                    log.ReportHeader("xihan-deadline", 19, null);
                }
            })).ToArray();
            start.Set();
            await Task.WhenAll(workers);
            clock.Advance(UnreadableDeadlineLog.Window);
        }

        log.ReportHeader("xihan-deadline", 19, null);

        // 抢到窗口的那一条取走计数时，同一批里没抢到的可能已经记了数：单条上的数会在相邻两条之间挪动几个，合计不变
        var entries = logs.Entries;
        Assert.Equal(3, entries.Count);
        Assert.Equal((2L * Workers * PerWorker) + 1 - entries.Count, entries.Sum(static entry => SuppressedOf(entry.Message)));
    }

    [Fact]
    public void Report_RejectsArgumentsTheCallSiteMustSupply()
    {
        var (log, _, _) = Create();

        Assert.Throws<ArgumentException>(() => log.ReportHeader(string.Empty, 1, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => log.ReportHeader("xihan-deadline", -1, null));
        Assert.Throws<ArgumentException>(() => log.ReportPluginMetadata(string.Empty, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => log.ReportPluginMetadata("xihan.samples.echo", -1));
    }

    [Fact]
    public async Task TheKernel_RegistersItAsASingleton_OnTheHostLoggerAndClock()
    {
        var logs = new LogCapture();
        var clock = new FakeTimeProvider();
        await using var host = await TestHost.StartAsync(static _ => { }, services: services => services.AddSingleton<ILoggerFactory>(logs).AddSingleton<TimeProvider>(clock));

        var log = host.Services.GetRequiredService<UnreadableDeadlineLog>();
        Assert.Same(log, host.Services.GetRequiredService<UnreadableDeadlineLog>());
        log.ReportHeader("xihan-deadline", 19, null);
        log.ReportHeader("xihan-deadline", 19, null);
        clock.Advance(UnreadableDeadlineLog.Window);
        log.ReportHeader("xihan-deadline", 19, null);

        var entries = logs.Entries.Where(static entry => entry.Category == typeof(UnreadableDeadlineLog).FullName).ToList();
        Assert.Equal(2, entries.Count);
        Assert.EndsWith("Similar warnings suppressed since the previous one: 1.", entries[1].Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 一条告警里被压掉的条数
    /// </summary>
    /// <param name="message">格式化后的日志消息，以「: 条数.」结尾</param>
    private static long SuppressedOf(string message)
    {
        var start = message.LastIndexOf(": ", StringComparison.Ordinal) + 2;
        return long.Parse(message.AsSpan(start, message.Length - start - 1), NumberStyles.None, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 建一个写进内存日志、时钟可推进的告警服务
    /// </summary>
    private static (UnreadableDeadlineLog Log, LogCapture Logs, FakeTimeProvider Clock) Create()
    {
        var logs = new LogCapture();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 24, 8, 0, 0, TimeSpan.Zero));
        return (new UnreadableDeadlineLog(logs, clock), logs, clock);
    }
}
