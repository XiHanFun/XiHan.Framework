// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Logging.Options;
using XiHan.Framework.Logging.Providers;

namespace XiHan.Framework.Logging.Tests.Providers;

/// <summary>
/// 日志条目格式化契约测试
/// </summary>
/// <remarks>
/// 格式化器本身是 internal，唯一的公开出口是文件/控制台提供器的落地文本。
/// 这里统一走文件提供器：文件是确定性的、可逐字节回读的通道，能把模板占位符的替换规则、
/// 异常与作用域的兜底追加规则完整暴露出来；控制台特有的开关另见 XiHanConsoleLoggerProviderTests。
/// </remarks>
public sealed class XiHanLogEntryFormattingTests : IDisposable
{
    private const string Category = "Cat.Sub";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// 构造函数，准备独占的临时目录
    /// </summary>
    public XiHanLogEntryFormattingTests()
    {
        Directory.CreateDirectory(_root);
    }

    /// <summary>
    /// 清理临时目录
    /// </summary>
    public void Dispose()
    {
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
    /// 级别占位符的 u3 格式取前三位大写
    /// </summary>
    [Theory]
    [InlineData(LogLevel.Trace, "TRA")]
    [InlineData(LogLevel.Debug, "DEB")]
    [InlineData(LogLevel.Information, "INF")]
    [InlineData(LogLevel.Warning, "WAR")]
    [InlineData(LogLevel.Error, "ERR")]
    [InlineData(LogLevel.Critical, "CRI")]
    public void Format_WithUpperThreeLevelToken_WritesThreeLetterUpperCase(LogLevel level, string expected)
    {
        var content = WriteAndRead(
            options => options.LogFormat = "{Level:u3}|{Message}",
            logger => logger.Log(level, "m1"));

        Assert.StartsWith(expected + "|m1", content);
    }

    /// <summary>
    /// 级别占位符的 w3 格式取前三位小写
    /// </summary>
    [Fact]
    public void Format_WithLowerThreeLevelToken_WritesThreeLetterLowerCase()
    {
        var content = WriteAndRead(
            options => options.LogFormat = "{Level:w3}|{Message}",
            logger => logger.LogWarning("m1"));

        Assert.StartsWith("war|m1", content);
    }

    /// <summary>
    /// 不带格式的级别占位符输出完整级别名
    /// </summary>
    [Fact]
    public void Format_WithBareLevelToken_WritesFullLevelName()
    {
        var content = WriteAndRead(
            options => options.LogFormat = "{Level}|{Message}",
            logger => logger.LogInformation("m1"));

        Assert.StartsWith("Information|m1", content);
    }

    /// <summary>
    /// 无法识别的级别格式回退为完整级别名
    /// </summary>
    [Fact]
    public void Format_WithUnknownLevelFormat_FallsBackToFullLevelName()
    {
        var content = WriteAndRead(
            options => options.LogFormat = "{Level:zz}|{Message}",
            logger => logger.LogError("m1"));

        Assert.StartsWith("Error|m1", content);
    }

    /// <summary>
    /// 时间戳占位符按模板给定的格式渲染
    /// </summary>
    [Fact]
    public void Format_WithCustomTimestampFormat_UsesGivenPattern()
    {
        var content = WriteAndRead(
            options => options.LogFormat = "{Timestamp:yyyy}|{Message}",
            logger => logger.LogInformation("m1"));

        Assert.StartsWith($"{DateTimeOffset.Now:yyyy}|m1", content);
    }

    /// <summary>
    /// 不带格式的时间戳占位符使用毫秒级默认格式
    /// </summary>
    [Fact]
    public void Format_WithBareTimestampToken_UsesMillisecondDefaultPattern()
    {
        var content = WriteAndRead(
            options => options.LogFormat = "{Timestamp}|{Message}",
            logger => logger.LogInformation("m1"));

        var stamp = content.Split('|')[0];

        // 不比对具体时刻，只校验默认格式串本身没有被换掉
        Assert.True(
            DateTime.TryParseExact(stamp, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.CurrentCulture, DateTimeStyles.None, out _),
            $"默认时间戳格式不符合 yyyy-MM-dd HH:mm:ss.fff，实际为：{stamp}");
    }

    /// <summary>
    /// 分类占位符输出日志分类名
    /// </summary>
    [Fact]
    public void Format_WithCategoryToken_WritesCategoryName()
    {
        var content = WriteAndRead(
            options => options.LogFormat = "{Category}|{Message}",
            logger => logger.LogInformation("m1"));

        Assert.StartsWith(Category + "|m1", content);
    }

    /// <summary>
    /// 消息占位符渲染的是格式化后的消息而非原始模板
    /// </summary>
    [Fact]
    public void Format_WithMessageToken_WritesFormattedMessage()
    {
        var content = WriteAndRead(
            options => options.LogFormat = "{Message}",
            logger => logger.LogInformation("hello {Name} #{Index}", "world", 7));

        Assert.StartsWith("hello world #7", content);
    }

    /// <summary>
    /// 模板尾部多余空白会被裁掉
    /// </summary>
    [Fact]
    public void Format_WithTrailingWhitespaceInTemplate_TrimsLineEnd()
    {
        var content = WriteAndRead(
            options => options.LogFormat = "{Message}     ",
            logger => logger.LogInformation("m1"));

        Assert.Equal("m1" + Environment.NewLine, content);
    }

    /// <summary>
    /// 无异常时换行占位符渲染为空，不产生空行
    /// </summary>
    /// <remarks>
    /// 默认模板形如 "...{Message}{NewLine}{Exception}"，若无异常时仍然吐出换行，
    /// 每条正常日志后面都会多一个空行，日志体积和可读性都会明显变差。
    /// </remarks>
    [Fact]
    public void Format_WithoutException_NewLineTokenRendersEmpty()
    {
        var content = WriteAndRead(
            options => options.LogFormat = "{Message}{NewLine}{Exception}",
            logger => logger.LogInformation("m1"));

        Assert.Equal("m1" + Environment.NewLine, content);
    }

    /// <summary>
    /// 有异常且模板含异常占位符时，异常紧跟在消息之后单独成行且只出现一次
    /// </summary>
    [Fact]
    public void Format_WithExceptionToken_RendersExceptionOnceAfterMessage()
    {
        var exception = new InvalidOperationException("boom-token");

        var content = WriteAndRead(
            options => options.LogFormat = "{Message}{NewLine}{Exception}",
            logger => logger.LogError(exception, "m1"));

        var lines = content.Split(Environment.NewLine);
        Assert.Equal("m1", lines[0]);
        Assert.Contains("boom-token", content, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(content, "boom-token"));
    }

    /// <summary>
    /// 模板不含异常占位符时，异常兜底追加到条目末尾
    /// </summary>
    /// <remarks>
    /// 这是「配错模板也不会丢异常」的保底路径，丢失异常堆栈是排障成本最高的一类回归。
    /// </remarks>
    [Fact]
    public void Format_WhenTemplateHasNoExceptionToken_AppendsExceptionAsFallback()
    {
        var exception = new InvalidOperationException("boom-token");

        var content = WriteAndRead(
            options => options.LogFormat = "{Message}",
            logger => logger.LogError(exception, "m1"));

        var lines = content.Split(Environment.NewLine);
        Assert.Equal("m1", lines[0]);
        Assert.Contains("boom-token", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// 模板含作用域占位符时，作用域就地渲染
    /// </summary>
    [Fact]
    public void Format_WithScopeToken_RendersScopeInPlace()
    {
        var content = WriteAndRead(
            options => options.LogFormat = "{Scope}|{Message}",
            logger =>
            {
                using (logger.BeginScope("tenant=t9"))
                {
                    logger.LogInformation("m1");
                }
            });

        Assert.Equal("tenant=t9|m1" + Environment.NewLine, content);
    }

    /// <summary>
    /// 没有作用域时作用域占位符渲染为空
    /// </summary>
    [Fact]
    public void Format_WithScopeTokenButNoActiveScope_RendersEmpty()
    {
        var content = WriteAndRead(
            options => options.LogFormat = "{Scope}|{Message}",
            logger => logger.LogInformation("m1"));

        Assert.Equal("|m1" + Environment.NewLine, content);
    }

    /// <summary>
    /// 模板不含作用域占位符时，作用域兜底追加到条目末尾
    /// </summary>
    [Fact]
    public void Format_WhenTemplateHasNoScopeToken_AppendsScopeAsFallback()
    {
        var content = WriteAndRead(
            options => options.LogFormat = "{Message}",
            logger =>
            {
                using (logger.BeginScope("tenant=t9"))
                {
                    logger.LogInformation("m1");
                }
            });

        Assert.Equal("m1 [Scope: tenant=t9]" + Environment.NewLine, content);
    }

    /// <summary>
    /// 嵌套作用域按由外向内的顺序拼接
    /// </summary>
    [Fact]
    public void BuildScopeText_WithNestedScopes_JoinsFromOuterToInner()
    {
        var content = WriteAndRead(
            options => options.LogFormat = "{Scope}|{Message}",
            logger =>
            {
                using (logger.BeginScope("outer"))
                using (logger.BeginScope("inner"))
                {
                    logger.LogInformation("m1");
                }
            });

        Assert.Equal("outer => inner|m1" + Environment.NewLine, content);
    }

    /// <summary>
    /// 内层作用域释放后不再参与拼接
    /// </summary>
    [Fact]
    public void BuildScopeText_AfterInnerScopeDisposed_RestoresOuterScopeOnly()
    {
        var content = WriteAndRead(
            options => options.LogFormat = "{Scope}|{Message}",
            logger =>
            {
                using (logger.BeginScope("outer"))
                {
                    using (logger.BeginScope("inner"))
                    {
                        logger.LogInformation("m1");
                    }

                    logger.LogInformation("m2");
                }
            });

        var lines = content.Split(Environment.NewLine);
        Assert.Equal("outer => inner|m1", lines[0]);
        Assert.Equal("outer|m2", lines[1]);
    }

    /// <summary>
    /// 键值对形态的作用域渲染成 key=value 列表
    /// </summary>
    [Fact]
    public void BuildScopeText_WithKeyValueScope_RendersPairList()
    {
        List<KeyValuePair<string, object?>> scope =
        [
            new("TenantId", "t1"),
            new("UserId", "u2")
        ];

        var content = WriteAndRead(
            options => options.LogFormat = "{Scope}|{Message}",
            logger =>
            {
                using (logger.BeginScope(scope))
                {
                    logger.LogInformation("m1");
                }
            });

        Assert.Equal("TenantId=t1, UserId=u2|m1" + Environment.NewLine, content);
    }

    /// <summary>
    /// 模板型作用域渲染具名参数，不泄漏内部的原始模板键
    /// </summary>
    /// <remarks>
    /// BeginScope("Order {OrderId}", 42) 的状态里额外带一个 {OriginalFormat} 键，
    /// 那是日志基础设施的内部字段，落到日志正文里属于噪声。
    /// </remarks>
    [Fact]
    public void BuildScopeText_WithTemplateScope_DropsOriginalFormatKey()
    {
        var content = WriteAndRead(
            options => options.LogFormat = "{Scope}|{Message}",
            logger =>
            {
                using (logger.BeginScope("Order {OrderId}", 42))
                {
                    logger.LogInformation("m1");
                }
            });

        Assert.Equal("OrderId=42|m1" + Environment.NewLine, content);
    }

    /// <summary>
    /// 关闭作用域开关后，作用域既不就地渲染也不兜底追加
    /// </summary>
    [Fact]
    public void BuildScopeText_WhenIncludeScopesDisabled_SuppressesBothRenderPaths()
    {
        var content = WriteAndRead(
            options =>
            {
                options.IncludeScopes = false;
                options.LogFormat = "{Scope}|{Message}";
            },
            logger =>
            {
                using (logger.BeginScope("tenant=t9"))
                {
                    logger.LogInformation("m1");
                }
            });

        Assert.Equal("|m1" + Environment.NewLine, content);
        Assert.DoesNotContain("Scope:", content, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string content, string token)
    {
        return content.Split(token).Length - 1;
    }

    private string WriteAndRead(Action<XiHanFileLoggerOptions> configure, Action<ILogger> write)
    {
        // 文件名不含 "{Date}" 也不含 "-."，落盘路径与配置路径一致，便于逐字比对内容
        var filePath = Path.Combine(_root, "app.log");
        var options = new XiHanFileLoggerOptions
        {
            FilePath = filePath,
            MinLevel = LogLevel.Trace
        };
        configure(options);

        using (var provider = new XiHanFileLoggerProvider(Microsoft.Extensions.Options.Options.Create(options)))
        {
            write(provider.CreateLogger(Category));
        }

        return File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
    }
}
