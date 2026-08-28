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
/// LoggingMiddleware 日志中间件测试
/// </summary>
/// <remarks>
/// 日志中间件必须是"透明"的：结果原样返回、异常原样抛出，只在旁路写日志。
/// 用手写的记录型日志器断言成功走 Information、失败走 Warning、异常走 Error 这三档分流。
/// </remarks>
public class LoggingMiddlewareTests
{
    /// <summary>
    /// 日志器为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new LoggingMiddleware(null!));
    }

    /// <summary>
    /// 成功结果原样返回，不被中间件替换
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenDownstreamSucceeds_ReturnsSameResultInstance()
    {
        var middleware = new LoggingMiddleware(NullLogger<LoggingMiddleware>.Instance);
        var expected = JobResult.Success("payload");

        var result = await middleware.InvokeAsync(CreateContext(), _ => Task.FromResult(expected));

        Assert.Same(expected, result);
    }

    /// <summary>
    /// 失败结果同样原样返回，中间件不做降级或改写
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenDownstreamFails_ReturnsSameResultInstance()
    {
        var middleware = new LoggingMiddleware(NullLogger<LoggingMiddleware>.Instance);
        var expected = JobResult.Failure("失败原因");

        var result = await middleware.InvokeAsync(CreateContext(), _ => Task.FromResult(expected));

        Assert.Same(expected, result);
    }

    /// <summary>
    /// 下游异常继续向上抛出，不被吞成失败结果
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenDownstreamThrows_RethrowsSameException()
    {
        var middleware = new LoggingMiddleware(NullLogger<LoggingMiddleware>.Instance);
        var boom = new InvalidOperationException("下游炸了");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(CreateContext(), _ => throw boom));

        Assert.Same(boom, thrown);
    }

    /// <summary>
    /// 成功执行时记录开始与成功两条信息级日志
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenDownstreamSucceeds_LogsInformationOnly()
    {
        var logger = new RecordingLogger<LoggingMiddleware>();
        var middleware = new LoggingMiddleware(logger);

        await middleware.InvokeAsync(CreateContext(), _ => Task.FromResult(JobResult.Success()));

        Assert.Equal(2, logger.Entries.Count);
        Assert.All(logger.Entries, level => Assert.Equal(LogLevel.Information, level));
    }

    /// <summary>
    /// 失败执行时降级为警告级日志
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenDownstreamFails_LogsWarning()
    {
        var logger = new RecordingLogger<LoggingMiddleware>();
        var middleware = new LoggingMiddleware(logger);

        await middleware.InvokeAsync(CreateContext(), _ => Task.FromResult(JobResult.Failure("失败原因")));

        Assert.Contains(LogLevel.Warning, logger.Entries);
    }

    /// <summary>
    /// 异常执行时记录错误级日志
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenDownstreamThrows_LogsError()
    {
        var logger = new RecordingLogger<LoggingMiddleware>();
        var middleware = new LoggingMiddleware(logger);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(CreateContext(), _ => throw new InvalidOperationException("炸了")));

        Assert.Contains(LogLevel.Error, logger.Entries);
    }

    /// <summary>
    /// 构造一个执行上下文
    /// </summary>
    private static JobExecutionContext CreateContext()
    {
        var jobInfo = new JobInfo
        {
            JobName = "logging-job",
            JobType = typeof(LoggingMiddlewareTests),
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
    private sealed class RecordingLogger<TCategory> : ILogger<TCategory>
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
