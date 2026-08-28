// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using System.Text;
using XiHan.Framework.Logging.Options;
using XiHan.Framework.Logging.Providers;

namespace XiHan.Framework.Logging.Tests.Providers;

/// <summary>
/// 文件日志提供器编码前导码测试
/// </summary>
/// <remarks>
/// 锁住一条修复：默认的 "UTF-8" 原来经 Encoding.GetEncoding 拿到的是带 BOM 的实例，首次创建日志文件会写入
/// EF BB BF；而编码名为空或非法的两条回退路径用的是 UTF8Encoding(false)，同一个提供器产出的文件字节前缀不一致。
/// 这里一律读原始字节断言，不能用 File.ReadAllText——它会按 BOM 自动剥掉前导码，正好把要验的东西吃掉。
/// 全部使用独占临时目录，析构时递归清理。
/// </remarks>
public sealed class XiHanFileLoggerEncodingBomTests : IDisposable
{
    private static readonly byte[] Utf8Preamble = [0xEF, 0xBB, 0xBF];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "XiHanTests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// 构造函数，准备独占的临时目录
    /// </summary>
    public XiHanFileLoggerEncodingBomTests()
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
    /// 不显式配置编码时首次落盘不写 UTF-8 前导码
    /// </summary>
    /// <remarks>
    /// 这是修复前必然失败的场景：选项默认值就是 "UTF-8"，宿主什么都不配也会拿到带 BOM 的日志文件。
    /// </remarks>
    [Fact]
    public void Log_WithDefaultEncodingOption_WritesFileWithoutUtf8Preamble()
    {
        var filePath = Path.Combine(_root, "default.log");
        var logger = CreateLogger(options => options.FilePath = filePath);

        logger.LogInformation("m1");

        Assert.False(StartsWithUtf8Preamble(File.ReadAllBytes(filePath)));
    }

    /// <summary>
    /// 各种编码名写法与回退路径产出的字节前缀一致
    /// </summary>
    /// <remarks>
    /// 关键在于「一致」：可识别的 UTF-8 别名、空白名、非法名三条路径过去分成带 BOM 与不带 BOM 两派，
    /// 采集端按行读到的首行因此时有时无地多出三个字节。
    /// </remarks>
    [Theory]
    [InlineData("UTF-8")]
    [InlineData("utf-8")]
    [InlineData("Utf-8")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-encoding")]
    public void Log_WithAnyUtf8OrFallbackEncodingName_WritesFileWithoutUtf8Preamble(string encodingName)
    {
        var filePath = Path.Combine(_root, "variant.log");
        var logger = CreateLogger(options =>
        {
            options.FilePath = filePath;
            options.Encoding = encodingName;
        });

        logger.LogInformation("m1");

        var bytes = File.ReadAllBytes(filePath);
        Assert.False(StartsWithUtf8Preamble(bytes));
        Assert.Equal("m1" + Environment.NewLine, new UTF8Encoding(false).GetString(bytes));
    }

    /// <summary>
    /// 去掉前导码后中文内容仍然原样可读
    /// </summary>
    /// <remarks>
    /// 修复只应影响文件开头的三个字节，正文编码不能跟着变。
    /// </remarks>
    [Fact]
    public void Log_WithDefaultEncodingOption_KeepsChineseTextIntact()
    {
        var filePath = Path.Combine(_root, "chinese.log");
        var logger = CreateLogger(options => options.FilePath = filePath);

        logger.LogInformation("订单已创建：编号 A-001");

        var bytes = File.ReadAllBytes(filePath);
        Assert.False(StartsWithUtf8Preamble(bytes));
        Assert.Equal("订单已创建：编号 A-001" + Environment.NewLine, new UTF8Encoding(false).GetString(bytes));
    }

    /// <summary>
    /// 连续写入多条日志时逐行可切且没有多余前导码
    /// </summary>
    [Fact]
    public void Log_MultipleEntries_KeepsEveryLineParsable()
    {
        var filePath = Path.Combine(_root, "lines.log");
        var logger = CreateLogger(options => options.FilePath = filePath);

        logger.LogInformation("m1");
        logger.LogInformation("m2");

        var bytes = File.ReadAllBytes(filePath);
        Assert.False(StartsWithUtf8Preamble(bytes));
        var lines = new UTF8Encoding(false)
            .GetString(bytes)
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(new[] { "m1", "m2" }, lines);
    }

    /// <summary>
    /// 非 UTF-8 的编码不受影响，仍按其自身编码落盘
    /// </summary>
    /// <remarks>
    /// 反例：修复只把 UTF-8 收敛到不带 BOM 的实例，UTF-16 这类编码的前导码是其自身可解析性所必需的，不能一并抹掉。
    /// 用 EndsWith 断言正文，这样无论实现是否写出前导码都只检验「内容确实是 UTF-16」这一点。
    /// </remarks>
    [Fact]
    public void Log_WithUtf16EncodingName_StillWritesUtf16()
    {
        var filePath = Path.Combine(_root, "utf16.log");
        var logger = CreateLogger(options =>
        {
            options.FilePath = filePath;
            options.Encoding = "utf-16";
        });

        logger.LogInformation("m1");

        var bytes = File.ReadAllBytes(filePath);
        Assert.False(StartsWithUtf8Preamble(bytes));
        Assert.EndsWith("m1" + Environment.NewLine, Encoding.Unicode.GetString(bytes), StringComparison.Ordinal);
    }

    private static bool StartsWithUtf8Preamble(byte[] bytes)
    {
        return bytes.Length >= Utf8Preamble.Length
            && bytes[0] == Utf8Preamble[0]
            && bytes[1] == Utf8Preamble[1]
            && bytes[2] == Utf8Preamble[2];
    }

    private ILogger CreateLogger(Action<XiHanFileLoggerOptions> configure)
    {
        var options = new XiHanFileLoggerOptions
        {
            FilePath = Path.Combine(_root, "fallback.log"),
            MinLevel = LogLevel.Trace,
            LogFormat = "{Message}"
        };
        configure(options);

        var provider = new XiHanFileLoggerProvider(Microsoft.Extensions.Options.Options.Create(options));
        return provider.CreateLogger("Cat");
    }
}
