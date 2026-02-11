#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LogFileHelperConcurrencyTests
// Guid:c89a234d-82b4-4f3e-af12-0e9f8a7b3c5d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/18 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Diagnostics;
using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Utils.Tests.Logging;

/// <summary>
/// LogFileHelper 高并发测试
/// </summary>
public class LogFileHelperConcurrencyTests : IDisposable
{
    private readonly string _testLogDirectory;
    private readonly string _originalLogDirectory;

    public LogFileHelperConcurrencyTests()
    {
        _testLogDirectory = Path.Combine(Path.GetTempPath(), "XiHanTests", "Logs", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testLogDirectory);

        // 保存原始配置以便恢复
        _originalLogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

        // 设置测试配置
        LogFileHelper.SetLogDirectory(_testLogDirectory);
        LogFileHelper.SetBufferSize(50); // 较小的缓冲区便于测试
        LogFileHelper.SetMaxFileSize(1024 * 1024); // 1MB文件大小
        LogFileHelper.SetAsyncWriteEnabled(true);
    }

    /// <summary>
    /// 测试高并发写入日志的线程安全性
    /// </summary>
    [Fact]
    public async Task ConcurrentWrites_ShouldBeThreadSafe()
    {
        // Arrange
        const int ThreadCount = 50;
        const int MessagesPerThread = 100;
        var threads = new List<Task>();
        var writtenMessages = new ConcurrentBag<string>();
        var barrier = new Barrier(ThreadCount);

        // Act
        for (var i = 0; i < ThreadCount; i++)
        {
            var threadId = i;
            var task = Task.Run(() =>
            {
                barrier.SignalAndWait(); // 确保所有线程同时开始

                for (var j = 0; j < MessagesPerThread; j++)
                {
                    var message = $"Thread-{threadId:D2}-Message-{j:D3}";
                    writtenMessages.Add(message);
                    LogFileHelper.Info(message);
                }
            });
            threads.Add(task);
        }

        await Task.WhenAll(threads);

        // 等待所有缓冲区刷新
        LogFileHelper.Flush();
        await Task.Delay(2000);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*.log");
        Assert.NotEmpty(logFiles);

        var allLogContent = string.Empty;
        foreach (var file in logFiles)
        {
            allLogContent += await File.ReadAllTextAsync(file);
        }

        // 验证所有消息都被写入
        foreach (var message in writtenMessages)
        {
            Assert.Contains(message, allLogContent);
        }

        // 验证总消息数量
        var expectedMessageCount = ThreadCount * MessagesPerThread;
        var actualMessageCount = writtenMessages.Count;
        Assert.Equal(expectedMessageCount, actualMessageCount);
    }

    /// <summary>
    /// 测试缓冲区压力情况
    /// </summary>
    [Fact]
    public async Task BufferPressure_ShouldHandleCorrectly()
    {
        // Arrange
        LogFileHelper.SetBufferSize(10); // 很小的缓冲区
        const int MessageCount = 1000;
        var tasks = new List<Task>();

        // Act
        for (var i = 0; i < MessageCount; i++)
        {
            var messageIndex = i;
            var task = Task.Run(() =>
            {
                LogFileHelper.Warn($"Pressure-Test-Message-{messageIndex:D4}");
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        LogFileHelper.Flush();
        await Task.Delay(1000);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*warn*.log");
        Assert.NotEmpty(logFiles);

        var totalLines = 0;
        foreach (var file in logFiles)
        {
            var lines = await File.ReadAllLinesAsync(file);
            totalLines += lines.Length;
        }

        Assert.Equal(MessageCount, totalLines);
    }

    /// <summary>
    /// 测试文件滚动机制在高并发下的表现
    /// </summary>
    [Fact]
    public async Task FileRollover_UnderConcurrency_ShouldWorkCorrectly()
    {
        // Arrange
        LogFileHelper.SetMaxFileSize(1024); // 1KB 文件大小，强制滚动
        const int ThreadCount = 10;
        const int MessagesPerThread = 50;
        var longMessage = new string('X', 100); // 100字符的长消息

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < ThreadCount; i++)
        {
            var threadId = i;
            var task = Task.Run(async () =>
            {
                for (var j = 0; j < MessagesPerThread; j++)
                {
                    LogFileHelper.Error($"Thread-{threadId:D2}-LongMessage-{j:D3}: {longMessage}");
                    await Task.Delay(1); // 小延迟以模拟真实场景
                }
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        LogFileHelper.Flush();
        await Task.Delay(2000);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*error*.log");
        Assert.NotEmpty(logFiles);

        // 应该有多个文件（由于文件滚动）
        Assert.True(logFiles.Length > 1, $"Expected multiple log files due to rollover, but found {logFiles.Length}");

        // 验证总消息数量
        var totalMessages = 0;
        foreach (var file in logFiles)
        {
            var lines = await File.ReadAllLinesAsync(file);
            totalMessages += lines.Length;
        }

        Assert.Equal(ThreadCount * MessagesPerThread, totalMessages);
    }

    /// <summary>
    /// 测试不同日志级别的并发写入
    /// </summary>
    [Fact]
    public async Task MixedLogLevels_ConcurrentWrites_ShouldBeHandledCorrectly()
    {
        // Arrange
        const int IterationCount = 200;
        var logLevels = new[] { LogLevel.Info, LogLevel.Warn, LogLevel.Error, LogLevel.Success, LogLevel.Handle };
        var expectedCounts = new ConcurrentDictionary<LogLevel, int>();

        foreach (var level in logLevels)
        {
            expectedCounts[level] = 0;
        }

        // Act
        var tasks = new List<Task>();
        var random = new Random();

        for (var i = 0; i < IterationCount; i++)
        {
            var iteration = i;
            var task = Task.Run(() =>
            {
                var level = logLevels[random.Next(logLevels.Length)];
                expectedCounts.AddOrUpdate(level, 1, (_, oldValue) => oldValue + 1);

                switch (level)
                {
                    case LogLevel.Info:
                        LogFileHelper.Info($"Info message {iteration}");
                        break;

                    case LogLevel.Warn:
                        LogFileHelper.Warn($"Warn message {iteration}");
                        break;

                    case LogLevel.Error:
                        LogFileHelper.Error($"Error message {iteration}");
                        break;

                    case LogLevel.Success:
                        LogFileHelper.Success($"Success message {iteration}");
                        break;

                    case LogLevel.Handle:
                        LogFileHelper.Handle($"Handle message {iteration}");
                        break;
                }
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        LogFileHelper.Flush();
        await Task.Delay(2000);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*.log");
        Assert.NotEmpty(logFiles);

        foreach (var level in logLevels)
        {
            var levelFiles = logFiles.Where(f => Path.GetFileName(f).Contains(level.ToString(), StringComparison.CurrentCultureIgnoreCase)).ToArray();

            if (expectedCounts[level] > 0)
            {
                Assert.NotEmpty(levelFiles);

                var totalLines = 0;
                foreach (var file in levelFiles)
                {
                    var lines = await File.ReadAllLinesAsync(file);
                    totalLines += lines.Length;
                }

                Assert.Equal(expectedCounts[level], totalLines);
            }
        }
    }

    /// <summary>
    /// 测试异步写入模式下的性能
    /// </summary>
    [Fact]
    public async Task AsyncWriteMode_Performance_ShouldBeEfficient()
    {
        // Arrange
        LogFileHelper.SetAsyncWriteEnabled(true);
        LogFileHelper.SetBufferSize(100);
        const int MessageCount = 5000;

        // Act
        var stopwatch = Stopwatch.StartNew();

        var tasks = new List<Task>();
        for (var i = 0; i < MessageCount; i++)
        {
            var messageIndex = i;
            var task = Task.Run(() =>
            {
                LogFileHelper.Info($"Async-Performance-Test-{messageIndex:D5}");
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        var writeTime = stopwatch.Elapsed;

        LogFileHelper.Flush();
        await Task.Delay(3000); // 等待异步写入完成
        stopwatch.Stop();

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*info*.log");
        Assert.NotEmpty(logFiles);

        var totalMessages = 0;
        foreach (var file in logFiles)
        {
            var lines = await File.ReadAllLinesAsync(file);
            totalMessages += lines.Length;
        }

        Assert.Equal(MessageCount, totalMessages);

        // 性能断言：写入应该在合理时间内完成
        var averageTimePerMessage = writeTime.TotalMilliseconds / MessageCount;
        Assert.True(averageTimePerMessage < 5, $"Average time per message ({averageTimePerMessage:F2}ms) should be less than 5ms");
    }

    /// <summary>
    /// 测试长时间运行的稳定性
    /// </summary>
    [Fact]
    public async Task LongRunning_ShouldMaintainStability()
    {
        // Arrange
        const int DurationSeconds = 10;
        const int MessagesPerSecond = 100;
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(DurationSeconds));
        var messageCount = 0;

        // Act
        var tasks = new List<Task>();

        // 启动多个写入任务
        for (var i = 0; i < 5; i++)
        {
            var taskId = i;
            var task = Task.Run(async () =>
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    LogFileHelper.Handle($"LongRunning-Task-{taskId}-Message-{Interlocked.Increment(ref messageCount)}");
                    await Task.Delay(1000 / MessagesPerSecond, cancellationTokenSource.Token);
                }
            }, cancellationTokenSource.Token);
            tasks.Add(task);
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // 预期的取消异常
        }

        LogFileHelper.Flush();
        await Task.Delay(1000);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*handle*.log");
        Assert.NotEmpty(logFiles);

        var totalLines = 0;
        foreach (var file in logFiles)
        {
            var lines = await File.ReadAllLinesAsync(file);
            totalLines += lines.Length;
        }

        // 验证消息数量在合理范围内
        var expectedMinMessages = DurationSeconds * MessagesPerSecond * 5 * 0.8; // 80%的预期量
        Assert.True(totalLines >= expectedMinMessages, $"Expected at least {expectedMinMessages} messages, but got {totalLines}");
    }

    /// <summary>
    /// 测试内存压力下的表现
    /// </summary>
    [Fact]
    public async Task MemoryPressure_ShouldHandleGracefully()
    {
        // Arrange
        LogFileHelper.SetBufferSize(500);
        const int BatchCount = 20;
        const int MessagesPerBatch = 100;
        var batches = new List<Task>();

        // Act
        for (var batch = 0; batch < BatchCount; batch++)
        {
            var batchId = batch;
            var batchTask = Task.Run(() =>
            {
                var largeDat = new string('M', 1000); // 1KB 数据

                for (var i = 0; i < MessagesPerBatch; i++)
                {
                    LogFileHelper.Success($"Batch-{batchId:D2}-Message-{i:D3}: {largeDat}");

                    // 偶尔强制垃圾回收
                    if (i % 50 == 0)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                    }
                }
            });
            batches.Add(batchTask);
        }

        await Task.WhenAll(batches);
        LogFileHelper.Flush();
        await Task.Delay(2000);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*success*.log");
        Assert.NotEmpty(logFiles);

        var totalMessages = 0;
        foreach (var file in logFiles)
        {
            var lines = await File.ReadAllLinesAsync(file);
            totalMessages += lines.Length;
        }

        Assert.Equal(BatchCount * MessagesPerBatch, totalMessages);
    }

    public void Dispose()
    {
        // 清理测试数据
        try
        {
            LogFileHelper.Flush();
            Thread.Sleep(1000); // 确保所有写入完成

            if (Directory.Exists(_testLogDirectory))
            {
                Directory.Delete(_testLogDirectory, true);
            }
        }
        catch (Exception)
        {
            // 忽略清理异常
        }

        // 恢复原始配置
        LogFileHelper.SetLogDirectory(_originalLogDirectory);
        LogFileHelper.SetBufferSize(100);
        LogFileHelper.SetMaxFileSize(10 * 1024 * 1024);
        LogFileHelper.SetAsyncWriteEnabled(true);
    }
}
