// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Testing.Tests;

/// <summary>
/// <see cref="LogCapture"/> 的收集、两种接入方式、条数上限与并发
/// </summary>
public sealed class LogCaptureTests
{
    [Fact]
    public void Entries_KeepLevelCategoryMessageAndException_InWriteOrder()
    {
        using var logs = new LogCapture();
        var failure = new InvalidOperationException("The store is down.");

        Write(logs.CreateLogger("orders"), LogLevel.Information, "Order 42 was placed.");
        Write(logs.CreateLogger("billing"), LogLevel.Warning, "Charging 42 failed.", failure);

        Assert.Collection(
            logs.Entries,
            first =>
            {
                Assert.Equal(LogLevel.Information, first.Level);
                Assert.Equal("orders", first.Category);
                Assert.Equal("Order 42 was placed.", first.Message);
                Assert.Null(first.Exception);
            },
            second =>
            {
                Assert.Equal(LogLevel.Warning, second.Level);
                Assert.Equal("billing", second.Category);
                Assert.Equal("Charging 42 failed.", second.Message);
                Assert.Same(failure, second.Exception);
            });
    }

    [Fact]
    public void EveryLevelIsEnabled_ExceptNone()
    {
        using var logs = new LogCapture();
        var logger = logs.CreateLogger("levels");

        Assert.All(
            [LogLevel.Trace, LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical],
            level => Assert.True(logger.IsEnabled(level)));
        Assert.False(logger.IsEnabled(LogLevel.None));
        Assert.Null(logger.BeginScope("scope"));
    }

    [Fact]
    public void RegisteredAsTheLoggerFactory_ReceivesWhatInjectedLoggersWrite()
    {
        using var logs = new LogCapture();
        using var services = new ServiceCollection().AddLogging().AddSingleton<ILoggerFactory>(logs).BuildServiceProvider();

        Write(services.GetRequiredService<ILogger<LogCaptureTests>>(), LogLevel.Error, "Injected.");

        var entry = Assert.Single(logs.Entries);
        Assert.Equal(typeof(LogCaptureTests).FullName, entry.Category);
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    [Fact]
    public void AddedAsAProvider_ReceivesWhatTheBuilderRoutesToIt()
    {
        using var logs = new LogCapture();
        using var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug).AddProvider(logs));

        Write(factory.CreateLogger("routed"), LogLevel.Debug, "Through the builder.");

        var entry = Assert.Single(logs.Entries);
        Assert.Equal(("routed", "Through the builder."), (entry.Category, entry.Message));
    }

    [Fact]
    public void Entries_IsASnapshot_LaterWritesAreNotInIt()
    {
        using var logs = new LogCapture();
        var logger = logs.CreateLogger("snapshot");
        Write(logger, LogLevel.Information, "first");

        var before = logs.Entries;
        Write(logger, LogLevel.Information, "second");

        Assert.Single(before);
        Assert.Equal(2, logs.Entries.Count);
    }

    [Fact]
    public void Capacity_DropsTheOldestEntries()
    {
        using var logs = new LogCapture(capacity: 3);
        var logger = logs.CreateLogger("bounded");

        for (var index = 1; index <= 5; index++)
        {
            Write(logger, LogLevel.Information, $"entry {index}");
        }

        Assert.Equal(3, logs.Capacity);
        Assert.Equal(["entry 3", "entry 4", "entry 5"], logs.Entries.Select(static e => e.Message));
    }

    [Fact]
    public void Capacity_DefaultsToTenThousand_AndMustBePositive()
    {
        using var logs = new LogCapture();

        Assert.Equal(LogCapture.DefaultCapacity, logs.Capacity);
        Assert.Equal(10_000, LogCapture.DefaultCapacity);
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogCapture(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogCapture(-1));
    }

    [Fact]
    public void AddProvider_IsNotSupported_AndANullCategoryIsRejected()
    {
        using var logs = new LogCapture();
        using var other = new LogCapture();

        Assert.Throws<NotSupportedException>(() => logs.AddProvider(other));
        Assert.Throws<ArgumentNullException>(() => logs.CreateLogger(null!));
    }

    [Fact]
    public void Dispose_KeepsWhatWasCaptured_AndLaterWritesAreStillRecorded()
    {
        var logs = new LogCapture();
        var logger = logs.CreateLogger("disposed");
        Write(logger, LogLevel.Information, "before");

        logs.Dispose();
        Write(logger, LogLevel.Information, "after");

        Assert.Equal(["before", "after"], logs.Entries.Select(static e => e.Message));
    }

    [Fact]
    public async Task ConcurrentWritersAndReaders_LoseNothing_AndNeverExceedTheCapacity()
    {
        const int Writers = 8;
        const int PerWriter = 500;
        using var bounded = new LogCapture(capacity: 100);
        using var roomy = new LogCapture();
        using var stop = new CancellationTokenSource();

        // 一边写一边读快照：读到的条数任何时刻都不超过上限
        var reader = Task.Run(
            () =>
            {
                var largest = 0;
                while (!stop.IsCancellationRequested)
                {
                    largest = Math.Max(largest, bounded.Entries.Count);
                }

                return largest;
            },
            TestContext.Current.CancellationToken);
        await Task.WhenAll(Enumerable.Range(0, Writers).Select(writer => Task.Run(
            () =>
            {
                var tight = bounded.CreateLogger($"writer-{writer}");
                var wide = roomy.CreateLogger($"writer-{writer}");
                for (var index = 0; index < PerWriter; index++)
                {
                    var text = index.ToString(CultureInfo.InvariantCulture);
                    Write(tight, LogLevel.Information, text);
                    Write(wide, LogLevel.Information, text);
                }
            },
            TestContext.Current.CancellationToken)));
        await stop.CancelAsync();

        Assert.True(await reader <= 100);
        Assert.Equal(100, bounded.Entries.Count);
        // 上限够大时一条不丢，每个写者自己的顺序保持
        var entries = roomy.Entries;
        Assert.Equal(Writers * PerWriter, entries.Count);
        Assert.All(
            entries.GroupBy(static e => e.Category),
            group => Assert.Equal(Enumerable.Range(0, PerWriter).Select(static i => i.ToString(CultureInfo.InvariantCulture)), group.Select(static e => e.Message)));
    }

    /// <summary>
    /// 经 <see cref="ILogger.Log"/> 写一条已经成文的日志
    /// </summary>
    private static void Write(ILogger logger, LogLevel level, string message, Exception? exception = null)
        => logger.Log(level, default, message, exception, static (state, _) => state);
}
