// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using XiHan.Framework.Logging.Options;

namespace XiHan.Framework.Logging.Tests.Options;

/// <summary>
/// 曦寒日志总配置选项测试
/// </summary>
/// <remarks>
/// 这个选项类没有 Validate()，它的契约全部落在「默认值」和「能否从配置节绑定」两件事上：
/// 默认值一旦漂移，所有未显式配置的宿主行为都会跟着变；配置节键名或属性名一旦改动，
/// appsettings 里的既有配置会静默失效。因此两者都必须锁住。
/// </remarks>
public class XiHanLoggingOptionsTests
{
    /// <summary>
    /// 配置节名称是外部 appsettings 的锚点，不允许漂移
    /// </summary>
    [Fact]
    public void SectionName_IsStableConfigurationKey()
    {
        Assert.Equal("XiHan:Logging", XiHanLoggingOptions.SectionName);
    }

    /// <summary>
    /// 未做任何配置时的默认值
    /// </summary>
    [Fact]
    public void Defaults_MatchDocumentedContract()
    {
        var options = new XiHanLoggingOptions();

        Assert.True(options.IsEnabled);
        Assert.Equal(LogLevel.Information, options.MinimumLevel);
        Assert.Equal("Logs/xihan-.log", options.FileOutputPath);
        Assert.Equal(RollingInterval.Day, options.RollingInterval);
        Assert.Equal<int?>(31, options.RetainedFileCountLimit);
        Assert.Equal<long?>(100L * 1024 * 1024, options.FileSizeLimitBytes);
        Assert.True(options.RollOnFileSizeLimit);
        Assert.True(options.EnableStructuredLogging);
        Assert.True(options.EnableAsyncLogging);
        Assert.Equal(10000, options.AsyncBufferSize);
        Assert.False(options.BlockWhenFull);
        Assert.True(options.EnableRequestLogging);
    }

    /// <summary>
    /// 性能计数器默认关闭
    /// </summary>
    /// <remarks>
    /// 单独拎出来断言：XiHanLogger.LogPerformance 以此开关为总闸，
    /// 默认值一旦翻成 true，所有宿主会凭空多出一批性能日志。
    /// </remarks>
    [Fact]
    public void EnablePerformanceCounters_DefaultsToDisabled()
    {
        Assert.False(new XiHanLoggingOptions().EnablePerformanceCounters);
    }

    /// <summary>
    /// 控制台模板保留链路标识占位符
    /// </summary>
    [Fact]
    public void ConsoleOutputTemplate_KeepsTraceIdPlaceholder()
    {
        var template = new XiHanLoggingOptions().ConsoleOutputTemplate;

        Assert.Contains("{TraceId}", template, StringComparison.Ordinal);
        Assert.Contains("{Message:lj}", template, StringComparison.Ordinal);
        Assert.Contains("{Exception}", template, StringComparison.Ordinal);
    }

    /// <summary>
    /// 文件模板同时保留链路与跨度标识占位符
    /// </summary>
    [Fact]
    public void FileOutputTemplate_KeepsTraceIdAndSpanIdPlaceholders()
    {
        var template = new XiHanLoggingOptions().FileOutputTemplate;

        Assert.Contains("{TraceId}", template, StringComparison.Ordinal);
        Assert.Contains("{SpanId}", template, StringComparison.Ordinal);
        Assert.Contains("{SourceContext}", template, StringComparison.Ordinal);
    }

    /// <summary>
    /// 请求日志默认排除探活与静态资源路径
    /// </summary>
    [Theory]
    [InlineData("/health")]
    [InlineData("/metrics")]
    [InlineData("/favicon.ico")]
    [InlineData("/swagger")]
    public void RequestLoggingExcludePaths_ContainsNoiseEndpoints(string path)
    {
        Assert.Contains(path, new XiHanLoggingOptions().RequestLoggingExcludePaths);
    }

    /// <summary>
    /// 上下文属性与过滤器默认是空的可写字典
    /// </summary>
    [Fact]
    public void ContextPropertiesAndFilters_DefaultToEmptyWritableDictionaries()
    {
        var options = new XiHanLoggingOptions();

        Assert.Empty(options.ContextProperties);
        Assert.Empty(options.Filters);

        options.ContextProperties["Tenant"] = "t1";
        options.Filters["Microsoft"] = LogLevel.Warning;

        Assert.Equal("t1", options.ContextProperties["Tenant"]);
        Assert.Equal(LogLevel.Warning, options.Filters["Microsoft"]);
    }

    /// <summary>
    /// 配置节能整体绑定到选项对象
    /// </summary>
    [Fact]
    public void Bind_FromConfigurationSection_MapsScalarPropertiesAndFilters()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:Logging:IsEnabled"] = "false",
                ["XiHan:Logging:MinimumLevel"] = "Warning",
                ["XiHan:Logging:FileOutputPath"] = "custom/app-.log",
                ["XiHan:Logging:RollingInterval"] = "Hour",
                ["XiHan:Logging:RetainedFileCountLimit"] = "7",
                ["XiHan:Logging:FileSizeLimitBytes"] = "2048",
                ["XiHan:Logging:RollOnFileSizeLimit"] = "false",
                ["XiHan:Logging:EnableStructuredLogging"] = "false",
                ["XiHan:Logging:EnableAsyncLogging"] = "false",
                ["XiHan:Logging:AsyncBufferSize"] = "64",
                ["XiHan:Logging:BlockWhenFull"] = "true",
                ["XiHan:Logging:EnablePerformanceCounters"] = "true",
                ["XiHan:Logging:EnableRequestLogging"] = "false",
                ["XiHan:Logging:Filters:Microsoft"] = "Error"
            })
            .Build();

        var options = configuration.GetSection(XiHanLoggingOptions.SectionName).Get<XiHanLoggingOptions>();

        Assert.NotNull(options);
        Assert.False(options.IsEnabled);
        Assert.Equal(LogLevel.Warning, options.MinimumLevel);
        Assert.Equal("custom/app-.log", options.FileOutputPath);
        Assert.Equal(RollingInterval.Hour, options.RollingInterval);
        Assert.Equal<int?>(7, options.RetainedFileCountLimit);
        Assert.Equal<long?>(2048L, options.FileSizeLimitBytes);
        Assert.False(options.RollOnFileSizeLimit);
        Assert.False(options.EnableStructuredLogging);
        Assert.False(options.EnableAsyncLogging);
        Assert.Equal(64, options.AsyncBufferSize);
        Assert.True(options.BlockWhenFull);
        Assert.True(options.EnablePerformanceCounters);
        Assert.False(options.EnableRequestLogging);
        Assert.Equal(LogLevel.Error, options.Filters["Microsoft"]);
    }

    /// <summary>
    /// 可空的保留数量与大小上限允许显式置空表示不限
    /// </summary>
    [Fact]
    public void NullableLimits_AcceptNullToMeanUnlimited()
    {
        var options = new XiHanLoggingOptions
        {
            RetainedFileCountLimit = null,
            FileSizeLimitBytes = null
        };

        Assert.Null(options.RetainedFileCountLimit);
        Assert.Null(options.FileSizeLimitBytes);
    }
}
