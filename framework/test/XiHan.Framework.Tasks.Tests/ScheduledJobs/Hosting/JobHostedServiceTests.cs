// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Hosting;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Hosting;

/// <summary>
/// JobHostedService 托管服务测试
/// </summary>
/// <remarks>
/// 托管服务只做转发与日志，关键契约是"启停失败必须重抛"——若被吞掉，主机会以为调度器已就绪，
/// 任务将静默不执行。
/// </remarks>
public class JobHostedServiceTests
{
    /// <summary>
    /// 调度器为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_WhenSchedulerIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new JobHostedService(null!, NullLogger<JobHostedService>.Instance));
    }

    /// <summary>
    /// 日志器为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new JobHostedService(new RecordingScheduler(), null!));
    }

    /// <summary>
    /// 是标准的托管服务，可被主机生命周期接管
    /// </summary>
    [Fact]
    public void Instance_IsHostedService()
    {
        var service = new JobHostedService(new RecordingScheduler(), NullLogger<JobHostedService>.Instance);

        Assert.IsAssignableFrom<IHostedService>(service);
    }

    /// <summary>
    /// 启动时转发到调度器，并透传取消令牌
    /// </summary>
    [Fact]
    public async Task StartAsync_ForwardsToSchedulerWithSameToken()
    {
        var scheduler = new RecordingScheduler();
        var service = new JobHostedService(scheduler, NullLogger<JobHostedService>.Instance);
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);

        Assert.Equal(1, scheduler.StartCount);
        Assert.Equal(0, scheduler.StopCount);
        Assert.Equal(cts.Token, scheduler.LastStartToken);
    }

    /// <summary>
    /// 停止时转发到调度器，并透传取消令牌
    /// </summary>
    [Fact]
    public async Task StopAsync_ForwardsToSchedulerWithSameToken()
    {
        var scheduler = new RecordingScheduler();
        var service = new JobHostedService(scheduler, NullLogger<JobHostedService>.Instance);
        using var cts = new CancellationTokenSource();

        await service.StopAsync(cts.Token);

        Assert.Equal(1, scheduler.StopCount);
        Assert.Equal(0, scheduler.StartCount);
        Assert.Equal(cts.Token, scheduler.LastStopToken);
    }

    /// <summary>
    /// 调度器启动失败时异常必须重抛，让主机启动失败而不是假装成功
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenSchedulerThrows_RethrowsException()
    {
        var scheduler = new RecordingScheduler { ThrowOnStart = true };
        var service = new JobHostedService(scheduler, NullLogger<JobHostedService>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("启动失败", exception.Message);
    }

    /// <summary>
    /// 调度器停止失败时异常同样重抛
    /// </summary>
    [Fact]
    public async Task StopAsync_WhenSchedulerThrows_RethrowsException()
    {
        var scheduler = new RecordingScheduler { ThrowOnStop = true };
        var service = new JobHostedService(scheduler, NullLogger<JobHostedService>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StopAsync(TestContext.Current.CancellationToken));

        Assert.Equal("停止失败", exception.Message);
    }

    /// <summary>
    /// 完整的启动—停止周期各转发一次
    /// </summary>
    [Fact]
    public async Task StartThenStop_ForwardsBothCallsOnce()
    {
        var scheduler = new RecordingScheduler();
        var service = new JobHostedService(scheduler, NullLogger<JobHostedService>.Instance);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, scheduler.StartCount);
        Assert.Equal(1, scheduler.StopCount);
    }

    /// <summary>
    /// 记录启停调用的假调度器
    /// </summary>
    private sealed class RecordingScheduler : IJobScheduler
    {
        /// <summary>
        /// 启动时是否抛异常
        /// </summary>
        public bool ThrowOnStart { get; init; }

        /// <summary>
        /// 停止时是否抛异常
        /// </summary>
        public bool ThrowOnStop { get; init; }

        /// <summary>
        /// 启动被调用的次数
        /// </summary>
        public int StartCount { get; private set; }

        /// <summary>
        /// 停止被调用的次数
        /// </summary>
        public int StopCount { get; private set; }

        /// <summary>
        /// 最近一次启动的取消令牌
        /// </summary>
        public CancellationToken LastStartToken { get; private set; }

        /// <summary>
        /// 最近一次停止的取消令牌
        /// </summary>
        public CancellationToken LastStopToken { get; private set; }

        /// <summary>
        /// 注册任务
        /// </summary>
        public void RegisterJob(JobInfo jobInfo)
        {
        }

        /// <summary>
        /// 取消注册任务
        /// </summary>
        public void UnregisterJob(string jobName)
        {
        }

        /// <summary>
        /// 暂停任务
        /// </summary>
        public void PauseJob(string jobName)
        {
        }

        /// <summary>
        /// 恢复任务
        /// </summary>
        public void ResumeJob(string jobName)
        {
        }

        /// <summary>
        /// 手动触发任务
        /// </summary>
        public Task<string> TriggerJobAsync(string jobName, IDictionary<string, object?>? parameters = null)
        {
            return Task.FromResult(string.Empty);
        }

        /// <summary>
        /// 获取下次执行时间
        /// </summary>
        public DateTimeOffset? GetNextFireTime(string jobName)
        {
            return null;
        }

        /// <summary>
        /// 获取所有已注册的任务信息
        /// </summary>
        public IReadOnlyList<JobInfo> GetAllJobs()
        {
            return [];
        }

        /// <summary>
        /// 启动调度器
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            LastStartToken = cancellationToken;

            return ThrowOnStart
                ? throw new InvalidOperationException("启动失败")
                : Task.CompletedTask;
        }

        /// <summary>
        /// 停止调度器
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            LastStopToken = cancellationToken;

            return ThrowOnStop
                ? throw new InvalidOperationException("停止失败")
                : Task.CompletedTask;
        }
    }
}
