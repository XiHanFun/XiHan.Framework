// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using XiHan.Framework.Utils.ConsoleTools;
using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Utils.Tests.Logging;

/// <summary>
/// LogHelper 高并发测试
/// </summary>
public class LogHelperConcurrencyTests
{
    private readonly LogLevel _originalLogLevel;

    public LogHelperConcurrencyTests()
    {
        _originalLogLevel = LogHelper.GetMinimumLevel();
        LogHelper.SetMinimumLevel(LogLevel.Info);
    }

    /// <summary>
    /// 测试控制台输出的线程安全性
    /// </summary>
    [Fact]
    public async Task ConcurrentConsoleOutput_ShouldBeThreadSafe()
    {
        // Arrange
        const int ThreadCount = 20;
        const int MessagesPerThread = 50;
        var completedTasks = 0;
        var exceptions = new ConcurrentBag<Exception>();

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < ThreadCount; i++)
        {
            var threadId = i;
            var task = Task.Run(() =>
            {
                try
                {
                    for (var j = 0; j < MessagesPerThread; j++)
                    {
                        LogHelper.Info($"Thread-{threadId:D2} Message-{j:D3}");
                        LogHelper.Warn($"Thread-{threadId:D2} Warning-{j:D3}");
                        LogHelper.Error($"Thread-{threadId:D2} Error-{j:D3}");
                    }
                    Interlocked.Increment(ref completedTasks);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }, TestContext.Current.CancellationToken);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(ThreadCount, completedTasks);
    }

    /// <summary>
    /// 测试格式化消息的并发处理
    /// </summary>
    [Fact]
    public async Task ConcurrentFormattedMessages_ShouldHandleCorrectly()
    {
        // Arrange
        const int MessageCount = 1000;
        var exceptions = new ConcurrentBag<Exception>();
        var processedMessages = 0;

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < MessageCount; i++)
        {
            var messageIndex = i;
            var task = Task.Run(() =>
            {
                try
                {
                    LogHelper.Info("Formatted message {0} with value {1}", messageIndex, DateTime.Now);
                    LogHelper.Success("Success for item {0}", messageIndex);
                    LogHelper.Handle("Handling request {0} at {1}", messageIndex, DateTime.UtcNow);
                    Interlocked.Increment(ref processedMessages);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }, TestContext.Current.CancellationToken);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(MessageCount, processedMessages);
    }

    /// <summary>
    /// 测试彩虹模式输出的并发性能
    /// </summary>
    [Fact]
    public async Task ConcurrentRainbowOutput_ShouldBeStable()
    {
        // Arrange
        const int ThreadCount = 10;
        const int MessagesPerThread = 20;
        var exceptions = new ConcurrentBag<Exception>();
        var completedTasks = 0;

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < ThreadCount; i++)
        {
            var threadId = i;
            var task = Task.Run(() =>
            {
                try
                {
                    for (var j = 0; j < MessagesPerThread; j++)
                    {
                        LogHelper.Rainbow($"🌈 Rainbow Message from Thread-{threadId:D2} #{j:D3}");
                        Thread.Sleep(10); // 小延迟模拟真实场景
                    }
                    Interlocked.Increment(ref completedTasks);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }, TestContext.Current.CancellationToken);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(ThreadCount, completedTasks);
    }

    /// <summary>
    /// 测试表格输出的并发性能
    /// </summary>
    [Fact]
    public async Task ConcurrentTableOutput_ShouldBeThreadSafe()
    {
        // Arrange
        const int TableCount = 50;
        var exceptions = new ConcurrentBag<Exception>();
        var processedTables = 0;

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < TableCount; i++)
        {
            var tableIndex = i;
            var task = Task.Run(() =>
            {
                try
                {
                    var table = new ConsoleTable("Id", "Name", "Value", "Timestamp");
                    for (var row = 0; row < 5; row++)
                    {
                        table.AddRow(
                            $"{tableIndex}-{row}",
                            $"Item-{row}",
                            Random.Shared.Next(1000, 9999),
                            DateTime.Now.ToString("HH:mm:ss.fff")
                        );
                    }

                    LogHelper.InfoTable(table);
                    Interlocked.Increment(ref processedTables);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }, TestContext.Current.CancellationToken);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(TableCount, processedTables);
    }

    /// <summary>
    /// 测试日志级别过滤的并发性能
    /// </summary>
    [Fact]
    public async Task ConcurrentLogLevelFiltering_ShouldWorkCorrectly()
    {
        // Arrange
        LogHelper.SetMinimumLevel(LogLevel.Warn); // 只输出警告及以上级别
        const int MessageCount = 500;
        var exceptions = new ConcurrentBag<Exception>();
        var processedMessages = 0;

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < MessageCount; i++)
        {
            var messageIndex = i;
            var task = Task.Run(() =>
            {
                try
                {
                    // 这些消息应该被过滤掉
                    LogHelper.Info($"Info message {messageIndex} - should be filtered");
                    LogHelper.Success($"Success message {messageIndex} - should be filtered");
                    LogHelper.Handle($"Handle message {messageIndex} - should be filtered");

                    // 这些消息应该被输出
                    LogHelper.Warn($"Warning message {messageIndex} - should be shown");
                    LogHelper.Error($"Error message {messageIndex} - should be shown");

                    Interlocked.Increment(ref processedMessages);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }, TestContext.Current.CancellationToken);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(MessageCount, processedMessages);

        // 恢复日志级别
        LogHelper.SetMinimumLevel(LogLevel.Info);
    }

    /// <summary>
    /// 测试异常处理的并发安全性
    /// </summary>
    [Fact]
    public async Task ConcurrentExceptionHandling_ShouldBeRobust()
    {
        // Arrange
        const int TaskCount = 100;
        var exceptions = new ConcurrentBag<Exception>();
        var processedTasks = 0;

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < TaskCount; i++)
        {
            var taskIndex = i;
            var task = Task.Run(() =>
            {
                try
                {
                    // 模拟各种可能的异常情况
                    var testException = new InvalidOperationException($"Test exception {taskIndex}");

                    LogHelper.Error($"Exception occurred in task {taskIndex}", testException);

                    // 测试格式化字符串可能的问题
                    LogHelper.Warn("Task {0} with malformed {1} string", taskIndex); // 缺少一个参数

                    // 测试 null 值
                    string? nullValue = null;
                    LogHelper.Info($"Task {taskIndex} with null value: {nullValue}");

                    Interlocked.Increment(ref processedTasks);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }, TestContext.Current.CancellationToken);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(TaskCount, processedTasks);
    }

    /// <summary>
    /// 测试设置更改的线程安全性
    /// </summary>
    [Fact]
    public async Task ConcurrentSettingsChanges_ShouldBeThreadSafe()
    {
        // Arrange
        const int ChangeCount = 100;
        var exceptions = new ConcurrentBag<Exception>();
        var completedChanges = 0;

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < ChangeCount; i++)
        {
            var changeIndex = i;
            var task = Task.Run(() =>
            {
                try
                {
                    // 随机更改设置
                    var random = new Random();
                    var levels = new[] { LogLevel.Error, LogLevel.Warn, LogLevel.Handle, LogLevel.Success, LogLevel.Info };
                    var selectedLevel = levels[random.Next(levels.Length)];

                    LogHelper.SetMinimumLevel(selectedLevel);
                    LogHelper.SetIsDisplayHeader(random.Next(2) == 0);

                    // 测试当前设置
                    LogHelper.Info($"Change {changeIndex}: Level={selectedLevel}, Header={LogHelper.GetIsDisplayHeader()}");

                    Interlocked.Increment(ref completedChanges);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }, TestContext.Current.CancellationToken);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(ChangeCount, completedChanges);
    }

    /// <summary>
    /// 测试控制台清除的并发安全性
    /// </summary>
    [Fact]
    public async Task ConcurrentConsoleClear_ShouldBeStable()
    {
        // Arrange
        const int OperationCount = 50;
        var exceptions = new ConcurrentBag<Exception>();
        var completedOperations = 0;

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < OperationCount; i++)
        {
            var operationIndex = i;
            var task = Task.Run(() =>
            {
                try
                {
                    // 输出一些内容
                    LogHelper.Info($"Before clear operation {operationIndex}");
                    LogHelper.Success($"Success message {operationIndex}");

                    // 清除控制台
                    LogHelper.Clear();

                    // 再输出一些内容
                    LogHelper.Warn($"After clear operation {operationIndex}");

                    Interlocked.Increment(ref completedOperations);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }, TestContext.Current.CancellationToken);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(OperationCount, completedOperations);
    }

    /// <summary>
    /// 压力测试：大量快速日志输出
    /// </summary>
    [Fact]
    public async Task HighVolumeOutput_ShouldMaintainStability()
    {
        // Arrange
        const int ThreadCount = 25;
        const int MessagesPerThread = 200;
        var exceptions = new ConcurrentBag<Exception>();
        var totalMessages = 0;

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < ThreadCount; i++)
        {
            var threadId = i;
            var task = Task.Run(() =>
            {
                try
                {
                    for (var j = 0; j < MessagesPerThread; j++)
                    {
                        var logLevel = (LogLevel)((j % 5) + 1); // 循环使用不同的日志级别

                        switch (logLevel)
                        {
                            case LogLevel.Info:
                                LogHelper.Info($"High-Volume Thread-{threadId:D2} Info-{j:D3}");
                                break;

                            case LogLevel.Success:
                                LogHelper.Success($"High-Volume Thread-{threadId:D2} Success-{j:D3}");
                                break;

                            case LogLevel.Handle:
                                LogHelper.Handle($"High-Volume Thread-{threadId:D2} Handle-{j:D3}");
                                break;

                            case LogLevel.Warn:
                                LogHelper.Warn($"High-Volume Thread-{threadId:D2} Warn-{j:D3}");
                                break;

                            case LogLevel.Error:
                                LogHelper.Error($"High-Volume Thread-{threadId:D2} Error-{j:D3}");
                                break;
                        }

                        Interlocked.Increment(ref totalMessages);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }, TestContext.Current.CancellationToken);
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(ThreadCount * MessagesPerThread, totalMessages);
    }
}
