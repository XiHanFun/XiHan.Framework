// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using XiHan.Framework.Caching.Distributed;
using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.Workflow.SqlSugar.Extensions.DependencyInjection;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 进程内分布式锁的启动警告测试
/// </summary>
public class DistributedLockWarningTests
{
    /// <summary>
    /// 默认进程内锁记录一条警告
    /// </summary>
    [Fact]
    public void 默认进程内锁记录一条警告()
    {
        var logs = new CapturingLoggerProvider();
        using var provider = BuildProvider(logs, services => services.AddSingleton<IDistributedLock>(new DefaultDistributedLock()));

        Assert.True(provider.WarnIfWorkflowLockIsProcessLocal());

        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(nameof(DefaultDistributedLock), entry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 其他锁实现不记录警告
    /// </summary>
    [Fact]
    public void 其他锁实现不记录警告()
    {
        var logs = new CapturingLoggerProvider();
        using var provider = BuildProvider(logs, services => services.AddSingleton<IDistributedLock>(new InProcessTestLock()));

        Assert.False(provider.WarnIfWorkflowLockIsProcessLocal());
        Assert.Empty(logs.Entries);
    }

    /// <summary>
    /// 未注册锁时不记录警告
    /// </summary>
    [Fact]
    public void 未注册锁时不记录警告()
    {
        var logs = new CapturingLoggerProvider();
        using var provider = BuildProvider(logs, _ => { });

        Assert.False(provider.WarnIfWorkflowLockIsProcessLocal());
        Assert.Empty(logs.Entries);
    }

    private static ServiceProvider BuildProvider(CapturingLoggerProvider logs, Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(logs));
        configure(services);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 记录全部日志条目的日志提供程序
    /// </summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

        /// <summary>
        /// 已记录的日志条目
        /// </summary>
        public IReadOnlyList<(LogLevel Level, string Message)> Entries => [.. _entries];

        public ILogger CreateLogger(string categoryName)
        {
            return new CapturingLogger(_entries);
        }

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries;

        public CapturingLogger(ConcurrentQueue<(LogLevel Level, string Message)> entries)
        {
            _entries = entries;
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
            _entries.Enqueue((logLevel, formatter(state, exception)));
        }
    }
}
