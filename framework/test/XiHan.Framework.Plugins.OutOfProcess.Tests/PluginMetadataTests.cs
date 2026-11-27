// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 回调元数据还原成调用上下文（<c>invocation.md</c> §2，ADR-0237）
/// </summary>
public sealed class PluginMetadataTests
{
    [Fact]
    public void CreateContext_TakesTheEarlierOfTheMetadataAndTheCallDeadline()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var metadata = new InvocationMetadata { Deadline = "2026-11-18T03:25:05Z" };
        var call = new DateTimeOffset(2026, 11, 18, 3, 25, 0, TimeSpan.Zero);

        // 修之前 Host.Invoke 与 Host.Publish 不看 gRPC 调用自身的截止时间
        Assert.Equal(call, PluginMetadata.CreateContext(services, metadata, "xihan.samples.echo", CancellationToken.None, call).Deadline);
        Assert.Equal(call, PluginMetadata.CreateContext(services, null, "xihan.samples.echo", CancellationToken.None, call).Deadline);
        Assert.Equal(call.AddSeconds(5), PluginMetadata.CreateContext(services, metadata, "xihan.samples.echo", CancellationToken.None).Deadline);
    }

    [Fact]
    public void CreateContext_IgnoresADeadlineOutsideTheGrammar()
    {
        var services = new ServiceCollection().BuildServiceProvider();

        // 修之前 DateTimeOffset.TryParse 把它读成 UTC 03:25
        var context = PluginMetadata.CreateContext(services, new InvocationMetadata { Deadline = "2026-11-18 03:25:00" }, "xihan.samples.echo", CancellationToken.None);

        Assert.Null(context.Deadline);
    }

    [Fact]
    public void CreateContext_WarnsAboutAnUnreadableDeadline_WithoutTheText_AndLimitsTheRate()
    {
        var logs = new LogCapture();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 24, 8, 0, 0, TimeSpan.Zero));
        using var services = new ServiceCollection().AddSingleton<ILoggerFactory>(logs).AddSingleton<TimeProvider>(clock).AddSingleton<UnreadableDeadlineLog>().BuildServiceProvider();
        const string Unreadable = "2026-11-18 03:25:00 forged-log-line";
        var call = new DateTimeOffset(2026, 11, 24, 8, 0, 30, TimeSpan.Zero);

        // 读不出的照常当作没有：仍不晚于 gRPC 调用自身的截止时间（invocation.md §2，ADR-0279）
        Assert.Equal(call, PluginMetadata.CreateContext(services, new InvocationMetadata { Deadline = Unreadable }, "xihan.samples.echo", CancellationToken.None, call).Deadline);
        Assert.Null(PluginMetadata.CreateContext(services, new InvocationMetadata { Deadline = "tomorrow" }, "xihan.samples.echo", CancellationToken.None).Deadline);
        // 读得出、没有给（空串）与没有元数据都不记
        PluginMetadata.CreateContext(services, new InvocationMetadata { Deadline = "2026-11-24T08:00:05Z" }, "xihan.samples.echo", CancellationToken.None);
        PluginMetadata.CreateContext(services, new InvocationMetadata(), "xihan.samples.echo", CancellationToken.None);
        PluginMetadata.CreateContext(services, null, "xihan.samples.echo", CancellationToken.None);
        clock.Advance(UnreadableDeadlineLog.Window);
        PluginMetadata.CreateContext(services, new InvocationMetadata { Deadline = "tomorrow" }, "xihan.samples.cache", CancellationToken.None);

        var entries = logs.Entries;
        Assert.Equal(2, entries.Count);
        Assert.All(entries, static entry => Assert.Equal(LogLevel.Warning, entry.Level));
        Assert.Equal($"The deadline in the callback metadata of plugin 'xihan.samples.echo' ({Unreadable.Length} characters) could not be read and is ignored; the callback continues without it. Similar warnings suppressed since the previous one: 0.", entries[0].Message);
        Assert.Equal("The deadline in the callback metadata of plugin 'xihan.samples.cache' (8 characters) could not be read and is ignored; the callback continues without it. Similar warnings suppressed since the previous one: 1.", entries[1].Message);
        Assert.DoesNotContain(entries, static entry => entry.Message.Contains("forged-log-line", StringComparison.Ordinal) || entry.Message.Contains("tomorrow", StringComparison.Ordinal));
    }

    [Fact]
    public void Check_RefusesARegisteredValueWithALoneSurrogate_AndFromContextDoesNotWriteIt()
    {
        // invocation.md §2（ADR-0334）：上下文里只有幂等键能带着孤立的代理项进来；修之前 protobuf 把它写成 U+FFFD
        var services = new ServiceCollection().BuildServiceProvider();
        var lone = App.CreateContext(services, new InvocationContextOptions { Baggage = new Dictionary<string, string> { [BaggageKeys.IdempotencyKey] = "k\uDBFF" } }, CancellationToken.None);

        var error = PluginMetadata.Check(lone, "xihan.samples.echo", "echoes.create").Error;
        Assert.Equal((PluginErrors.Unexpected, PluginMessages.ProcessFailed), (error?.Code, error?.MessageKey));
        Assert.Equal(("xihan.samples.echo", "echoes.create", "baggage 'xihan.idempotency-key' has a lone surrogate and cannot be encoded as UTF-8"), (error!.Details!["id"], error.Details["phase"], error.Details["reason"]));
        // 写出的一侧最后一道：没先核对就写是编程错误，不交给载体宽松地编码
        Assert.Throws<ArgumentException>(() => PluginMetadata.FromContext(lone));

        // 未登记的键不出进程，不核对；成对的代理项照常写出
        var unregistered = App.CreateContext(services, new InvocationContextOptions { Baggage = new Dictionary<string, string> { ["xihan.local-note"] = "x\uD800", [BaggageKeys.IdempotencyKey] = "k😀" } }, CancellationToken.None);
        Assert.True(PluginMetadata.Check(unregistered, "xihan.samples.echo", "echoes.create").IsSuccess);
        var written = PluginMetadata.FromContext(unregistered);
        Assert.Equal("k😀", written.Baggage[BaggageKeys.IdempotencyKey]);
        Assert.False(written.Baggage.ContainsKey("xihan.local-note"));
    }

    [Fact]
    public void FromCall_TreatsMaxValueAsNoDeadline()
    {
        Assert.Null(PluginDeadlines.FromCall(DateTime.MaxValue));
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 3, 25, 0, TimeSpan.Zero), PluginDeadlines.FromCall(new DateTime(2026, 11, 18, 3, 25, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task Enter_MakesTheContextTheCurrentInvocationOfItsScope()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        await using var scope = host.Services.CreateAsyncScope();
        var context = PluginMetadata.CreateContext(scope.ServiceProvider, new InvocationMetadata(), "xihan.samples.echo", CancellationToken.None);
        var accessor = scope.ServiceProvider.GetRequiredService<IInvocationAccessor>();

        using (PluginMetadata.Enter(context))
        {
            Assert.Same(context, accessor.Current.ValueOrThrow());
        }

        Assert.False(accessor.Current.HasValue);
    }
}
