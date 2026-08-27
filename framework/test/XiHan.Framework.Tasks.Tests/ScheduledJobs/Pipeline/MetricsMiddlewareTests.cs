// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Executor;
using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Pipeline;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Pipeline;

/// <summary>
/// MetricsMiddleware 度量中间件测试
/// </summary>
/// <remarks>
/// 度量中间件是旁路：结果与异常都必须原样穿过。异常路径的关键契约是"先记度量再重新抛出"，
/// 否则失败的执行不会留下任何度量痕迹。
/// </remarks>
public class MetricsMiddlewareTests
{
    /// <summary>
    /// 日志器为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new MetricsMiddleware(null!));
    }

    /// <summary>
    /// 成功结果原样返回
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenDownstreamSucceeds_ReturnsSameResultInstance()
    {
        var middleware = new MetricsMiddleware(NullLogger<MetricsMiddleware>.Instance);
        var expected = JobResult.Success();

        var result = await middleware.InvokeAsync(CreateContext(), _ => Task.FromResult(expected));

        Assert.Same(expected, result);
    }

    /// <summary>
    /// 失败结果原样返回，不被改写成异常
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenDownstreamFails_ReturnsSameResultInstance()
    {
        var middleware = new MetricsMiddleware(NullLogger<MetricsMiddleware>.Instance);
        var expected = JobResult.Failure("失败原因");

        var result = await middleware.InvokeAsync(CreateContext(), _ => Task.FromResult(expected));

        Assert.Same(expected, result);
    }

    /// <summary>
    /// 下游异常在记录度量之后原样重抛
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenDownstreamThrows_RecordsMetricsThenRethrows()
    {
        var logger = new LevelRecordingLogger<MetricsMiddleware>();
        var middleware = new MetricsMiddleware(logger);
        var boom = new InvalidOperationException("下游炸了");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(CreateContext(), _ => throw boom));

        Assert.Same(boom, thrown);
        Assert.Contains(LogLevel.Debug, logger.Entries);
    }

    /// <summary>
    /// 正常路径也会写一条度量日志
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenDownstreamSucceeds_RecordsMetricsOnce()
    {
        var logger = new LevelRecordingLogger<MetricsMiddleware>();
        var middleware = new MetricsMiddleware(logger);

        await middleware.InvokeAsync(CreateContext(), _ => Task.FromResult(JobResult.Success()));

        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, logger.Entries[0]);
    }

    /// <summary>
    /// 构造一个执行上下文
    /// </summary>
    private static JobExecutionContext CreateContext()
    {
        var jobInfo = new JobInfo
        {
            JobName = "metrics-job",
            JobType = typeof(MetricsMiddlewareTests),
            TriggerType = JobTriggerType.Manual
        };

        var instance = new JobInstance
        {
            JobName = jobInfo.JobName,
            JobInfo = jobInfo,
            TriggerType = JobTriggerType.Manual
        };

        return new JobExecutionContext(instance, null, new ServiceCollection().BuildServiceProvider());
    }

    /// <summary>
    /// 只记录日志级别的手写日志器
    /// </summary>
    private sealed class LevelRecordingLogger<TCategory> : ILogger<TCategory>
    {
        /// <summary>
        /// 已记录的日志级别
        /// </summary>
        public List<LogLevel> Entries { get; } = [];

        /// <summary>
        /// 开始日志作用域
        /// </summary>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        /// <summary>
        /// 是否启用指定级别
        /// </summary>
        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        /// <summary>
        /// 写日志
        /// </summary>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(logLevel);
        }
    }
}
