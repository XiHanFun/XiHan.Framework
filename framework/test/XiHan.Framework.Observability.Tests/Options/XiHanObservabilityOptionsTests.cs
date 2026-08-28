// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Observability.Options;

namespace XiHan.Framework.Observability.Tests.Options;

/// <summary>
/// 可观测性配置项测试
/// </summary>
/// <remarks>
/// 该选项类没有 Validate()，语义全在默认值与配置绑定上：
/// 默认必须保持「装配即孤儿」（Enabled=false），配置节名是 appsettings 的对外约定不能漂移；
/// 采样率的合法区间由装配处 Math.Clamp 兜底，选项本身不做校验，这里按现状锁定。
/// </remarks>
public class XiHanObservabilityOptionsTests
{
    /// <summary>
    /// 配置节名是 appsettings 的对外约定，不允许漂移
    /// </summary>
    [Fact]
    public void SectionName_AsConfigurationContract_IsStable()
    {
        Assert.Equal("XiHan:Observability", XiHanObservabilityOptions.SectionName);
    }

    /// <summary>
    /// 默认值：总开关关闭，指标与日志导出关闭，链路追踪开关默认打开但受总开关约束
    /// </summary>
    [Fact]
    public void Constructor_Default_KeepsModuleInert()
    {
        var options = new XiHanObservabilityOptions();

        Assert.False(options.Enabled);
        Assert.Equal("XiHan.App", options.ServiceName);
        Assert.Null(options.ServiceVersion);
        Assert.True(options.EnableTracing);
        Assert.False(options.EnableMetrics);
        Assert.False(options.EnableLogging);
        Assert.Equal(1.0d, options.SamplingRatio);
        Assert.Null(options.OtlpEndpoint);
        Assert.False(options.ConsoleExporter);
        Assert.NotNull(options.AdditionalSources);
        Assert.Empty(options.AdditionalSources);
    }

    /// <summary>
    /// 额外源列表默认可写，且不同实例互不共享
    /// </summary>
    [Fact]
    public void AdditionalSources_OnTwoInstances_AreIndependentMutableLists()
    {
        var first = new XiHanObservabilityOptions();
        var second = new XiHanObservabilityOptions();

        first.AdditionalSources.Add("App.Custom");

        Assert.NotSame(first.AdditionalSources, second.AdditionalSources);
        Assert.Single(first.AdditionalSources);
        Assert.Empty(second.AdditionalSources);
    }

    /// <summary>
    /// 配置节内的全部键都能绑定到对应属性
    /// </summary>
    [Fact]
    public void Bind_WithFullSection_MapsEveryProperty()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["XiHan:Observability:Enabled"] = "true",
            ["XiHan:Observability:ServiceName"] = "demo-service",
            ["XiHan:Observability:ServiceVersion"] = "2.1.0",
            ["XiHan:Observability:EnableTracing"] = "false",
            ["XiHan:Observability:EnableMetrics"] = "true",
            ["XiHan:Observability:EnableLogging"] = "true",
            ["XiHan:Observability:SamplingRatio"] = "0.25",
            ["XiHan:Observability:OtlpEndpoint"] = "http://localhost:4317",
            ["XiHan:Observability:ConsoleExporter"] = "true",
            ["XiHan:Observability:AdditionalSources:0"] = "App.Custom",
            ["XiHan:Observability:AdditionalSources:1"] = "App.Jobs"
        });

        var options = new XiHanObservabilityOptions();
        configuration.GetSection(XiHanObservabilityOptions.SectionName).Bind(options);

        Assert.True(options.Enabled);
        Assert.Equal("demo-service", options.ServiceName);
        Assert.Equal("2.1.0", options.ServiceVersion);
        Assert.False(options.EnableTracing);
        Assert.True(options.EnableMetrics);
        Assert.True(options.EnableLogging);
        Assert.Equal(0.25d, options.SamplingRatio);
        Assert.Equal("http://localhost:4317", options.OtlpEndpoint);
        Assert.True(options.ConsoleExporter);
        Assert.Equal(2, options.AdditionalSources.Count);
        Assert.Contains("App.Custom", options.AdditionalSources);
        Assert.Contains("App.Jobs", options.AdditionalSources);
    }

    /// <summary>
    /// 配置里没有该节时保持全部默认值
    /// </summary>
    [Fact]
    public void Bind_WithoutSection_KeepsDefaults()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Other:Key"] = "value"
        });

        var options = new XiHanObservabilityOptions();
        configuration.GetSection(XiHanObservabilityOptions.SectionName).Bind(options);

        Assert.False(options.Enabled);
        Assert.Equal("XiHan.App", options.ServiceName);
        Assert.True(options.EnableTracing);
        Assert.Equal(1.0d, options.SamplingRatio);
        Assert.Empty(options.AdditionalSources);
    }

    /// <summary>
    /// 只配置部分键时未配置的属性保留默认值
    /// </summary>
    [Fact]
    public void Bind_WithPartialSection_OnlyOverridesGivenKeys()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["XiHan:Observability:Enabled"] = "true"
        });

        var options = new XiHanObservabilityOptions();
        configuration.GetSection(XiHanObservabilityOptions.SectionName).Bind(options);

        Assert.True(options.Enabled);
        Assert.Equal("XiHan.App", options.ServiceName);
        Assert.Null(options.ServiceVersion);
        Assert.True(options.EnableTracing);
        Assert.False(options.EnableMetrics);
        Assert.Null(options.OtlpEndpoint);
    }

    /// <summary>
    /// 采样率按不变文化解析小数，不受运行环境区域设置影响
    /// </summary>
    [Theory]
    [InlineData("0", 0d)]
    [InlineData("0.1", 0.1d)]
    [InlineData("0.5", 0.5d)]
    [InlineData("1", 1d)]
    public void Bind_SamplingRatio_ParsesWithInvariantCulture(string configured, double expected)
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["XiHan:Observability:SamplingRatio"] = configured
        });

        var options = new XiHanObservabilityOptions();
        configuration.GetSection(XiHanObservabilityOptions.SectionName).Bind(options);

        Assert.Equal(expected, options.SamplingRatio);
    }

    /// <summary>
    /// 选项本身不校验采样率区间，越界值原样保留（由装配处 Math.Clamp 兜底）
    /// </summary>
    [Theory]
    [InlineData(-1d)]
    [InlineData(2d)]
    public void SamplingRatio_OutOfRange_IsStoredVerbatimWithoutValidation(double value)
    {
        var options = new XiHanObservabilityOptions { SamplingRatio = value };

        Assert.Equal(value, options.SamplingRatio);
    }

    /// <summary>
    /// 所有属性均可读写，赋值后原样读回
    /// </summary>
    [Fact]
    public void Properties_WhenAssigned_AreReadBackVerbatim()
    {
        var options = new XiHanObservabilityOptions
        {
            Enabled = true,
            ServiceName = "svc",
            ServiceVersion = "1.2.3",
            EnableTracing = false,
            EnableMetrics = true,
            EnableLogging = true,
            SamplingRatio = 0.75d,
            OtlpEndpoint = "http://otel:4317",
            ConsoleExporter = true,
            AdditionalSources = ["A", "B"]
        };

        Assert.True(options.Enabled);
        Assert.Equal("svc", options.ServiceName);
        Assert.Equal("1.2.3", options.ServiceVersion);
        Assert.False(options.EnableTracing);
        Assert.True(options.EnableMetrics);
        Assert.True(options.EnableLogging);
        Assert.Equal(0.75d, options.SamplingRatio);
        Assert.Equal("http://otel:4317", options.OtlpEndpoint);
        Assert.True(options.ConsoleExporter);
        Assert.Equal(2, options.AdditionalSources.Count);
    }

    /// <summary>
    /// 构造内存配置
    /// </summary>
    private static IConfiguration BuildConfiguration(Dictionary<string, string?> settings)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
