// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Logging.Options;

namespace XiHan.Framework.Logging.Tests.Options;

/// <summary>
/// 控制台日志提供器选项测试
/// </summary>
/// <remarks>
/// 控制台提供器把这些开关直接翻译成输出结构（是否打时间戳/级别/分类、是否单行、是否着色），
/// 默认值决定了「什么都不配」时的落地形态，因此逐项锁死。
/// </remarks>
public class XiHanConsoleLoggerOptionsTests
{
    /// <summary>
    /// 配置节名称是外部 appsettings 的锚点，不允许漂移
    /// </summary>
    [Fact]
    public void SectionName_IsStableConfigurationKey()
    {
        Assert.Equal("XiHan:Logging:Console", XiHanConsoleLoggerOptions.SectionName);
    }

    /// <summary>
    /// 未做任何配置时的默认值
    /// </summary>
    [Fact]
    public void Defaults_MatchDocumentedContract()
    {
        var options = new XiHanConsoleLoggerOptions();

        Assert.Equal(LogLevel.Information, options.MinLevel);
        Assert.True(options.IncludeScopes);
        Assert.True(options.EnableColors);
        Assert.False(options.EnableRainbow);
        Assert.Equal("[{Timestamp:HH:mm:ss}] [{Level}] {Category}: {Message}{Exception}", options.LogFormat);
        Assert.Equal("HH:mm:ss", options.TimestampFormat);
        Assert.True(options.ShowCategoryName);
        Assert.True(options.ShowTimestamp);
        Assert.True(options.ShowLogLevel);
        Assert.False(options.SingleLine);
        Assert.False(options.UseStdErrorForErrors);
    }

    /// <summary>
    /// 默认配色覆盖全部六个可写级别
    /// </summary>
    [Theory]
    [InlineData(LogLevel.Trace, ConsoleColor.Gray)]
    [InlineData(LogLevel.Debug, ConsoleColor.DarkGray)]
    [InlineData(LogLevel.Information, ConsoleColor.Green)]
    [InlineData(LogLevel.Warning, ConsoleColor.Yellow)]
    [InlineData(LogLevel.Error, ConsoleColor.Red)]
    [InlineData(LogLevel.Critical, ConsoleColor.DarkRed)]
    public void LogLevelColors_MapEachWritableLevel(LogLevel level, ConsoleColor expected)
    {
        var colors = new XiHanConsoleLoggerOptions().LogLevelColors;

        Assert.True(colors.TryGetValue(level, out var actual));
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// 默认配色不给 None 级别着色
    /// </summary>
    /// <remarks>
    /// None 不是可写级别，一旦被映射就意味着提供器认为它可以着色输出，属于语义污染。
    /// </remarks>
    [Fact]
    public void LogLevelColors_DoNotCoverNoneLevel()
    {
        var colors = new XiHanConsoleLoggerOptions().LogLevelColors;

        Assert.False(colors.ContainsKey(LogLevel.None));
        Assert.Equal(6, colors.Count);
    }

    /// <summary>
    /// 配色表可被宿主整体替换
    /// </summary>
    [Fact]
    public void LogLevelColors_CanBeReplacedByHost()
    {
        var options = new XiHanConsoleLoggerOptions
        {
            LogLevelColors = new Dictionary<LogLevel, ConsoleColor>
            {
                [LogLevel.Error] = ConsoleColor.Magenta
            }
        };

        Assert.Single(options.LogLevelColors);
        Assert.Equal(ConsoleColor.Magenta, options.LogLevelColors[LogLevel.Error]);
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
                ["XiHan:Logging:Console:MinLevel"] = "Debug",
                ["XiHan:Logging:Console:IncludeScopes"] = "false",
                ["XiHan:Logging:Console:EnableColors"] = "false",
                ["XiHan:Logging:Console:EnableRainbow"] = "true",
                ["XiHan:Logging:Console:LogFormat"] = "{Level}|{Message}",
                ["XiHan:Logging:Console:TimestampFormat"] = "HH:mm",
                ["XiHan:Logging:Console:ShowCategoryName"] = "false",
                ["XiHan:Logging:Console:ShowTimestamp"] = "false",
                ["XiHan:Logging:Console:ShowLogLevel"] = "false",
                ["XiHan:Logging:Console:SingleLine"] = "true",
                ["XiHan:Logging:Console:UseStdErrorForErrors"] = "true"
            })
            .Build();

        var options = configuration.GetSection(XiHanConsoleLoggerOptions.SectionName).Get<XiHanConsoleLoggerOptions>();

        Assert.NotNull(options);
        Assert.Equal(LogLevel.Debug, options.MinLevel);
        Assert.False(options.IncludeScopes);
        Assert.False(options.EnableColors);
        Assert.True(options.EnableRainbow);
        Assert.Equal("{Level}|{Message}", options.LogFormat);
        Assert.Equal("HH:mm", options.TimestampFormat);
        Assert.False(options.ShowCategoryName);
        Assert.False(options.ShowTimestamp);
        Assert.False(options.ShowLogLevel);
        Assert.True(options.SingleLine);
        Assert.True(options.UseStdErrorForErrors);
    }
}
