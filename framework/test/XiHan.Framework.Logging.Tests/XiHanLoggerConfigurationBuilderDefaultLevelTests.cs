// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Serilog.Core;
using Serilog.Events;
using XiHan.Framework.Logging.Tests.Fakes;

namespace XiHan.Framework.Logging.Tests;

/// <summary>
/// 曦寒日志配置构建器默认最小级别测试
/// </summary>
/// <remarks>
/// 锁住一条修复：MinimumLevelDefault 原来在 #if DEBUG 里设 Debug 之后又无条件设了一次 Information，
/// Serilog 的最小级别后设者覆盖前者，于是 DEBUG 分支是死代码，调试构建与发布构建完全一样；
/// 同一个构建器里 WriteToConsoleDefault / WriteToFileDefault 的 #if DEBUG 调试级接收器也因此永远收不到事件。
/// 「调试构建放行 Debug」这件事本身随编译符号变化，所以按构建配置分别断言：
/// 测试工程与被测工程由同一次构建产出、共享同一个 Configuration，两侧的 DEBUG 符号是一致的。
/// 挂内存接收器把配置真正跑起来，不落盘、不打控制台。
/// </remarks>
public class XiHanLoggerConfigurationBuilderDefaultLevelTests
{
    /// <summary>
    /// 默认最小级别随构建配置变化
    /// </summary>
    /// <remarks>
    /// 修复前无论哪种构建都停在 Information，调试构建下这条会红。
    /// </remarks>
    [Fact]
    public void MinimumLevelDefault_HonorsBuildConfiguration()
    {
        var sink = new CollectingSink();
        var builder = new XiHanLoggerConfigurationBuilder().MinimumLevelDefault();

        using (var logger = BuildLogger(builder, sink))
        {
            logger.Verbose("verbose");
            logger.Debug("debug");
            logger.Information("info");
        }

#if DEBUG
        // 调试构建：#if DEBUG 分支必须真的生效，调试级放行
        Assert.Collection(
            sink.Events,
            evt => Assert.Equal(LogEventLevel.Debug, evt.Level),
            evt => Assert.Equal(LogEventLevel.Information, evt.Level));
#else
        // 发布构建：门槛停在信息级
        var onlyEvent = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Information, onlyEvent.Level);
#endif
    }

    /// <summary>
    /// 默认最小级别在任何构建下都丢弃跟踪级
    /// </summary>
    /// <remarks>
    /// Verbose 低于 Debug 与 Information，是两种构建的共同下界，可以无条件断言。
    /// </remarks>
    [Fact]
    public void MinimumLevelDefault_AlwaysDropsVerbose()
    {
        var sink = new CollectingSink();
        var builder = new XiHanLoggerConfigurationBuilder().MinimumLevelDefault();

        using (var logger = BuildLogger(builder, sink))
        {
            logger.Verbose("verbose");
        }

        Assert.Empty(sink.Events);
    }

    /// <summary>
    /// 默认最小级别在任何构建下都放行信息级及以上
    /// </summary>
    /// <remarks>
    /// 修复不能把门槛抬高，这条守住「调试构建放宽」不会误伤已有的信息级输出。
    /// </remarks>
    [Fact]
    public void MinimumLevelDefault_AlwaysKeepsInformationAndAbove()
    {
        var sink = new CollectingSink();
        var builder = new XiHanLoggerConfigurationBuilder().MinimumLevelDefault();

        using (var logger = BuildLogger(builder, sink))
        {
            logger.Information("info");
            logger.Warning("warning");
            logger.Error("error");
            logger.Fatal("fatal");
        }

        Assert.Equal(
            new[] { LogEventLevel.Information, LogEventLevel.Warning, LogEventLevel.Error, LogEventLevel.Fatal },
            sink.Events.Select(evt => evt.Level));
    }

    /// <summary>
    /// 默认最小级别之后仍可被显式最小级别覆盖
    /// </summary>
    /// <remarks>
    /// 反例：修复只调整默认分支，不能把 MinimumLevelDefault 变成一道拦住后续显式配置的门。
    /// </remarks>
    [Fact]
    public void MinimumLevelDefault_IsStillOverridableByExplicitLevel()
    {
        var sink = new CollectingSink();
        var builder = new XiHanLoggerConfigurationBuilder()
            .MinimumLevelDefault()
            .MinimumLevel(LogEventLevel.Error);

        using (var logger = BuildLogger(builder, sink))
        {
            logger.Information("dropped-info");
            logger.Warning("dropped-warning");
            logger.Error("kept-error");
        }

        var onlyEvent = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Error, onlyEvent.Level);
    }

    private static Logger BuildLogger(XiHanLoggerConfigurationBuilder builder, CollectingSink sink)
    {
        var configuration = builder.Build();
        configuration.WriteTo.Sink(sink);
        return configuration.CreateLogger();
    }
}
