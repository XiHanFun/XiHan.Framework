// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Logging.Options;
using XiHan.Framework.Logging.Providers;

namespace XiHan.Framework.Logging.Tests.Providers;

/// <summary>
/// 控制台日志器标准错误流分流测试
/// </summary>
/// <remarks>
/// 锁住一条修复：XiHanConsoleLoggerOptions.UseStdErrorForErrors 原来定义了却没有任何读取点，
/// 宿主配成 true 也拿不到效果，错误级别照样写标准输出。修复后该开关按
/// Microsoft 官方控制台提供器 LogToStandardErrorThreshold 的语义生效：
/// 开关打开时 Error 及以上写 Console.Error，其余级别与关闭时一律写 Console.Out。
/// 断言必须同时接管两条流并交叉验证「在这条流里、不在那条流里」，只看一条流会把「两条都写了」放过去。
/// Console.Out / Console.Error 是进程级共享状态，故归入禁用并行的控制台输出集合；
/// 着色与彩虹输出依赖真实控制台句柄，用例一律关闭 EnableColors，避免在无控制台的 CI 上产生环境相关失败。
/// </remarks>
[Collection(XiHanConsoleOutputCollection.Name)]
public class XiHanConsoleLoggerStdErrorTests
{
    private const string Category = "Cat.StdError";

    /// <summary>
    /// 开关打开时错误级别写标准错误流
    /// </summary>
    /// <remarks>
    /// 这是修复前必然失败的核心场景：开关是 true，错误却仍旧只出现在标准输出里。
    /// </remarks>
    [Fact]
    public void Log_WhenStdErrorEnabled_WritesErrorToStandardError()
    {
        var marker = NewMarker();

        var captured = Capture(
            options =>
            {
                options.LogFormat = "{Message}";
                options.UseStdErrorForErrors = true;
            },
            logger => logger.LogError(marker));

        Assert.Contains(marker, captured.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(marker, captured.Out, StringComparison.Ordinal);
    }

    /// <summary>
    /// 开关打开时严重级别同样写标准错误流
    /// </summary>
    /// <remarks>
    /// 选项文案写的是「仅错误级别」，按官方阈值语义 Critical 高于 Error，必须一并分流，
    /// 否则最该被告警系统抓到的那一档反而留在标准输出里。
    /// </remarks>
    [Fact]
    public void Log_WhenStdErrorEnabled_WritesCriticalToStandardError()
    {
        var marker = NewMarker();

        var captured = Capture(
            options =>
            {
                options.LogFormat = "{Message}";
                options.UseStdErrorForErrors = true;
            },
            logger => logger.LogCritical(marker));

        Assert.Contains(marker, captured.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(marker, captured.Out, StringComparison.Ordinal);
    }

    /// <summary>
    /// 开关打开时警告级别及以下仍写标准输出
    /// </summary>
    /// <remarks>
    /// 边界：分流阈值卡在 Error，Warning 是紧邻的下一档，不能被一起冲进标准错误流，
    /// 否则按 stderr 判定告警的宿主会被警告日志刷屏。
    /// </remarks>
    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    public void Log_WhenStdErrorEnabledAndLevelBelowError_KeepsEntryOnStandardOutput(LogLevel logLevel)
    {
        var marker = NewMarker();

        var captured = Capture(
            options =>
            {
                options.LogFormat = "{Message}";
                options.UseStdErrorForErrors = true;
            },
            logger => logger.Log(logLevel, marker));

        Assert.Contains(marker, captured.Out, StringComparison.Ordinal);
        Assert.DoesNotContain(marker, captured.Error, StringComparison.Ordinal);
    }

    /// <summary>
    /// 开关保持默认关闭时错误级别仍写标准输出
    /// </summary>
    /// <remarks>
    /// 反例：默认值是 false，修复不能顺手把「错误写 stderr」变成默认行为，
    /// 那会改变所有既有宿主的输出目标。这条守住默认行为逐字节不变。
    /// </remarks>
    [Theory]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    public void Log_WhenStdErrorDisabled_KeepsEntryOnStandardOutput(LogLevel logLevel)
    {
        var marker = NewMarker();

        var captured = Capture(
            options => options.LogFormat = "{Message}",
            logger => logger.Log(logLevel, marker));

        Assert.Contains(marker, captured.Out, StringComparison.Ordinal);
        Assert.DoesNotContain(marker, captured.Error, StringComparison.Ordinal);
    }

    /// <summary>
    /// 分流不绕开最小级别过滤
    /// </summary>
    /// <remarks>
    /// 边界：分流只决定「写去哪条流」，不决定「写不写」。被最小级别挡下的错误日志
    /// 两条流里都不该出现，否则开关一开就等于把级别过滤失效了。
    /// </remarks>
    [Fact]
    public void Log_WhenStdErrorEnabledAndBelowMinLevel_WritesNothingToEitherStream()
    {
        var marker = NewMarker();

        var captured = Capture(
            options =>
            {
                options.MinLevel = LogLevel.Critical;
                options.LogFormat = "{Message}";
                options.UseStdErrorForErrors = true;
            },
            logger => logger.LogError(marker));

        Assert.DoesNotContain(marker, captured.Out, StringComparison.Ordinal);
        Assert.DoesNotContain(marker, captured.Error, StringComparison.Ordinal);
    }

    /// <summary>
    /// 分流后的日志条目仍按完整模板渲染
    /// </summary>
    /// <remarks>
    /// 修复只换输出目标，格式化管线一步都不能少，否则采集端会拿到两套排版。
    /// </remarks>
    [Fact]
    public void Log_WhenStdErrorEnabled_StillRendersConfiguredTemplate()
    {
        var marker = NewMarker();

        var captured = Capture(
            options =>
            {
                options.LogFormat = "{Level}|{Category}|{Message}";
                options.UseStdErrorForErrors = true;
            },
            logger => logger.LogError(marker));

        var line = Assert.Single(MarkerLines(captured.Error, marker));
        Assert.Equal($"Error|{Category}|{marker}", line);
    }

    /// <summary>
    /// 同一个日志器上错误与非错误各走各的流
    /// </summary>
    /// <remarks>
    /// 分流是按每条日志的级别决定的，不是按日志器决定的：同一个实例连写两条，
    /// 必须一条落 stderr 一条落 stdout，且不互相串流。
    /// </remarks>
    [Fact]
    public void Log_WhenStdErrorEnabled_SplitsStreamsPerEntry()
    {
        var infoMarker = NewMarker();
        var errorMarker = NewMarker();

        var captured = Capture(
            options =>
            {
                options.LogFormat = "{Message}";
                options.UseStdErrorForErrors = true;
            },
            logger =>
            {
                logger.LogInformation(infoMarker);
                logger.LogError(errorMarker);
            });

        Assert.Single(MarkerLines(captured.Out, infoMarker));
        Assert.Empty(MarkerLines(captured.Out, errorMarker));
        Assert.Single(MarkerLines(captured.Error, errorMarker));
        Assert.Empty(MarkerLines(captured.Error, infoMarker));
    }

    private static string NewMarker()
    {
        return "mk" + Guid.NewGuid().ToString("N");
    }

    private static List<string> MarkerLines(string text, string marker)
    {
        return [.. text.Split(Environment.NewLine).Where(line => line.Contains(marker, StringComparison.Ordinal))];
    }

    private static ILogger CreateLogger(Action<XiHanConsoleLoggerOptions> configure)
    {
        var options = new XiHanConsoleLoggerOptions
        {
            MinLevel = LogLevel.Trace,
            EnableColors = false
        };
        configure(options);

        var provider = new XiHanConsoleLoggerProvider(Microsoft.Extensions.Options.Options.Create(options));
        return provider.CreateLogger(Category);
    }

    private static CapturedConsole Capture(Action<XiHanConsoleLoggerOptions> configure, Action<ILogger> write)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var outBuffer = new StringWriter();
        var errorBuffer = new StringWriter();
        try
        {
            Console.SetOut(outBuffer);
            Console.SetError(errorBuffer);
            write(CreateLogger(configure));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        return new CapturedConsole(outBuffer.ToString(), errorBuffer.ToString());
    }

    /// <summary>
    /// 一次捕获中两条控制台流的内容
    /// </summary>
    /// <param name="Out">标准输出内容</param>
    /// <param name="Error">标准错误流内容</param>
    private sealed record CapturedConsole(string Out, string Error);
}
