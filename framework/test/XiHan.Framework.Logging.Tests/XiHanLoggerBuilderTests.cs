// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Serilog.Events;

namespace XiHan.Framework.Logging.Tests;

/// <summary>
/// 曦寒日志构建器测试
/// </summary>
/// <remarks>
/// 只覆盖不落盘的两条路径：空配置构建与从外部配置构建。
/// CreateLoggerDefault 会在配置期就把文件接收器建起来并往程序目录写日志文件，
/// 属于宿主启动期行为，不适合放进单元测试，另行说明未覆盖。
/// </remarks>
public class XiHanLoggerBuilderTests
{
    /// <summary>
    /// 未挂接收器时构建出的日志器沿用默认最小级别且写入不抛异常
    /// </summary>
    [Fact]
    public void CreateLogger_WithoutSinks_UsesDefaultMinimumLevelAndSwallowsWrites()
    {
        using var logger = new XiHanLoggerBuilder().CreateLogger();

        Assert.True(logger.IsEnabled(LogEventLevel.Information));
        Assert.True(logger.IsEnabled(LogEventLevel.Fatal));

        logger.Information("no-sink");
        logger.Error(new InvalidOperationException("boom"), "no-sink");
    }

    /// <summary>
    /// 从配置构建时应用配置里的最小级别
    /// </summary>
    [Fact]
    public void CreateLogger_WithConfiguration_AppliesConfiguredMinimumLevel()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:MinimumLevel:Default"] = "Fatal"
            })
            .Build();

        using var logger = new XiHanLoggerBuilder().CreateLogger(configuration);

        Assert.False(logger.IsEnabled(LogEventLevel.Information));
        Assert.False(logger.IsEnabled(LogEventLevel.Error));
        Assert.True(logger.IsEnabled(LogEventLevel.Fatal));
    }

    /// <summary>
    /// 从配置构建时未指定最小级别则沿用默认值
    /// </summary>
    [Fact]
    public void CreateLogger_WithEmptyConfiguration_KeepsDefaultMinimumLevel()
    {
        var configuration = new ConfigurationBuilder().Build();

        using var logger = new XiHanLoggerBuilder().CreateLogger(configuration);

        Assert.True(logger.IsEnabled(LogEventLevel.Information));
        Assert.False(logger.IsEnabled(LogEventLevel.Debug));
    }

    /// <summary>
    /// 每次构建产出彼此独立的日志器实例
    /// </summary>
    /// <remarks>
    /// 构建器内部持有一份 Serilog 配置且只允许构建一次，
    /// 因此宿主要拿多个日志器必须各起一个构建器，这里锁住实例不共享这一点。
    /// </remarks>
    [Fact]
    public void CreateLogger_FromSeparateBuilders_ProducesIndependentLoggers()
    {
        using var first = new XiHanLoggerBuilder().CreateLogger();
        using var second = new XiHanLoggerBuilder().CreateLogger();

        Assert.NotSame(first, second);
    }
}
