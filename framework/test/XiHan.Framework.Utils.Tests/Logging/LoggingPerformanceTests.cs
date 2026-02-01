#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LoggingPerformanceTests
// Guid:d34f567a-42e8-4b9c-8a12-3f7e9d8c5b6a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/18 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;
using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Utils.Tests.Logging;

/// <summary>
/// Logging 模块性能基准测试
/// </summary>
public class LoggingPerformanceTests : IDisposable
{
    private readonly string _testLogDirectory;

    public LoggingPerformanceTests()
    {
        _testLogDirectory = Path.Combine(Path.GetTempPath(), "XiHanTests", "Performance", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testLogDirectory);

        // 设置测试配置
        LogFileHelper.SetLogDirectory(_testLogDirectory);
        LogFileHelper.SetBufferSize(100);
        LogFileHelper.SetMaxFileSize(5 * 1024 * 1024); // 5MB
        LogFileHelper.SetAsyncWriteEnabled(true);
    }

    /// <summary>
    /// 基准测试：单线程写入性能
    /// </summary>
    [Fact]
    public void Benchmark_SingleThreadWrite_Performance()
    {
        // Arrange
        const int MessageCount = 10000;
        var message = "Performance test message with some data to simulate real log entries";

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();
        var startMemory = GC.GetTotalMemory(true);

        for (var i = 0; i < MessageCount; i++)
        {
            LogFileHelper.Info($"{message} #{i:D5}");
        }

        var writeTime = stopwatch.Elapsed;
        LogFileHelper.Flush();
        stopwatch.Stop();

        var endMemory = GC.GetTotalMemory(false);
        var totalTime = stopwatch.Elapsed;

        // Assert & Report
        var messagesPerSecond = MessageCount / totalTime.TotalSeconds;
        var averageTimePerMessage = totalTime.TotalMicroseconds / MessageCount;
        var memoryUsed = endMemory - startMemory;

        Assert.True(messagesPerSecond > 1000, $"Performance too low: {messagesPerSecond:F0} messages/second");
        Assert.True(averageTimePerMessage < 1000, $"Average time too high: {averageTimePerMessage:F2} microseconds/message");

        // 输出性能报告
        Console.WriteLine($"=== Single Thread Write Performance ===");
        Console.WriteLine($"Messages: {MessageCount:N0}");
        Console.WriteLine($"Write Time: {writeTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Total Time: {totalTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Throughput: {messagesPerSecond:F0} messages/second");
        Console.WriteLine($"Average Time: {averageTimePerMessage:F2} μs/message");
        Console.WriteLine($"Memory Used: {memoryUsed / 1024.0:F2} KB");
    }

    /// <summary>
    /// 基准测试：多线程写入性能
    /// </summary>
    [Fact]
    public async Task Benchmark_MultiThreadWrite_Performance()
    {
        // Arrange
        var threadCount = Environment.ProcessorCount;
        var messagesPerThread = 2000;
        var totalMessages = threadCount * messagesPerThread;
        var message = "Multi-thread performance test message";

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();
        var startMemory = GC.GetTotalMemory(true);

        var tasks = new List<Task>();
        for (var i = 0; i < threadCount; i++)
        {
            var threadId = i;
            var task = Task.Run(() =>
            {
                for (var j = 0; j < messagesPerThread; j++)
                {
                    LogFileHelper.Warn($"Thread-{threadId:D2} {message} #{j:D4}");
                }
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        var writeTime = stopwatch.Elapsed;

        LogFileHelper.Flush();
        await Task.Delay(1000); // 等待异步写入完成
        stopwatch.Stop();

        var endMemory = GC.GetTotalMemory(false);
        var totalTime = stopwatch.Elapsed;

        // Assert & Report
        var messagesPerSecond = totalMessages / totalTime.TotalSeconds;
        var averageTimePerMessage = totalTime.TotalMicroseconds / totalMessages;
        var memoryUsed = endMemory - startMemory;

        Assert.True(messagesPerSecond > 2000, $"Multi-thread performance too low: {messagesPerSecond:F0} messages/second");

        Console.WriteLine($"=== Multi Thread Write Performance ===");
        Console.WriteLine($"Threads: {threadCount}");
        Console.WriteLine($"Messages per Thread: {messagesPerThread:N0}");
        Console.WriteLine($"Total Messages: {totalMessages:N0}");
        Console.WriteLine($"Write Time: {writeTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Total Time: {totalTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Throughput: {messagesPerSecond:F0} messages/second");
        Console.WriteLine($"Average Time: {averageTimePerMessage:F2} μs/message");
        Console.WriteLine($"Memory Used: {memoryUsed / 1024.0:F2} KB");
    }

    /// <summary>
    /// 基准测试：缓冲区效率
    /// </summary>
    [Fact]
    public void Benchmark_BufferEfficiency()
    {
        // 测试不同缓冲区大小的性能影响
        var bufferSizes = new[] { 1, 10, 50, 100, 500 };
        var results = new Dictionary<int, (double throughput, double latency)>();

        foreach (var bufferSize in bufferSizes)
        {
            // 重置配置
            LogFileHelper.SetBufferSize(bufferSize);
            LogFileHelper.Clear();

            const int MessageCount = 5000;
            var message = $"Buffer test message for size {bufferSize}";

            var stopwatch = Stopwatch.StartNew();

            for (var i = 0; i < MessageCount; i++)
            {
                LogFileHelper.Error($"{message} #{i:D4}");
            }

            LogFileHelper.Flush();
            stopwatch.Stop();

            var throughput = MessageCount / stopwatch.Elapsed.TotalSeconds;
            var latency = stopwatch.Elapsed.TotalMicroseconds / MessageCount;

            results[bufferSize] = (throughput, latency);

            Console.WriteLine($"Buffer Size {bufferSize,3}: {throughput:F0} msg/s, {latency:F2} μs/msg");
        }

        // 验证较大的缓冲区通常有更好的性能
        var smallBufferThroughput = results[1].throughput;
        var largeBufferThroughput = results[500].throughput;

        Assert.True(largeBufferThroughput >= smallBufferThroughput * 0.8,
            "Large buffer should have competitive performance");
    }

    /// <summary>
    /// 基准测试：异步vs同步写入性能对比
    /// </summary>
    [Fact]
    public async Task Benchmark_AsyncVsSyncWrite()
    {
        const int MessageCount = 8000;
        var message = "Async vs Sync performance comparison test";

        // 测试同步写入
        LogFileHelper.SetAsyncWriteEnabled(false);
        LogFileHelper.Clear();

        var syncStopwatch = Stopwatch.StartNew();
        for (var i = 0; i < MessageCount; i++)
        {
            LogFileHelper.Success($"SYNC {message} #{i:D4}");
        }
        LogFileHelper.Flush();
        syncStopwatch.Stop();

        var syncThroughput = MessageCount / syncStopwatch.Elapsed.TotalSeconds;

        // 测试异步写入
        LogFileHelper.SetAsyncWriteEnabled(true);
        LogFileHelper.Clear();

        var asyncStopwatch = Stopwatch.StartNew();
        for (var i = 0; i < MessageCount; i++)
        {
            LogFileHelper.Success($"ASYNC {message} #{i:D4}");
        }
        LogFileHelper.Flush();
        await Task.Delay(2000); // 等待异步写入完成
        asyncStopwatch.Stop();

        var asyncThroughput = MessageCount / asyncStopwatch.Elapsed.TotalSeconds;

        Console.WriteLine($"=== Async vs Sync Performance ===");
        Console.WriteLine($"Sync Throughput:  {syncThroughput:F0} messages/second");
        Console.WriteLine($"Async Throughput: {asyncThroughput:F0} messages/second");
        Console.WriteLine($"Performance Ratio: {asyncThroughput / syncThroughput:F2}x");

        // 异步写入应该有相似或更好的性能
        Assert.True(asyncThroughput >= syncThroughput * 0.7,
            "Async write should maintain competitive performance");
    }

    /// <summary>
    /// 压力测试：大数据量写入
    /// </summary>
    [Fact]
    public async Task StressTest_LargeVolumeWrite()
    {
        // Arrange
        const int TotalMessages = 50000;
        const int ThreadCount = 10;
        const int MessagesPerThread = TotalMessages / ThreadCount;
        var largeMessage = new string('X', 500); // 500字符消息

        // 监控资源使用
        var initialMemory = GC.GetTotalMemory(true);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < ThreadCount; i++)
        {
            var threadId = i;
            var task = Task.Run(() =>
            {
                for (var j = 0; j < MessagesPerThread; j++)
                {
                    LogFileHelper.Handle($"STRESS Thread-{threadId:D2} #{j:D5}: {largeMessage}");

                    // 偶尔休息一下，模拟真实应用
                    if (j % 1000 == 0)
                    {
                        Thread.Sleep(1);
                    }
                }
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        LogFileHelper.Flush();
        await Task.Delay(3000); // 等待所有异步操作完成

        stopwatch.Stop();
        var finalMemory = GC.GetTotalMemory(false);

        // Assert
        var throughput = TotalMessages / stopwatch.Elapsed.TotalSeconds;
        var memoryIncrease = (finalMemory - initialMemory) / 1024.0 / 1024.0; // MB

        Console.WriteLine($"=== Large Volume Stress Test ===");
        Console.WriteLine($"Total Messages: {TotalMessages:N0}");
        Console.WriteLine($"Message Size: 500 characters");
        Console.WriteLine($"Threads: {ThreadCount}");
        Console.WriteLine($"Duration: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($"Throughput: {throughput:F0} messages/second");
        Console.WriteLine($"Memory Increase: {memoryIncrease:F2} MB");

        // 验证日志文件
        var logFiles = Directory.GetFiles(_testLogDirectory, "*handle*.log");
        Assert.NotEmpty(logFiles);

        var totalLoggedMessages = 0;
        foreach (var file in logFiles)
        {
            var lines = await File.ReadAllLinesAsync(file);
            totalLoggedMessages += lines.Length;
        }

        Assert.Equal(TotalMessages, totalLoggedMessages);
        Assert.True(throughput > 500, "Throughput should be reasonable for large volume");
        Assert.True(memoryIncrease < 100, "Memory increase should be controlled");
    }

    /// <summary>
    /// 耐久性测试：长时间连续写入
    /// </summary>
    [Fact]
    public async Task EnduranceTest_ContinuousWrite()
    {
        // Arrange
        const int DurationMinutes = 2; // 2分钟测试
        const int MessagesPerSecond = 50;
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(DurationMinutes));

        var messageCount = 0;
        var errorCount = 0;
        var startMemory = GC.GetTotalMemory(true);

        // Act
        var tasks = new List<Task>();

        // 启动多个持续写入任务
        for (var i = 0; i < 3; i++)
        {
            var taskId = i;
            var task = Task.Run(async () =>
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        LogFileHelper.Info($"Endurance Task-{taskId} Message-{Interlocked.Increment(ref messageCount)} at {DateTime.Now:HH:mm:ss.fff}");
                        await Task.Delay(1000 / MessagesPerSecond, cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        Interlocked.Increment(ref errorCount);
                    }
                }
            }, cancellationTokenSource.Token);
            tasks.Add(task);
        }

        // 定期执行垃圾回收和状态检查
        var monitorTask = Task.Run(async () =>
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                await Task.Delay(30000, cancellationTokenSource.Token); // 每30秒

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var currentMemory = GC.GetTotalMemory(false);
                var memoryIncrease = (currentMemory - startMemory) / 1024.0 / 1024.0;

                Console.WriteLine($"Endurance Test - Messages: {messageCount}, Memory: {memoryIncrease:F2} MB, Errors: {errorCount}");
            }
        });

        try
        {
            await Task.WhenAll(tasks.Concat([monitorTask]));
        }
        catch (OperationCanceledException)
        {
            // Expected cancellation
        }

        LogFileHelper.Flush();
        await Task.Delay(2000);

        var finalMemory = GC.GetTotalMemory(false);
        var totalMemoryIncrease = (finalMemory - startMemory) / 1024.0 / 1024.0;

        // Assert
        Console.WriteLine($"=== Endurance Test Results ===");
        Console.WriteLine($"Duration: {DurationMinutes} minutes");
        Console.WriteLine($"Total Messages: {messageCount:N0}");
        Console.WriteLine($"Error Count: {errorCount}");
        Console.WriteLine($"Final Memory Increase: {totalMemoryIncrease:F2} MB");

        Assert.True(messageCount > DurationMinutes * 60 * MessagesPerSecond * 3 * 0.8,
            "Should have processed most expected messages");
        Assert.True(errorCount < messageCount * 0.01, "Error rate should be very low");
        Assert.True(totalMemoryIncrease < 50, "Memory increase should be reasonable");

        // 验证日志文件完整性
        var logFiles = Directory.GetFiles(_testLogDirectory, "*info*.log");
        Assert.NotEmpty(logFiles);
    }

    /// <summary>
    /// 资源清理测试
    /// </summary>
    [Fact]
    public void ResourceCleanup_ShouldReleaseResources()
    {
        // Arrange
        const int Iterations = 100;
        var initialMemory = GC.GetTotalMemory(true);

        // Act
        for (var i = 0; i < Iterations; i++)
        {
            LogFileHelper.Info($"Resource cleanup test iteration {i}");
            LogFileHelper.Warn($"Warning for iteration {i}");
            LogFileHelper.Error($"Error for iteration {i}");

            if (i % 10 == 0)
            {
                LogFileHelper.Flush();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        LogFileHelper.Flush();
        LogFileHelper.Clear(); // 清理所有日志

        // 强制垃圾回收
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryDifference = finalMemory - initialMemory;

        // Assert
        Console.WriteLine($"=== Resource Cleanup Test ===");
        Console.WriteLine($"Initial Memory: {initialMemory / 1024.0:F2} KB");
        Console.WriteLine($"Final Memory: {finalMemory / 1024.0:F2} KB");
        Console.WriteLine($"Memory Difference: {memoryDifference / 1024.0:F2} KB");

        // 内存增长应该在合理范围内
        Assert.True(Math.Abs(memoryDifference) < 1024 * 1024, // 1MB
            "Memory should be released properly after cleanup");
    }

    public void Dispose()
    {
        try
        {
            LogFileHelper.Flush();
            Thread.Sleep(1000);

            if (Directory.Exists(_testLogDirectory))
            {
                Directory.Delete(_testLogDirectory, true);
            }
        }
        catch (Exception)
        {
            // 忽略清理异常
        }
    }
}
