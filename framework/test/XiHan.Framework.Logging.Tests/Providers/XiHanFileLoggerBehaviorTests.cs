// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Logging.Options;
using XiHan.Framework.Logging.Providers;

namespace XiHan.Framework.Logging.Tests.Providers;

/// <summary>
/// 文件日志提供器落盘行为测试
/// </summary>
/// <remarks>
/// 覆盖格式化之外的部分：落盘路径推导、级别过滤、切档开关、编码回退与作用域提供器替换。
/// 全部使用独占临时目录，析构时递归清理，不依赖工作目录。
/// </remarks>
public sealed class XiHanFileLoggerBehaviorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// 构造函数，准备独占的临时目录
    /// </summary>
    public XiHanFileLoggerBehaviorTests()
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
    /// 提供器别名是配置文件按提供器过滤日志的键，不允许漂移
    /// </summary>
    [Fact]
    public void ProviderAlias_IsXiHanFile()
    {
        var alias = typeof(XiHanFileLoggerProvider).GetCustomAttribute<ProviderAliasAttribute>();

        Assert.NotNull(alias);
        Assert.Equal("XiHanFile", alias.Alias);
    }

    /// <summary>
    /// 路径中的日期占位符大小写不敏感
    /// </summary>
    [Theory]
    [InlineData("a-{Date}.log")]
    [InlineData("a-{date}.log")]
    [InlineData("a-{DATE}.log")]
    public void ResolveFilePath_WithDateToken_IsCaseInsensitive(string fileNameTemplate)
    {
        var logger = CreateLogger(options => options.FilePath = Path.Combine(_root, fileNameTemplate));

        logger.LogInformation("m1");

        var expected = Path.Combine(_root, $"a-{DateTimeOffset.Now:yyyyMMdd}.log");
        Assert.True(File.Exists(expected), $"未按日期占位符落盘，目录内容：{string.Join(", ", Directory.GetFiles(_root))}");
    }

    /// <summary>
    /// 短横线结尾的命名约定会被自动补上日期
    /// </summary>
    /// <remarks>
    /// 默认配置就是 "Logs/xihan-.log" 这种形态，若该约定失效，默认部署下所有日志会挤进同一个文件。
    /// </remarks>
    [Fact]
    public void ResolveFilePath_WithTrailingDashConvention_InsertsDateBeforeExtension()
    {
        var logger = CreateLogger(options => options.FilePath = Path.Combine(_root, "xihan-.log"));

        logger.LogInformation("m1");

        var expected = Path.Combine(_root, $"xihan-{DateTimeOffset.Now:yyyyMMdd}.log");
        Assert.True(File.Exists(expected), $"未按短横线约定落盘，目录内容：{string.Join(", ", Directory.GetFiles(_root))}");
    }

    /// <summary>
    /// 普通文件名原样使用，不做任何日期改写
    /// </summary>
    [Fact]
    public void ResolveFilePath_WithPlainFileName_KeepsNameUnchanged()
    {
        var filePath = Path.Combine(_root, "plain.log");
        var logger = CreateLogger(options => options.FilePath = filePath);

        logger.LogInformation("m1");

        var file = Assert.Single(Directory.GetFiles(_root));
        Assert.Equal(filePath, file);
    }

    /// <summary>
    /// 路径两端的空白会被裁掉
    /// </summary>
    [Fact]
    public void ResolveFilePath_WithSurroundingWhitespace_TrimsBeforeUse()
    {
        var filePath = Path.Combine(_root, "trim.log");
        var logger = CreateLogger(options => options.FilePath = "  " + filePath + "  ");

        logger.LogInformation("m1");

        var file = Assert.Single(Directory.GetFiles(_root));
        Assert.Equal(filePath, file);
    }

    /// <summary>
    /// 目标目录不存在时自动逐级创建
    /// </summary>
    [Fact]
    public void Log_WhenTargetDirectoryMissing_CreatesDirectoryTree()
    {
        var filePath = Path.Combine(_root, "nested", "deep", "app.log");
        var logger = CreateLogger(options => options.FilePath = filePath);

        logger.LogInformation("m1");

        Assert.True(File.Exists(filePath));
    }

    /// <summary>
    /// 低于最小级别的日志既不启用也不落盘
    /// </summary>
    [Fact]
    public void IsEnabled_BelowMinLevel_DisablesLevelAndSkipsWrite()
    {
        var filePath = Path.Combine(_root, "level.log");
        var logger = CreateLogger(options =>
        {
            options.FilePath = filePath;
            options.MinLevel = LogLevel.Warning;
            options.LogFormat = "{Message}";
        });

        Assert.False(logger.IsEnabled(LogLevel.Trace));
        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
        Assert.True(logger.IsEnabled(LogLevel.Critical));

        logger.LogInformation("dropped");
        Assert.False(File.Exists(filePath));

        logger.LogWarning("kept");
        Assert.Equal("kept" + Environment.NewLine, File.ReadAllText(filePath));
    }

    /// <summary>
    /// None 级别永远不应被视为启用
    /// </summary>
    /// <remarks>
    /// None 是「不写任何日志」的哨兵值，框架自带的提供器一律返回 false。
    /// 当前实现只做 logLevel >= MinLevel 的数值比较，None 反而是最大值，会被判定为启用。
    /// 本用例按正确语义断言，红灯对应报告中的疑似缺陷条目。
    /// </remarks>
    [Fact]
    public void IsEnabled_WhenLogLevelNone_ReturnsFalse()
    {
        var logger = CreateLogger(options => options.FilePath = Path.Combine(_root, "none.log"));

        Assert.False(logger.IsEnabled(LogLevel.None));
    }

    /// <summary>
    /// 大小上限为非正数时永不切档
    /// </summary>
    [Fact]
    public void EnsureFileSizeLimit_WhenLimitNotPositive_NeverArchives()
    {
        var filePath = Path.Combine(_root, "nolimit.log");
        var logger = CreateLogger(options =>
        {
            options.FilePath = filePath;
            options.FileSizeLimit = 0;
            options.LogFormat = "{Message}";
        });

        for (var i = 0; i < 50; i++)
        {
            logger.LogInformation(new string('x', 200));
        }

        Assert.Single(Directory.GetFiles(_root));
    }

    /// <summary>
    /// 超过大小上限后归档旧文件并继续写新文件
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void EnsureFileSizeLimit_WhenExceeded_ArchivesAndKeepsWritingToActiveFile()
    {
        var filePath = Path.Combine(_root, "roll.log");
        var logger = CreateLogger(options =>
        {
            options.FilePath = filePath;
            options.FileSizeLimit = 64;
            options.RetainedFileCountLimit = 10;
            options.LogFormat = "{Message}";
        });

        for (var i = 0; i < 5; i++)
        {
            logger.LogInformation(new string('x', 100));
        }

        Assert.True(File.Exists(filePath));

        // 归档文件命名形如 roll.<yyyyMMddHHmmssfff>.log，与活动文件同目录
        var archives = Directory.GetFiles(_root, "roll.*.log")
            .Where(path => !string.Equals(path, filePath, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(archives);
    }

    /// <summary>
    /// 编码名非法时回退到 UTF-8 而不是抛错
    /// </summary>
    [Fact]
    public void ResolveEncoding_WithUnknownEncodingName_FallsBackToUtf8()
    {
        var filePath = Path.Combine(_root, "encoding.log");
        var logger = CreateLogger(options =>
        {
            options.FilePath = filePath;
            options.Encoding = "not-a-real-encoding";
            options.LogFormat = "{Message}";
        });

        logger.LogInformation("中文日志");

        Assert.Equal("中文日志" + Environment.NewLine, File.ReadAllText(filePath));
    }

    /// <summary>
    /// 默认编码下中文可原样往返
    /// </summary>
    [Fact]
    public void ResolveEncoding_WithDefaultEncoding_RoundTripsChineseText()
    {
        var filePath = Path.Combine(_root, "chinese.log");
        var logger = CreateLogger(options =>
        {
            options.FilePath = filePath;
            options.LogFormat = "{Message}";
        });

        logger.LogInformation("订单已创建：编号 A-001");

        Assert.Contains("订单已创建：编号 A-001", File.ReadAllText(filePath), StringComparison.Ordinal);
    }

    /// <summary>
    /// 传入 null 的作用域提供器时回退到内置实现而不是崩溃
    /// </summary>
    [Fact]
    public void SetScopeProvider_WithNull_FallsBackToBuiltInProvider()
    {
        var filePath = Path.Combine(_root, "nullscope.log");
        var options = new XiHanFileLoggerOptions
        {
            FilePath = filePath,
            MinLevel = LogLevel.Trace,
            LogFormat = "{Scope}|{Message}"
        };

        using var provider = new XiHanFileLoggerProvider(Microsoft.Extensions.Options.Options.Create(options));
        provider.SetScopeProvider(null!);
        var logger = provider.CreateLogger("Cat");

        using (logger.BeginScope("s1"))
        {
            logger.LogInformation("m1");
        }

        Assert.Equal("s1|m1" + Environment.NewLine, File.ReadAllText(filePath));
    }

    /// <summary>
    /// 外部作用域提供器接管后，容器压入的作用域同样生效
    /// </summary>
    [Fact]
    public void SetScopeProvider_WithExternalProvider_UsesScopesPushedOutside()
    {
        var filePath = Path.Combine(_root, "extscope.log");
        var options = new XiHanFileLoggerOptions
        {
            FilePath = filePath,
            MinLevel = LogLevel.Trace,
            LogFormat = "{Scope}|{Message}"
        };

        var external = new LoggerExternalScopeProvider();
        using var provider = new XiHanFileLoggerProvider(Microsoft.Extensions.Options.Options.Create(options));
        provider.SetScopeProvider(external);
        var logger = provider.CreateLogger("Cat");

        using (external.Push("request=r1"))
        {
            logger.LogInformation("m1");
        }

        Assert.Equal("request=r1|m1" + Environment.NewLine, File.ReadAllText(filePath));
    }

    /// <summary>
    /// 不同分类的日志器共享同一份选项并写入同一个文件
    /// </summary>
    [Fact]
    public void CreateLogger_WithDifferentCategories_WritesEachCategoryIntoSameFile()
    {
        var filePath = Path.Combine(_root, "multi.log");
        var options = new XiHanFileLoggerOptions
        {
            FilePath = filePath,
            MinLevel = LogLevel.Trace,
            LogFormat = "{Category}|{Message}"
        };

        using var provider = new XiHanFileLoggerProvider(Microsoft.Extensions.Options.Options.Create(options));
        provider.CreateLogger("Alpha").LogInformation("m1");
        provider.CreateLogger("Beta").LogInformation("m2");

        var lines = File.ReadAllText(filePath).Split(Environment.NewLine);
        Assert.Equal("Alpha|m1", lines[0]);
        Assert.Equal("Beta|m2", lines[1]);
    }

    private ILogger CreateLogger(Action<XiHanFileLoggerOptions> configure)
    {
        var options = new XiHanFileLoggerOptions
        {
            FilePath = Path.Combine(_root, "default.log"),
            MinLevel = LogLevel.Trace
        };
        configure(options);

        var provider = new XiHanFileLoggerProvider(Microsoft.Extensions.Options.Options.Create(options));
        return provider.CreateLogger("Cat");
    }
}
