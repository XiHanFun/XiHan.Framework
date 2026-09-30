// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
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

    private static async Task RunAsync<TJob>(TJob job, CancellationToken cancellationToken)
        where TJob : class
    {
        var services = new ServiceCollection();
        services.AddSingleton(job);
        using var provider = services.BuildServiceProvider();

        var context = new BackgroundJobExecutionContext(provider, typeof(TJob), new NamedJobArgs(), cancellationToken);
        var executer = new BackgroundJobExecuter(NullLogger<BackgroundJobExecuter>.Instance);

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
}
