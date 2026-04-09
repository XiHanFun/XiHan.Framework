#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanFileLoggerProviderTests
// Guid:8e302ec5-97c8-4d61-a94f-35b7db3707ea
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/10 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Logging.Extensions.DependencyInjection;
using XiHan.Framework.Logging.Options;

namespace XiHan.Framework.Logging.Tests.Providers;

/// <summary>
/// XiHan 文件日志提供器测试
/// </summary>
public class XiHanFileLoggerProviderTests
{
    [Fact]
    public void WriteLog_WithScope_ShouldWriteScopeTextAndDatedFileName()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var filePathTemplate = Path.Combine(tempRoot, "xihan-{Date}.log");
            using var serviceProvider = BuildServiceProvider(options =>
            {
                options.FilePath = filePathTemplate;
                options.MinLevel = LogLevel.Trace;
                options.IncludeScopes = true;
                options.LogFormat = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] {Category} {Scope} {Message}{NewLine}{Exception}";
            });

            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Tests.Logging.Scope");
            using (logger.BeginScope("tenant=t1"))
            {
                logger.LogInformation("hello {Name}", "world");
            }

            var expectedFileName = $"xihan-{DateTimeOffset.Now:yyyyMMdd}.log";
            var expectedFilePath = Path.Combine(tempRoot, expectedFileName);
            Assert.True(File.Exists(expectedFilePath));

            var content = File.ReadAllText(expectedFilePath);
            Assert.Contains("tenant=t1", content);
            Assert.Contains("hello world", content);
            Assert.Contains("Tests.Logging.Scope", content);
        }
        finally
        {
            SafeDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void WriteLog_WhenIncludeScopesFalse_ShouldNotPersistScopeText()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var filePathTemplate = Path.Combine(tempRoot, "noscope-{Date}.log");
            using var serviceProvider = BuildServiceProvider(options =>
            {
                options.FilePath = filePathTemplate;
                options.MinLevel = LogLevel.Trace;
                options.IncludeScopes = false;
                options.LogFormat = "[{Level}] {Scope} {Message}";
            });

            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Tests.Logging.NoScope");
            using (logger.BeginScope("tenant=t2"))
            {
                logger.LogInformation("scope-disabled");
            }

            var expectedFilePath = Path.Combine(tempRoot, $"noscope-{DateTimeOffset.Now:yyyyMMdd}.log");
            var content = File.ReadAllText(expectedFilePath);
            Assert.DoesNotContain("tenant=t2", content);
            Assert.Contains("scope-disabled", content);
        }
        finally
        {
            SafeDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void WriteLog_WhenExceedSizeLimit_ShouldArchiveAndRespectRetention()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var filePathTemplate = Path.Combine(tempRoot, "rolling-{Date}.log");
            using var serviceProvider = BuildServiceProvider(options =>
            {
                options.FilePath = filePathTemplate;
                options.MinLevel = LogLevel.Trace;
                options.FileSizeLimit = 80;
                options.RetainedFileCountLimit = 1;
                options.LogFormat = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {Message}";
            });

            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Tests.Logging.Rolling");
            for (var i = 0; i < 20; i++)
            {
                logger.LogInformation("size-limit-test-{Index}-{Payload}", i, new string('x', 64));
                Thread.Sleep(2);
            }

            var activeFileName = $"rolling-{DateTimeOffset.Now:yyyyMMdd}.log";
            var activeFilePath = Path.Combine(tempRoot, activeFileName);
            Assert.True(File.Exists(activeFilePath));

            var archives = Directory
                .EnumerateFiles(tempRoot, $"rolling-{DateTimeOffset.Now:yyyyMMdd}.*.log")
                .ToList();

            Assert.True(archives.Count <= 1, $"归档文件数量超过限制，当前数量: {archives.Count}");
        }
        finally
        {
            SafeDeleteDirectory(tempRoot);
        }
    }

    private static ServiceProvider BuildServiceProvider(Action<XiHanFileLoggerOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddXiHanFileLogger(configure);
        });
        return services.BuildServiceProvider();
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "XiHan.Framework.Logging.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void SafeDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // 忽略清理失败，避免影响断言结果
        }
    }
}
