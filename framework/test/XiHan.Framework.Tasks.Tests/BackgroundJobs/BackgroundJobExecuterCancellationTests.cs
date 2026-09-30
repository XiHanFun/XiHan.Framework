// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs;

/// <summary>
/// 后台作业执行器取消令牌传递测试
/// </summary>
public class BackgroundJobExecuterCancellationTests
{
    /// <summary>
    /// 覆写两参数版本的处理器收到上下文的取消令牌
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ExecuteAsync_WhenHandlerOverridesTokenOverload_ReceivesContextToken()
    {
        var job = new TokenAwareJob();
        using var cts = new CancellationTokenSource();

        await RunAsync(job, cts.Token);

        Assert.Equal(cts.Token, job.ReceivedToken);
        Assert.Equal(0, job.LegacyCalls);
    }

    /// <summary>
    /// 只实现单参数版本的处理器照常执行
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ExecuteAsync_WhenHandlerOnlyImplementsLegacyOverload_ExecutesNormally()
    {
        var job = new RecordingNamedArgsJob();
        using var cts = new CancellationTokenSource();

        await RunAsync(job, cts.Token);

        Assert.Single(job.Executed);
    }

    /// <summary>
    /// 以显式接口实现两参数版本的处理器同样收到取消令牌
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ExecuteAsync_WhenHandlerImplementsTokenOverloadExplicitly_ReceivesContextToken()
    {
        var job = new ExplicitTokenJob();
        using var cts = new CancellationTokenSource();

        await RunAsync(job, cts.Token);

        Assert.Equal(cts.Token, job.ReceivedToken);
    }

    /// <summary>
    /// 上下文令牌已取消时处理器抛出的取消异常按信息级别记录，仍包装为作业执行异常外抛
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ExecuteAsync_WhenHandlerObservesCancelledContextToken_LogsInformationAndWraps()
    {
        var logger = new LevelRecordingLogger<BackgroundJobExecuter>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var exception = await Assert.ThrowsAsync<BackgroundJobExecutionException>(() => RunAsync(new CancellingJob(), cts.Token, logger));

        Assert.IsType<OperationCanceledException>(exception.InnerException, exactMatch: false);
        Assert.Contains(LogLevel.Information, logger.Entries);
        Assert.DoesNotContain(LogLevel.Error, logger.Entries);
    }

    /// <summary>
    /// 上下文令牌未取消时处理器抛出的取消异常仍按错误记录
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ExecuteAsync_WhenHandlerThrowsCancellationWithoutContextCancellation_LogsError()
    {
        var logger = new LevelRecordingLogger<BackgroundJobExecuter>();
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAsync<BackgroundJobExecutionException>(() => RunAsync(new CancellingJob(), cts.Token, logger));

        Assert.Contains(LogLevel.Error, logger.Entries);
        Assert.DoesNotContain(LogLevel.Information, logger.Entries);
    }

    private static async Task RunAsync<TJob>(TJob job, CancellationToken cancellationToken, ILogger<BackgroundJobExecuter>? logger = null)
        where TJob : class
    {
        var services = new ServiceCollection();
        services.AddSingleton(job);
        using var provider = services.BuildServiceProvider();

        var context = new BackgroundJobExecutionContext(provider, typeof(TJob), new NamedJobArgs(), cancellationToken);
        var executer = new BackgroundJobExecuter(logger ?? NullLogger<BackgroundJobExecuter>.Instance);

        await executer.ExecuteAsync(context);
    }

    private sealed class TokenAwareJob : AsyncBackgroundJob<NamedJobArgs>
    {
        public CancellationToken ReceivedToken { get; private set; }

        public int LegacyCalls { get; private set; }

        public override Task ExecuteAsync(NamedJobArgs args)
        {
            LegacyCalls++;
            return Task.CompletedTask;
        }

        public override Task ExecuteAsync(NamedJobArgs args, CancellationToken cancellationToken)
        {
            ReceivedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class ExplicitTokenJob : IAsyncBackgroundJob<NamedJobArgs>
    {
        public CancellationToken ReceivedToken { get; private set; }

        public Task ExecuteAsync(NamedJobArgs args)
        {
            return Task.CompletedTask;
        }

        Task IAsyncBackgroundJob<NamedJobArgs>.ExecuteAsync(NamedJobArgs args, CancellationToken cancellationToken)
        {
            ReceivedToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class CancellingJob : AsyncBackgroundJob<NamedJobArgs>
    {
        public override Task ExecuteAsync(NamedJobArgs args)
        {
            return Task.CompletedTask;
        }

        public override async Task ExecuteAsync(NamedJobArgs args, CancellationToken cancellationToken)
        {
            await Task.Yield();
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class LevelRecordingLogger<TCategory> : ILogger<TCategory>
    {
        private readonly object _gate = new();
        private readonly List<LogLevel> _entries = [];

        public IReadOnlyList<LogLevel> Entries
        {
            get
            {
                lock (_gate)
                {
                    return [.. _entries];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_gate)
            {
                _entries.Add(logLevel);
            }
        }
    }
}
