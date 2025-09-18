#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LogFileHelperFixTests
// Guid:e45f678b-34c5-4d6e-9a12-5f8e0b7c3d4a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/18 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Utils.Tests.Logging;

/// <summary>
/// LogFileHelper 修复验证测试
/// </summary>
public class LogFileHelperFixTests : IDisposable
{
    private readonly string _testLogDirectory;

    public LogFileHelperFixTests()
    {
        _testLogDirectory = Path.Combine(Path.GetTempPath(), "XiHanTests", "Fix", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testLogDirectory);
        
        // 设置测试配置
        LogFileHelper.SetLogDirectory(_testLogDirectory);
        LogFileHelper.SetMaxFileSize(1024 * 50); // 50KB文件大小便于测试
        LogFileHelper.SetBufferSize(10); // 小缓冲区便于快速刷新
        LogFileHelper.SetFlushInterval(500);
        LogFileHelper.SetAsyncWriteEnabled(false); // 同步写入便于测试验证
    }

    /// <summary>
    /// 验证大量日志写入不会创建过多文件 - 重现用户问题的测试
    /// </summary>
    [Fact]
    public void LargeVolumeWrite_ShouldNotCreateExcessiveFiles()
    {
        // Arrange
        const int messageCount = 10000; // 1万条消息测试
        var expectedMaxFiles = 50; // 预期最多50个文件（50KB * 50 = 2.5MB）

        // Act
        for (var i = 0; i < messageCount; i++)
        {
            LogFileHelper.Handle($"应用启动中...{i + 1}/{messageCount}");
            
            // 每1000条强制刷新一次
            if (i % 1000 == 0)
            {
                LogFileHelper.Flush();
                Thread.Sleep(10);
            }
        }

        LogFileHelper.Flush();
        Thread.Sleep(1000); // 等待所有写入完成

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*handle*.log");
        
        Console.WriteLine($"Created {logFiles.Length} handle log files for {messageCount} messages");
        foreach (var file in logFiles.Take(10)) // 显示前10个文件信息
        {
            var fileInfo = new FileInfo(file);
            Console.WriteLine($"  {Path.GetFileName(file)}: {fileInfo.Length / 1024.0:F2} KB");
        }

        // 验证文件数量在合理范围内
        Assert.True(logFiles.Length <= expectedMaxFiles, 
            $"Created {logFiles.Length} files, expected max {expectedMaxFiles}");

        // 验证所有消息都被写入
        var totalLines = 0;
        foreach (var file in logFiles)
        {
            var lines = File.ReadAllLines(file);
            totalLines += lines.Length;
        }

        Assert.Equal(messageCount, totalLines);
    }

    /// <summary>
    /// 验证文件大小控制机制工作正常
    /// </summary>
    [Fact]
    public void FileSizeControl_ShouldWorkCorrectly()
    {
        // Arrange
        LogFileHelper.SetMaxFileSize(1024); // 1KB 非常小的文件大小
        const int messageCount = 500;
        var longMessage = new string('X', 100); // 100字符消息

        // Act
        for (var i = 0; i < messageCount; i++)
        {
            LogFileHelper.Info($"Message-{i:D3}: {longMessage}");
        }

        LogFileHelper.Flush();
        Thread.Sleep(500);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*info*.log");
        
        Console.WriteLine($"File size control test: {logFiles.Length} files created");
        
        // 应该创建多个文件
        Assert.True(logFiles.Length > 1, "Should create multiple files due to size limit");
        
        // 检查每个文件大小（除最后一个外都应该接近1KB）
        foreach (var file in logFiles.Take(logFiles.Length - 1))
        {
            var fileInfo = new FileInfo(file);
            Console.WriteLine($"  {Path.GetFileName(file)}: {fileInfo.Length} bytes");
            Assert.True(fileInfo.Length >= 1024 * 0.8, "File should be close to size limit");
        }

        // 验证消息完整性
        var totalLines = 0;
        foreach (var file in logFiles)
        {
            var lines = File.ReadAllLines(file);
            totalLines += lines.Length;
        }

        Assert.Equal(messageCount, totalLines);
    }

    /// <summary>
    /// 验证并发写入时文件管理的正确性
    /// </summary>
    [Fact]
    public async Task ConcurrentWrite_ShouldMaintainFileIntegrity()
    {
        // Arrange
        LogFileHelper.SetMaxFileSize(2048); // 2KB文件大小
        const int threadCount = 10;
        const int messagesPerThread = 100;
        var totalMessages = threadCount * messagesPerThread;

        // Act
        var tasks = new List<Task>();
        for (var i = 0; i < threadCount; i++)
        {
            var threadId = i;
            var task = Task.Run(() =>
            {
                for (var j = 0; j < messagesPerThread; j++)
                {
                    LogFileHelper.Warn($"Thread-{threadId:D2}-Message-{j:D3}: Concurrent test data");
                }
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        LogFileHelper.Flush();
        await Task.Delay(1000);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*warn*.log");
        
        Console.WriteLine($"Concurrent test: {logFiles.Length} files, {totalMessages} total messages");
        
        // 验证消息完整性
        var actualMessages = 0;
        var allContent = new List<string>();
        
        foreach (var file in logFiles)
        {
            var lines = File.ReadAllLines(file);
            actualMessages += lines.Length;
            allContent.AddRange(lines);
        }

        Assert.Equal(totalMessages, actualMessages);
        
        // 验证没有重复的消息
        var duplicates = allContent.GroupBy(x => x).Where(g => g.Count() > 1).ToList();
        Assert.Empty(duplicates);
    }

    /// <summary>
    /// 验证不同日志级别的文件管理
    /// </summary>
    [Fact]
    public void MultipleLogLevels_ShouldMaintainSeparateFiles()
    {
        // Arrange
        LogFileHelper.SetMaxFileSize(1024); // 1KB
        const int messagesPerLevel = 100;
        var longMessage = new string('Y', 80);

        // Act
        for (var i = 0; i < messagesPerLevel; i++)
        {
            LogFileHelper.Info($"Info-{i:D3}: {longMessage}");
            LogFileHelper.Warn($"Warn-{i:D3}: {longMessage}");
            LogFileHelper.Error($"Error-{i:D3}: {longMessage}");
            LogFileHelper.Success($"Success-{i:D3}: {longMessage}");
            LogFileHelper.Handle($"Handle-{i:D3}: {longMessage}");
        }

        LogFileHelper.Flush();
        Thread.Sleep(1000);

        // Assert
        var allLogFiles = Directory.GetFiles(_testLogDirectory, "*.log");
        var logLevels = new[] { "info", "warn", "error", "success", "handle" };

        Console.WriteLine($"Multiple levels test: {allLogFiles.Length} total files");

        foreach (var level in logLevels)
        {
            var levelFiles = allLogFiles.Where(f => Path.GetFileName(f).Contains(level)).ToArray();
            Assert.NotEmpty(levelFiles);
            
            var totalLines = 0;
            foreach (var file in levelFiles)
            {
                var lines = File.ReadAllLines(file);
                totalLines += lines.Length;
            }
            
            Assert.Equal(messagesPerLevel, totalLines);
            Console.WriteLine($"  {level}: {levelFiles.Length} files, {totalLines} messages");
        }
    }

    /// <summary>
    /// 验证文件命名的一致性
    /// </summary>
    [Fact]
    public void FileNaming_ShouldBeConsistent()
    {
        // Arrange
        LogFileHelper.SetMaxFileSize(512); // 512字节，强制多文件
        const int messageCount = 200;

        // Act
        for (var i = 0; i < messageCount; i++)
        {
            LogFileHelper.Error($"Error message {i:D3} with some content to reach size limit");
        }

        LogFileHelper.Flush();
        Thread.Sleep(500);

        // Assert
        var logFiles = Directory.GetFiles(_testLogDirectory, "*error*.log")
            .Select(f => Path.GetFileName(f))
            .OrderBy(f => f)
            .ToArray();

        Console.WriteLine($"File naming test: {logFiles.Length} files");
        foreach (var file in logFiles)
        {
            Console.WriteLine($"  {file}");
        }

        // 验证文件命名规律
        Assert.Contains(logFiles, f => f.Contains("error.log")); // 基础文件
        
        if (logFiles.Length > 1)
        {
            // 验证编号文件命名正确
            for (var i = 1; i < logFiles.Length; i++)
            {
                Assert.Contains($"error_{i}.log", logFiles);
            }
        }
    }

    /// <summary>
    /// 性能回归测试 - 确保修复后性能仍然良好
    /// </summary>
    [Fact]
    public void PerformanceRegression_ShouldMaintainGoodPerformance()
    {
        // Arrange
        LogFileHelper.SetMaxFileSize(10 * 1024); // 10KB
        const int messageCount = 5000;
        var message = "Performance test message with moderate length content";

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        for (var i = 0; i < messageCount; i++)
        {
            LogFileHelper.Success($"{message} #{i:D4}");
        }
        
        LogFileHelper.Flush();
        stopwatch.Stop();

        // Assert
        var throughput = messageCount / stopwatch.Elapsed.TotalSeconds;
        var logFiles = Directory.GetFiles(_testLogDirectory, "*success*.log");
        
        Console.WriteLine($"Performance test:");
        Console.WriteLine($"  Messages: {messageCount}");
        Console.WriteLine($"  Time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        Console.WriteLine($"  Throughput: {throughput:F0} messages/second");
        Console.WriteLine($"  Files created: {logFiles.Length}");

        // 性能应该保持良好
        Assert.True(throughput > 500, $"Throughput too low: {throughput:F0} msg/s");
        
        // 文件数量应该合理
        Assert.True(logFiles.Length < 10, $"Too many files created: {logFiles.Length}");
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

        // 恢复默认配置
        LogFileHelper.SetMaxFileSize(10 * 1024 * 1024); // 10MB
        LogFileHelper.SetBufferSize(100);
        LogFileHelper.SetFlushInterval(2000);
        LogFileHelper.SetAsyncWriteEnabled(true);
    }
}
