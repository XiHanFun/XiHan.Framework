#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LogHelperConcurrencyTests
// Guid:b78e123f-91c3-4e2f-ba67-1d8a9b6c4e5f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/18 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
        const int threadCount = 20;
        const int messagesPerThread = 50;
        var completedTasks = 0;
        var exceptions = new ConcurrentBag<Exception>();

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < threadCount; i++)
        {
            var threadId = i;
            var task = Task.Run(() =>
            {
                try
                {
                    for (var j = 0; j < messagesPerThread; j++)
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
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(threadCount, completedTasks);
    }

    /// <summary>
    /// 测试格式化消息的并发处理
    /// </summary>
    [Fact]
    public async Task ConcurrentFormattedMessages_ShouldHandleCorrectly()
    {
        // Arrange
        const int messageCount = 1000;
        var exceptions = new ConcurrentBag<Exception>();
        var processedMessages = 0;

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < messageCount; i++)
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
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(messageCount, processedMessages);
    }

    /// <summary>
    /// 测试彩虹模式输出的并发性能
    /// </summary>
    [Fact]
    public async Task ConcurrentRainbowOutput_ShouldBeStable()
    {
        // Arrange
        const int threadCount = 10;
        const int messagesPerThread = 20;
        var exceptions = new ConcurrentBag<Exception>();
        var completedTasks = 0;

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < threadCount; i++)
        {
            var threadId = i;
            var task = Task.Run(() =>
            {
                try
                {
                    for (var j = 0; j < messagesPerThread; j++)
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
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(threadCount, completedTasks);
    }

    /// <summary>
    /// 测试表格输出的并发性能
    /// </summary>
    [Fact]
    public async Task ConcurrentTableOutput_ShouldBeThreadSafe()
    {
        // Arrange
        const int tableCount = 50;
        var exceptions = new ConcurrentBag<Exception>();
        var processedTables = 0;

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < tableCount; i++)
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
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(tableCount, processedTables);
    }

    /// <summary>
    /// 测试日志级别过滤的并发性能
    /// </summary>
    [Fact]
    public async Task ConcurrentLogLevelFiltering_ShouldWorkCorrectly()
    {
        // Arrange
        LogHelper.SetMinimumLevel(LogLevel.Warn); // 只输出警告及以上级别
        const int messageCount = 500;
        var exceptions = new ConcurrentBag<Exception>();
        var processedMessages = 0;

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < messageCount; i++)
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
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(messageCount, processedMessages);

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
        const int taskCount = 100;
        var exceptions = new ConcurrentBag<Exception>();
        var processedTasks = 0;

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < taskCount; i++)
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
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(taskCount, processedTasks);
    }

    /// <summary>
    /// 测试设置更改的线程安全性
    /// </summary>
    [Fact]
    public async Task ConcurrentSettingsChanges_ShouldBeThreadSafe()
    {
        // Arrange
        const int changeCount = 100;
        var exceptions = new ConcurrentBag<Exception>();
        var completedChanges = 0;

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < changeCount; i++)
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
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(changeCount, completedChanges);
    }

    /// <summary>
    /// 测试控制台清除的并发安全性
    /// </summary>
    [Fact]
    public async Task ConcurrentConsoleClear_ShouldBeStable()
    {
        // Arrange
        const int operationCount = 50;
        var exceptions = new ConcurrentBag<Exception>();
        var completedOperations = 0;

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < operationCount; i++)
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
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(operationCount, completedOperations);
    }

    /// <summary>
    /// 压力测试：大量快速日志输出
    /// </summary>
    [Fact]
    public async Task HighVolumeOutput_ShouldMaintainStability()
    {
        // Arrange
        const int threadCount = 25;
        const int messagesPerThread = 200;
        var exceptions = new ConcurrentBag<Exception>();
        var totalMessages = 0;

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < threadCount; i++)
        {
            var threadId = i;
            var task = Task.Run(() =>
            {
                try
                {
                    for (var j = 0; j < messagesPerThread; j++)
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
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(threadCount * messagesPerThread, totalMessages);
    }
}
