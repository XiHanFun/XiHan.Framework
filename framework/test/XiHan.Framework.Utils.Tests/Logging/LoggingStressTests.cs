#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LoggingStressTests
// Guid:a89b567c-23d4-4e5f-9a78-2c1d3e4f5a6b
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
/// Logging 模块压力测试
/// </summary>
public class LoggingStressTests : IDisposable
{
    private readonly string _testLogDirectory;

    public LoggingStressTests()
    {
        _testLogDirectory = Path.Combine(Path.GetTempPath(), "XiHanTests", "Stress", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testLogDirectory);

        LogFileHelper.SetLogDirectory(_testLogDirectory);
        LogFileHelper.SetBufferSize(200);
        LogFileHelper.SetMaxFileSize(2 * 1024 * 1024); // 2MB
        LogFileHelper.SetAsyncWriteEnabled(true);
    }

    /// <summary>
    /// 极限并发测试：最大线程数同时写入
    /// </summary>
    [Fact]
    public async Task ExtremeParallelism_MaxConcurrentWrites()
    {
        // Arrange
        var maxThreads = Environment.ProcessorCount * 4; // 4倍CPU核心数
        const int messagesPerThread = 100;
        var totalMessages = maxThreads * messagesPerThread;
        var completedThreads = 0;
        var exceptions = new ConcurrentBag<Exception>();

        Console.WriteLine($"Starting extreme parallelism test with {maxThreads} threads...");

        // Act
        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task>();

        for (var i = 0; i < maxThreads; i++)
        {
            var threadId = i;
            var task = Task.Run(() =>
            {
                try
                {
                    for (var j = 0; j < messagesPerThread; j++)
                    {
                        LogFileHelper.Info($"EXTREME Thread-{threadId:D3} Message-{j:D3} PId-{Environment.ProcessId}");

                        // 随机小延迟模拟不同的写入模式
                        if (Random.Shared.Next(100) < 5) // 5% 概率
                        {
                            Thread.Sleep(Random.Shared.Next(1, 3));
                        }
                    }
                    Interlocked.Increment(ref completedThreads);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        LogFileHelper.Flush();
        await Task.Delay(3000); // 等待异步写入完成

        // Assert
        Assert.Empty(exceptions);
        Assert.Equal(maxThreads, completedThreads);

        var throughput = totalMessages / stopwatch.Elapsed.TotalSeconds;
        Console.WriteLine($"Extreme Parallelism: {maxThreads} threads, {throughput:F0} messages/second");

        // 验证所有消息都被写入
        var logFiles = Directory.GetFiles(_testLogDirectory, "*info*.log");
        Assert.NotEmpty(logFiles);

        var actualMessageCount = 0;
        foreach (var file in logFiles)
        {
            var lines = await File.ReadAllLinesAsync(file);
            actualMessageCount += lines.Length;
        }

        Assert.Equal(totalMessages, actualMessageCount);
    }

    /// <summary>
    /// 内存压力测试：大量数据写入直到内存警告
    /// </summary>
    [Fact]
    public async Task MemoryPressure_LargeDataVolume()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(true);
        const int maxMemoryIncreaseMB = 100; // 100MB 限制
        var messageCount = 0;
        var largeData = new string('M', 2048); // 2KB per message

        // Act
        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task>();

        for (var i = 0; i < 10; i++) // 10个并发任务
        {
            var taskId = i;
            var task = Task.Run(async () =>
            {
                while (stopwatch.Elapsed < TimeSpan.FromMinutes(1)) // 最多运行1分钟
                {
                    var currentMemory = GC.GetTotalMemory(false);
                    var memoryIncrease = (currentMemory - initialMemory) / 1024.0 / 1024.0;

                    if (memoryIncrease > maxMemoryIncreaseMB)
                    {
                        break; // 达到内存限制
                    }

                    LogFileHelper.Warn($"MEMORY Task-{taskId} #{Interlocked.Increment(ref messageCount):D6}: {largeData}");

                    if (messageCount % 100 == 0)
                    {
                        await Task.Delay(10); // 给系统一些喘息时间
                    }
                }
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        LogFileHelper.Flush();
        await Task.Delay(2000);

        var finalMemory = GC.GetTotalMemory(false);
        var totalMemoryIncrease = (finalMemory - initialMemory) / 1024.0 / 1024.0;

        // Assert
        Console.WriteLine($"Memory Pressure Test:");
        Console.WriteLine($"  Messages Written: {messageCount:N0}");
        Console.WriteLine($"  Duration: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($"  Memory Increase: {totalMemoryIncrease:F2} MB");
        Console.WriteLine($"  Data Volume: {messageCount * 2.0 / 1024:F2} MB");

        Assert.True(messageCount > 1000, "Should write significant number of messages");
        Assert.True(totalMemoryIncrease <= maxMemoryIncreaseMB * 1.5,
            "Memory usage should be controlled even under pressure");

        // 验证文件内容
        var logFiles = Directory.GetFiles(_testLogDirectory, "*warn*.log");
        Assert.NotEmpty(logFiles);
    }

    /// <summary>
    /// 文件系统压力测试：频繁文件滚动
    /// </summary>
    [Fact]
    public async Task FileSystemPressure_FrequentRollover()
    {
        // Arrange
        LogFileHelper.SetMaxFileSize(50 * 1024); // 50KB 非常小的文件，强制频繁滚动
        const int threadCount = 8;
        const int messagesPerThread = 500;
        var longMessage = new string('F', 200); // 200字符消息强制滚动

        // Act
        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task>();

        for (var i = 0; i < threadCount; i++)
        {
            var threadId = i;
            var task = Task.Run(() =>
            {
                for (var j = 0; j < messagesPerThread; j++)
                {
                    LogFileHelper.Error($"ROLLOVER Thread-{threadId:D2} #{j:D3} {DateTime.Now:HH:mm:ss.fff}: {longMessage}");

                    // 偶尔暂停让文件滚动完成
                    if (j % 50 == 0)
                    {
                        Thread.Sleep(5);
                    }
                }
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        LogFileHelper.Flush();
        await Task.Delay(3000);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*error*.log");

        Console.WriteLine($"File System Pressure Test:");
        Console.WriteLine($"  Total Log Files: {logFiles.Length}");
        Console.WriteLine($"  Duration: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($"  Files per Second: {logFiles.Length / stopwatch.Elapsed.TotalSeconds:F2}");

        // 应该生成多个文件（由于频繁滚动）
        Assert.True(logFiles.Length > 10, $"Expected multiple files due to rollover, got {logFiles.Length}");

        // 验证总消息数
        var totalMessages = 0;
        foreach (var file in logFiles)
        {
            var lines = await File.ReadAllLinesAsync(file);
            totalMessages += lines.Length;
        }

        Assert.Equal(threadCount * messagesPerThread, totalMessages);
    }

    /// <summary>
    /// 错误恢复测试：模拟文件系统错误
    /// </summary>
    [Fact]
    public async Task ErrorRecovery_FileSystemErrors()
    {
        // Arrange
        const int normalMessageCount = 1000;
        var messageCount = 0;
        var errorCount = 0;

        // Act - 正常写入一些消息
        for (var i = 0; i < normalMessageCount / 2; i++)
        {
            LogFileHelper.Success($"Normal message before error simulation #{++messageCount}");
        }

        // 模拟文件系统压力：创建很多任务同时写入
        var stressTasks = new List<Task>();
        for (var i = 0; i < 20; i++)
        {
            var taskId = i;
            var task = Task.Run(() =>
            {
                try
                {
                    for (var j = 0; j < 100; j++)
                    {
                        LogFileHelper.Success($"Stress task {taskId} message {j}");
                        Interlocked.Increment(ref messageCount);
                    }
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref errorCount);
                }
            });
            stressTasks.Add(task);
        }

        await Task.WhenAll(stressTasks);

        // 继续正常写入
        for (var i = 0; i < normalMessageCount / 2; i++)
        {
            LogFileHelper.Success($"Normal message after stress test #{++messageCount}");
        }

        LogFileHelper.Flush();
        await Task.Delay(2000);

        // Assert
        Console.WriteLine($"Error Recovery Test:");
        Console.WriteLine($"  Total Messages: {messageCount}");
        Console.WriteLine($"  Error Count: {errorCount}");

        Assert.True(messageCount > normalMessageCount, "Should process most messages despite stress");
        Assert.True(errorCount < messageCount * 0.1, "Error rate should be low");

        // 验证文件完整性
        var logFiles = Directory.GetFiles(_testLogDirectory, "*success*.log");
        Assert.NotEmpty(logFiles);
    }

    /// <summary>
    /// 资源竞争测试：多种操作同时进行
    /// </summary>
    [Fact]
    public async Task ResourceContention_MixedOperations()
    {
        // Arrange
        const int durationSeconds = 30;
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        var operations = new ConcurrentDictionary<string, int>();

        // Act - 启动多种并发操作
        var tasks = new List<Task>
        {
            // 持续写入不同级别的日志
            Task.Run(async () =>
            {
                var count = 0;
                while (!cancellationToken.Token.IsCancellationRequested)
                {
                    LogFileHelper.Info($"Continuous info #{++count}");
                    operations.AddOrUpdate("Info", 1, (k, v) => v + 1);
                    await Task.Delay(10, cancellationToken.Token);
                }
            }),

            Task.Run(async () =>
            {
                var count = 0;
                while (!cancellationToken.Token.IsCancellationRequested)
                {
                    LogFileHelper.Warn($"Continuous warn #{++count}");
                    operations.AddOrUpdate("Warn", 1, (k, v) => v + 1);
                    await Task.Delay(15, cancellationToken.Token);
                }
            }),

            Task.Run(async () =>
            {
                var count = 0;
                while (!cancellationToken.Token.IsCancellationRequested)
                {
                    LogFileHelper.Error($"Continuous error #{++count}");
                    operations.AddOrUpdate("Error", 1, (k, v) => v + 1);
                    await Task.Delay(20, cancellationToken.Token);
                }
            }),

            // 定期刷新
            Task.Run(async () =>
            {
                var count = 0;
                while (!cancellationToken.Token.IsCancellationRequested)
                {
                    LogFileHelper.Flush();
                    operations.AddOrUpdate("Flush", 1, (k, v) => v + 1);
                    count++;
                    await Task.Delay(500, cancellationToken.Token);
                }
            }),

            // 配置更改
            Task.Run(async () =>
            {
                var count = 0;
                var bufferSizes = new[] { 50, 100, 150, 200 };
                while (!cancellationToken.Token.IsCancellationRequested)
                {
                    var newSize = bufferSizes[count % bufferSizes.Length];
                    LogFileHelper.SetBufferSize(newSize);
                    operations.AddOrUpdate("ConfigChange", 1, (k, v) => v + 1);
                    count++;
                    await Task.Delay(1000, cancellationToken.Token);
                }
            })
        };

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Expected cancellation
        }

        LogFileHelper.Flush();
        await Task.Delay(2000);

        // Assert
        Console.WriteLine($"Resource Contention Test Results:");
        foreach (var op in operations)
        {
            Console.WriteLine($"  {op.Key}: {op.Value} operations");
        }

        Assert.True(operations["Info"] > 100, "Should perform many info operations");
        Assert.True(operations["Warn"] > 50, "Should perform many warn operations");
        Assert.True(operations["Error"] > 30, "Should perform many error operations");
        Assert.True(operations["Flush"] > 10, "Should perform multiple flush operations");

        // 验证所有日志文件
        var allLogFiles = Directory.GetFiles(_testLogDirectory, "*.log");
        Assert.NotEmpty(allLogFiles);
        Console.WriteLine($"  Total Log Files Created: {allLogFiles.Length}");
    }

    /// <summary>
    /// 长期稳定性测试：连续运行检查内存泄漏
    /// </summary>
    [Fact]
    public async Task LongTermStability_MemoryLeakCheck()
    {
        // Arrange
        const int cycles = 10;
        const int messagesPerCycle = 1000;
        var memorySnapshots = new List<long>();

        // Act
        for (var cycle = 0; cycle < cycles; cycle++)
        {
            // 记录内存快照
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var memoryBefore = GC.GetTotalMemory(false);

            // 执行一轮日志写入
            var tasks = new List<Task>();
            for (var i = 0; i < 5; i++)
            {
                var taskId = i;
                var task = Task.Run(() =>
                {
                    for (var j = 0; j < messagesPerCycle / 5; j++)
                    {
                        LogFileHelper.Handle($"Cycle-{cycle:D2} Task-{taskId} Message-{j:D3}");
                    }
                });
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
            LogFileHelper.Flush();
            await Task.Delay(500);

            // 再次记录内存
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var memoryAfter = GC.GetTotalMemory(false);

            memorySnapshots.Add(memoryAfter);

            Console.WriteLine($"Cycle {cycle + 1}: Memory = {memoryAfter / 1024.0:F2} KB, " +
                            $"Increase = {(memoryAfter - memoryBefore) / 1024.0:F2} KB");
        }

        // Assert
        var firstSnapshot = memorySnapshots[0];
        var lastSnapshot = memorySnapshots[^1];
        var totalIncrease = (lastSnapshot - firstSnapshot) / 1024.0 / 1024.0; // MB

        Console.WriteLine($"Long Term Stability Test:");
        Console.WriteLine($"  Cycles: {cycles}");
        Console.WriteLine($"  Messages per Cycle: {messagesPerCycle}");
        Console.WriteLine($"  Total Memory Increase: {totalIncrease:F2} MB");

        // 内存增长应该在合理范围内（不应该有严重的内存泄漏）
        Assert.True(totalIncrease < 20, $"Memory increase ({totalIncrease:F2} MB) should be reasonable");

        // 检查是否有持续增长的趋势
        var growthTrend = memorySnapshots.Skip(1).Zip(memorySnapshots, (current, previous) => current - previous).Average();
        Console.WriteLine($"  Average Growth per Cycle: {growthTrend / 1024.0:F2} KB");

        Assert.True(growthTrend < 100 * 1024, "Average growth per cycle should be minimal"); // 小于100KB
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
