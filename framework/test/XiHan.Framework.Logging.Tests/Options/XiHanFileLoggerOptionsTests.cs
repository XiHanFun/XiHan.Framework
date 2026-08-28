// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Logging.Options;

namespace XiHan.Framework.Logging.Tests.Options;

/// <summary>
/// 文件日志提供器选项测试
/// </summary>
/// <remarks>
/// 文件提供器直接按这些值决定落盘路径、切档阈值、保留份数与编码，
/// 默认值漂移会在不改任何业务代码的前提下改变磁盘行为，属于必须锁死的契约。
/// </remarks>
public class XiHanFileLoggerOptionsTests
{
    /// <summary>
    /// 配置节名称是外部 appsettings 的锚点，不允许漂移
    /// </summary>
    [Fact]
    public void SectionName_IsStableConfigurationKey()
    {
        Assert.Equal("XiHan:Logging:File", XiHanFileLoggerOptions.SectionName);
    }

    /// <summary>
    /// 未做任何配置时的默认值
    /// </summary>
    [Fact]
    public void Defaults_MatchDocumentedContract()
    {
        var options = new XiHanFileLoggerOptions();

        Assert.Equal("Logs/xihan-.log", options.FilePath);
        Assert.Equal(10L * 1024 * 1024, options.FileSizeLimit);
        Assert.Equal(31, options.RetainedFileCountLimit);
        Assert.Equal(1024, options.BufferSize);
        Assert.Equal(TimeSpan.FromSeconds(1), options.FlushPeriod);
        Assert.Equal(LogLevel.Information, options.MinLevel);
        Assert.True(options.IncludeScopes);
        Assert.Equal("[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] {Category}: {Message}{NewLine}{Exception}", options.LogFormat);
        Assert.True(options.EnableAsyncWrite);
        Assert.Equal("UTF-8", options.Encoding);
    }

    /// <summary>
    /// 默认路径采用「短横线结尾」的按天切分命名约定
    /// </summary>
    /// <remarks>
    /// 文件日志器把文件名里的 "-." 识别为按天切分锚点，默认路径必须与该约定保持一致，
    /// 否则默认配置下所有日志会挤进同一个不带日期的文件。
    /// </remarks>
    [Fact]
    public void DefaultFilePath_UsesDailyRollingNamingConvention()
    {
        var fileName = Path.GetFileName(new XiHanFileLoggerOptions().FilePath);

        Assert.Contains("-.", fileName, StringComparison.Ordinal);
    }

    /// <summary>
    /// 配置节能整体绑定到选项对象
    /// </summary>
    [Fact]
    public void Bind_FromConfigurationSection_MapsScalarProperties()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:Logging:File:FilePath"] = "custom/app-{Date}.log",
                ["XiHan:Logging:File:FileSizeLimit"] = "4096",
                ["XiHan:Logging:File:RetainedFileCountLimit"] = "5",
                ["XiHan:Logging:File:BufferSize"] = "256",
                ["XiHan:Logging:File:FlushPeriod"] = "00:00:05",
                ["XiHan:Logging:File:MinLevel"] = "Error",
                ["XiHan:Logging:File:IncludeScopes"] = "false",
                ["XiHan:Logging:File:LogFormat"] = "{Level}|{Message}",
                ["XiHan:Logging:File:EnableAsyncWrite"] = "false",
                ["XiHan:Logging:File:Encoding"] = "utf-8"
            })
            .Build();

        var options = configuration.GetSection(XiHanFileLoggerOptions.SectionName).Get<XiHanFileLoggerOptions>();

        Assert.NotNull(options);
        Assert.Equal("custom/app-{Date}.log", options.FilePath);
        Assert.Equal(4096L, options.FileSizeLimit);
        Assert.Equal(5, options.RetainedFileCountLimit);
        Assert.Equal(256, options.BufferSize);
        Assert.Equal(TimeSpan.FromSeconds(5), options.FlushPeriod);
        Assert.Equal(LogLevel.Error, options.MinLevel);
        Assert.False(options.IncludeScopes);
        Assert.Equal("{Level}|{Message}", options.LogFormat);
        Assert.False(options.EnableAsyncWrite);
        Assert.Equal("utf-8", options.Encoding);
    }

    /// <summary>
    /// 非正数的大小上限表示不切档
    /// </summary>
    /// <remarks>
    /// 这是文件日志器 EnsureFileSizeLimit 的短路条件，属于对外可配置的语义，保证赋值不被改写。
    /// </remarks>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void FileSizeLimit_AcceptsNonPositiveValueAsUnlimited(long limit)
    {
        var options = new XiHanFileLoggerOptions { FileSizeLimit = limit };

        Assert.Equal(limit, options.FileSizeLimit);
    }
}
