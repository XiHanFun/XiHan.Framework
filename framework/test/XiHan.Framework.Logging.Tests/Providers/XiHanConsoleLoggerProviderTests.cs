// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using System.Reflection;
using XiHan.Framework.Logging.Options;
using XiHan.Framework.Logging.Providers;

namespace XiHan.Framework.Logging.Tests.Providers;

/// <summary>
/// 控制台日志提供器测试
/// </summary>
/// <remarks>
/// 控制台提供器独有的开关（ShowTimestamp / ShowLogLevel / ShowCategoryName / SingleLine）只在这条通路上生效，
/// 文件通路测不到，所以这里临时接管 Console.Out 做断言。着色与彩虹输出依赖真实控制台句柄，
/// 用例一律关闭 EnableColors，避免在无控制台的 CI 上产生环境相关的失败。
/// </remarks>
[Collection(XiHanConsoleOutputCollection.Name)]
public class XiHanConsoleLoggerProviderTests
{
    private const string Category = "Cat.Console";

    /// <summary>
    /// 提供器别名是配置文件按提供器过滤日志的键，不允许漂移
    /// </summary>
    [Fact]
    public void ProviderAlias_IsXiHanConsole()
    {
        var alias = typeof(XiHanConsoleLoggerProvider).GetCustomAttribute<ProviderAliasAttribute>();

        Assert.NotNull(alias);
        Assert.Equal("XiHanConsole", alias.Alias);
    }

    /// <summary>
    /// 级别过滤按最小级别生效
    /// </summary>
    [Fact]
    public void IsEnabled_ReflectsConfiguredMinLevel()
    {
        var logger = CreateLogger(options => options.MinLevel = LogLevel.Error);

        Assert.False(logger.IsEnabled(LogLevel.Trace));
        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.False(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
        Assert.True(logger.IsEnabled(LogLevel.Critical));
    }

    /// <summary>
    /// None 级别永远不应被视为启用
    /// </summary>
    /// <remarks>
    /// 与文件日志器同源的问题：级别判断只做数值比较，而 None 是枚举里的最大值。
    /// 本用例按正确语义断言，红灯对应报告中的疑似缺陷条目。
    /// </remarks>
    [Fact]
    public void IsEnabled_WhenLogLevelNone_ReturnsFalse()
    {
        var logger = CreateLogger(options => options.MinLevel = LogLevel.Trace);

        Assert.False(logger.IsEnabled(LogLevel.None));
    }

    /// <summary>
    /// 低于最小级别的日志不产生任何输出
    /// </summary>
    [Fact]
    public void Log_BelowMinLevel_WritesNothing()
    {
        var marker = NewMarker();

        var lines = Capture(
            options =>
            {
                options.MinLevel = LogLevel.Error;
                options.LogFormat = "{Message}";
            },
            logger => logger.LogInformation(marker));

        Assert.Empty(MarkerLines(lines, marker));
    }

    /// <summary>
    /// 默认关闭着色时按模板逐字输出一行
    /// </summary>
    [Fact]
    public void Log_WithColorsDisabled_WritesSingleFormattedLine()
    {
        var marker = NewMarker();

        var lines = Capture(
            options => options.LogFormat = "{Level}|{Message}",
            logger => logger.LogInformation(marker));

        var line = Assert.Single(MarkerLines(lines, marker));
        Assert.Equal("Information|" + marker, line);
    }

    /// <summary>
    /// 关闭时间戳、级别与分类开关后，对应占位符渲染为空
    /// </summary>
    /// <remarks>
    /// 这三个开关不是「换个模板」，而是在既有模板上把对应片段抹空，
    /// 模板里的分隔符会原样保留，这一点必须锁住，否则宿主换开关会得到意料之外的排版。
    /// </remarks>
    [Fact]
    public void Log_WhenTimestampLevelAndCategoryDisabled_RendersThoseTokensEmpty()
    {
        var marker = NewMarker();

        var lines = Capture(
            options =>
            {
                options.LogFormat = "[{Timestamp:HH:mm:ss}] [{Level}] {Category}: {Message}";
                options.ShowTimestamp = false;
                options.ShowLogLevel = false;
                options.ShowCategoryName = false;
            },
            logger => logger.LogInformation(marker));

        var line = Assert.Single(MarkerLines(lines, marker));
        Assert.Equal("[] [] : " + marker, line);
        Assert.DoesNotContain(Category, line, StringComparison.Ordinal);
    }

    /// <summary>
    /// 时间戳格式选项在模板未自带格式时生效
    /// </summary>
    [Fact]
    public void Log_WhenTemplateTimestampHasNoFormat_AppliesTimestampFormatOption()
    {
        var marker = NewMarker();

        var lines = Capture(
            options =>
            {
                options.LogFormat = "{Timestamp}|{Message}";
                options.TimestampFormat = "yyyy";
            },
            logger => logger.LogInformation(marker));

        var line = Assert.Single(MarkerLines(lines, marker));
        Assert.Equal($"{DateTimeOffset.Now:yyyy}|{marker}", line);
    }

    /// <summary>
    /// 模板自带时间戳格式时不被选项覆盖
    /// </summary>
    [Fact]
    public void Log_WhenTemplateTimestampHasFormat_KeepsTemplateFormat()
    {
        var marker = NewMarker();

        var lines = Capture(
            options =>
            {
                options.LogFormat = "{Timestamp:yyyy}|{Message}";
                options.TimestampFormat = "HH:mm:ss";
            },
            logger => logger.LogInformation(marker));

        var line = Assert.Single(MarkerLines(lines, marker));
        Assert.Equal($"{DateTimeOffset.Now:yyyy}|{marker}", line);
    }

    /// <summary>
    /// 单行模式把多行异常压平到同一行
    /// </summary>
    /// <remarks>
    /// 单行模式的价值在于「一条日志一行」，日志采集端按行切分才不会把堆栈拆成孤儿行。
    /// 断言用内外层异常各自的标记同时出现在同一行，避开对堆栈文案的依赖。
    /// </remarks>
    [Fact]
    public void Log_WithSingleLineEnabled_CollapsesMultiLineExceptionIntoOneLine()
    {
        var marker = NewMarker();
        var exception = new InvalidOperationException(
            "outer-" + marker,
            new InvalidOperationException("inner-" + marker));

        var lines = Capture(
            options =>
            {
                options.LogFormat = "{Message} {Exception}";
                options.SingleLine = true;
            },
            logger => logger.LogError(exception, "m1"));

        var line = Assert.Single(MarkerLines(lines, "inner-" + marker));
        Assert.Contains("outer-" + marker, line, StringComparison.Ordinal);
    }

    /// <summary>
    /// 关闭单行模式时多行异常保持原有换行
    /// </summary>
    [Fact]
    public void Log_WithSingleLineDisabled_KeepsExceptionLineBreaks()
    {
        var marker = NewMarker();
        var exception = new InvalidOperationException(
            "outer-" + marker,
            new InvalidOperationException("inner-" + marker));

        var lines = Capture(
            options =>
            {
                options.LogFormat = "{Message} {Exception}";
                options.SingleLine = false;
            },
            logger => logger.LogError(exception, "m1"));

        var innerLine = Assert.Single(MarkerLines(lines, "inner-" + marker));
        Assert.DoesNotContain("outer-" + marker, innerLine, StringComparison.Ordinal);
    }

    /// <summary>
    /// 单行模式同样压平多行作用域文本
    /// </summary>
    [Fact]
    public void Log_WithSingleLineEnabled_CollapsesMultiLineScopeIntoOneLine()
    {
        var marker = NewMarker();
        var scopeText = "head-" + marker + Environment.NewLine + "tail-" + marker;

        var lines = Capture(
            options =>
            {
                options.LogFormat = "{Scope}|{Message}";
                options.SingleLine = true;
            },
            logger =>
            {
                using (logger.BeginScope(scopeText))
                {
                    logger.LogInformation("m1");
                }
            });

        var line = Assert.Single(MarkerLines(lines, "head-" + marker));
        Assert.Contains("tail-" + marker, line, StringComparison.Ordinal);
    }

    /// <summary>
    /// 关闭作用域开关后作用域不出现在输出中
    /// </summary>
    [Fact]
    public void Log_WhenIncludeScopesDisabled_OmitsScopeText()
    {
        var marker = NewMarker();

        var lines = Capture(
            options =>
            {
                options.LogFormat = "{Message}";
                options.IncludeScopes = false;
            },
            logger =>
            {
                using (logger.BeginScope("scope-" + marker))
                {
                    logger.LogInformation("msg-" + marker);
                }
            });

        var line = Assert.Single(MarkerLines(lines, "msg-" + marker));
        Assert.Equal("msg-" + marker, line);
    }

    /// <summary>
    /// 传入 null 的作用域提供器时回退到内置实现而不是崩溃
    /// </summary>
    [Fact]
    public void SetScopeProvider_WithNull_FallsBackToBuiltInProvider()
    {
        var marker = NewMarker();
        var options = new XiHanConsoleLoggerOptions
        {
            MinLevel = LogLevel.Trace,
            EnableColors = false,
            LogFormat = "{Scope}|{Message}"
        };

        var original = Console.Out;
        var buffer = new StringWriter();
        try
        {
            Console.SetOut(buffer);
            using var provider = new XiHanConsoleLoggerProvider(Microsoft.Extensions.Options.Options.Create(options));
            provider.SetScopeProvider(null!);
            var logger = provider.CreateLogger(Category);

            using (logger.BeginScope("scope-" + marker))
            {
                logger.LogInformation("msg-" + marker);
            }
        }
        finally
        {
            Console.SetOut(original);
        }

        var line = Assert.Single(MarkerLines(buffer.ToString().Split(Environment.NewLine), "msg-" + marker));
        Assert.Equal($"scope-{marker}|msg-{marker}", line);
    }

    private static string NewMarker()
    {
        return "mk" + Guid.NewGuid().ToString("N");
    }

    private static List<string> MarkerLines(IEnumerable<string> lines, string marker)
    {
        return [.. lines.Where(line => line.Contains(marker, StringComparison.Ordinal))];
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

    private static string[] Capture(Action<XiHanConsoleLoggerOptions> configure, Action<ILogger> write)
    {
        var original = Console.Out;
        var buffer = new StringWriter();
        try
        {
            Console.SetOut(buffer);
            write(CreateLogger(configure));
        }
        finally
        {
            Console.SetOut(original);
        }

        return buffer.ToString().Split(Environment.NewLine);
    }
}
