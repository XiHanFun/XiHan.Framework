// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Web.Gateway.Options;

namespace XiHan.Framework.Web.Gateway.Tests;

/// <summary>
/// 网关配置选项测试
/// </summary>
/// <remarks>
/// 该类型没有 Validate 方法，公共契约就是「默认值语义 + 配置节路径 + 可绑定性」三件事：
/// 默认值决定了使用方什么都不配时的行为，配置节路径写错会让整段配置静默失效。
/// </remarks>
public class XiHanGatewayOptionsTests
{
    /// <summary>
    /// 配置节路径保持稳定
    /// </summary>
    /// <remarks>
    /// 路径写在 appsettings.json 里，改一个字母就会让所有已有配置静默回落到默认值。
    /// </remarks>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:Web:Gateway", XiHanGatewayOptions.SectionName);
    }

    /// <summary>
    /// 新实例使用保守的默认值
    /// </summary>
    [Fact]
    public void NewInstance_UsesSafeDefaults()
    {
        var options = new XiHanGatewayOptions();

        Assert.True(options.EnableGrayRouting);
        Assert.True(options.EnableRequestTracing);
        // 限流与熔断会主动拒绝流量，默认必须关闭，只能由使用方显式打开
        Assert.False(options.EnableRateLimiting);
        Assert.False(options.EnableCircuitBreaker);
        Assert.Equal(30, options.RequestTimeoutSeconds);
    }

    /// <summary>
    /// 集合类默认值是空集合而不是 null
    /// </summary>
    [Fact]
    public void NewInstance_InitializesCollections()
    {
        var options = new XiHanGatewayOptions();

        Assert.NotNull(options.AllowedOrigins);
        Assert.Empty(options.AllowedOrigins);
        Assert.NotNull(options.GlobalHeaders);
        Assert.Empty(options.GlobalHeaders);
    }

    /// <summary>
    /// 不同实例之间不共享集合实例
    /// </summary>
    /// <remarks>
    /// 集合默认值一旦写成静态共享实例，多租户/多命名选项之间会互相污染，且极难排查。
    /// </remarks>
    [Fact]
    public void NewInstance_DoesNotShareCollectionsBetweenInstances()
    {
        var first = new XiHanGatewayOptions();
        var second = new XiHanGatewayOptions();

        first.AllowedOrigins.Add("https://a.example.com");
        first.GlobalHeaders["X-From"] = "gateway";

        Assert.Empty(second.AllowedOrigins);
        Assert.Empty(second.GlobalHeaders);
        Assert.NotSame(first.AllowedOrigins, second.AllowedOrigins);
        Assert.NotSame(first.GlobalHeaders, second.GlobalHeaders);
    }

    /// <summary>
    /// 按约定的配置节绑定后覆盖对应值，未出现的键保持默认
    /// </summary>
    [Fact]
    public void Bind_FromSectionName_OverridesOnlyPresentKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:Web:Gateway:EnableGrayRouting"] = "false",
                ["XiHan:Web:Gateway:RequestTimeoutSeconds"] = "5",
                ["XiHan:Web:Gateway:AllowedOrigins:0"] = "https://a.example.com",
                ["XiHan:Web:Gateway:AllowedOrigins:1"] = "https://b.example.com",
                ["XiHan:Web:Gateway:GlobalHeaders:X-From"] = "gateway"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<XiHanGatewayOptions>(configuration.GetSection(XiHanGatewayOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<XiHanGatewayOptions>>().Value;

        Assert.False(options.EnableGrayRouting);
        Assert.Equal(5, options.RequestTimeoutSeconds);
        Assert.Equal(new[] { "https://a.example.com", "https://b.example.com" }, options.AllowedOrigins);
        Assert.Equal("gateway", options.GlobalHeaders["X-From"]);
        // 配置里没写的键必须保持类型默认值，而不是被绑定成 false/0
        Assert.True(options.EnableRequestTracing);
        Assert.False(options.EnableRateLimiting);
    }

    /// <summary>
    /// 空配置节绑定后完全保持默认值
    /// </summary>
    [Fact]
    public void Bind_FromMissingSection_KeepsAllDefaults()
    {
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.Configure<XiHanGatewayOptions>(configuration.GetSection(XiHanGatewayOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<XiHanGatewayOptions>>().Value;

        Assert.True(options.EnableGrayRouting);
        Assert.True(options.EnableRequestTracing);
        Assert.False(options.EnableRateLimiting);
        Assert.False(options.EnableCircuitBreaker);
        Assert.Equal(30, options.RequestTimeoutSeconds);
        Assert.Empty(options.AllowedOrigins);
        Assert.Empty(options.GlobalHeaders);
    }
}
