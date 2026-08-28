// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Logging.Extensions.DependencyInjection;
using XiHan.Framework.Logging.Tests.Providers;

namespace XiHan.Framework.Logging.Tests.Extensions.DependencyInjection;

/// <summary>
/// Serilog 文件接收器异步选项装配测试
/// </summary>
/// <remarks>
/// 锁住一条修复：AddXiHanSerilog 原来无条件把文件接收器包进 WriteTo.Async，也不传缓冲区大小与满队列策略，
/// 于是 XiHanLoggingOptions 的 EnableAsyncLogging / AsyncBufferSize / BlockWhenFull 三个选项在全仓没有读取点。
/// 接收器的形状只能通过真实建管道来观察，因此本类是这个测试工程里唯一会解析 ILoggerFactory 的地方，代价是：
/// 日志文件必须落在独占临时目录；读取时要允许写入方继续持有句柄；建管道会顺带接管进程级的
/// Console.Out 与 Serilog 静态日志器，所以归入禁用并行的控制台输出集合，避免和接管控制台的用例互相干扰。
/// </remarks>
[Collection(XiHanConsoleOutputCollection.Name)]
public sealed class XiHanSerilogAsyncPipelineTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// 构造函数，准备独占的临时目录
    /// </summary>
    public XiHanSerilogAsyncPipelineTests()
    {
        Directory.CreateDirectory(_root);
    }

    /// <summary>
    /// 归还静态日志器并清理临时目录
    /// </summary>
    /// <remarks>
    /// 必须先关日志器再删目录：文件接收器不关就一直持有写句柄，目录删不掉。
    /// </remarks>
    public void Dispose()
    {
        try
        {
            Serilog.Log.CloseAndFlush();
        }
        catch
        {
            // 归还失败不影响断言结果
        }

        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // 清理失败不影响断言结果
        }
    }

    /// <summary>
    /// 关闭异步日志时文件接收器同步落盘
    /// </summary>
    /// <remarks>
    /// 这是修复前必然失败的核心场景：EnableAsyncLogging 配成 false，写入却仍被推给后台队列，
    /// 日志调用返回时磁盘上还没有这条记录。同步接收器的契约就是「写调用返回即落盘」，所以这里不做任何等待。
    /// </remarks>
    [Fact]
    public void AddXiHanLogging_WhenAsyncLoggingDisabled_WritesFileSynchronously()
    {
        var filePath = Path.Combine(_root, "sync.log");
        IServiceCollection services = new ServiceCollection();
        services.AddXiHanLogging(options =>
        {
            options.FileOutputPath = filePath;
            // 关掉滚动，文件名才会原样落在预期路径上；按天滚动会在扩展名前插日期
            options.RollingInterval = Serilog.RollingInterval.Infinite;
            options.EnableAsyncLogging = false;
        });

        var provider = services.BuildServiceProvider();
        try
        {
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("SyncCase");

            logger.LogInformation("sync-marker");

            Assert.Contains("sync-marker", ReadWhileSinkHoldsFile(filePath), StringComparison.Ordinal);
        }
        finally
        {
            DisposeQuietly(provider);
        }
    }

    /// <summary>
    /// 开启异步日志时缓冲区大小与满队列策略透传给异步包装器
    /// </summary>
    /// <remarks>
    /// 边界与反例：把队列容量压到 2 并要求满队列时阻塞，5 条事件必须一条不少地落盘。
    /// 若 BlockWhenFull 没有透传（异步包装器默认是丢弃），这种极小队列下超出的事件会被静默丢掉。
    /// 关闭静态日志器会等后台线程把队列写完，所以断言前不需要轮询等待。
    /// </remarks>
    [Fact]
    public void AddXiHanLogging_WhenAsyncLoggingEnabled_KeepsEveryEventUnderTinyBuffer()
    {
        var filePath = Path.Combine(_root, "async.log");
        IServiceCollection services = new ServiceCollection();
        services.AddXiHanLogging(options =>
        {
            options.FileOutputPath = filePath;
            options.RollingInterval = Serilog.RollingInterval.Infinite;
            options.EnableAsyncLogging = true;
            options.AsyncBufferSize = 2;
            options.BlockWhenFull = true;
        });

        var provider = services.BuildServiceProvider();
        try
        {
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("AsyncCase");
            for (var index = 0; index < 5; index++)
            {
                logger.LogInformation("async-marker-{Index}", index);
            }

            // 建管道时静态日志器已被换成刚建的这个（AddSerilog 默认不保留原值），关它即等待后台队列排空
            Serilog.Log.CloseAndFlush();

            var content = ReadWhileSinkHoldsFile(filePath);
            for (var index = 0; index < 5; index++)
            {
                Assert.Contains($"async-marker-{index}", content, StringComparison.Ordinal);
            }
        }
        finally
        {
            DisposeQuietly(provider);
        }
    }

    /// <summary>
    /// 释放容器，忽略释放期异常
    /// </summary>
    /// <remarks>
    /// 容器与 Log.CloseAndFlush 归还的可能是同一个 Serilog 日志器，归还顺序由 Serilog 的宿主集成决定；
    /// 这里只做清理，不该因为重复释放把断言已经通过的用例判红。
    /// </remarks>
    /// <param name="provider">服务提供器</param>
    private static void DisposeQuietly(ServiceProvider provider)
    {
        try
        {
            provider.Dispose();
        }
        catch
        {
            // 释放失败不影响断言结果
        }
    }

    /// <summary>
    /// 读取仍被接收器持有的日志文件
    /// </summary>
    /// <remarks>
    /// Serilog 文件接收器全程持有写句柄，File.ReadAllText 只声明允许读共享，会因共享模式不兼容抛 IOException，
    /// 必须显式声明允许对方继续写。
    /// </remarks>
    /// <param name="filePath">日志文件路径</param>
    /// <returns></returns>
    private static string ReadWhileSinkHoldsFile(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
