// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Web.Gateway.Extensions;
using XiHan.Framework.Web.Gateway.Options;

namespace XiHan.Framework.Web.Gateway.Tests;

/// <summary>
/// 网关服务集合扩展测试
/// </summary>
/// <remarks>
/// AddGateway 目前只做一件事：把可选的配置委托登记成 <see cref="XiHanGatewayOptions"/> 的配置源。
/// 用真实的 <see cref="ServiceCollection"/> 注册并解析，验证「链式返回、可选委托、选项确实生效」三点。
/// </remarks>
public class GatewayServiceCollectionExtensionsTests
{
    /// <summary>
    /// 返回同一个服务集合以支持链式调用
    /// </summary>
    [Fact]
    public void AddGateway_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddGateway();

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 传入配置委托时同样返回同一个服务集合
    /// </summary>
    [Fact]
    public void AddGateway_WithConfigureAction_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddGateway(options => options.EnableGrayRouting = false);

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 不传配置委托时不产生任何服务注册
    /// </summary>
    /// <remarks>
    /// 无参调用必须是纯粹的空操作：如果它顺手注册了别的东西，
    /// 使用方就无法用「只调 AddGateway 不配置」来保持默认行为。
    /// </remarks>
    [Fact]
    public void AddGateway_WithoutConfigureAction_RegistersNothing()
    {
        var services = new ServiceCollection();

        services.AddGateway();

        Assert.Empty(services);
    }

    /// <summary>
    /// 传入配置委托时选项被实际应用
    /// </summary>
    [Fact]
    public void AddGateway_WithConfigureAction_AppliesOptions()
    {
        var services = new ServiceCollection();

        services.AddGateway(options =>
        {
            options.EnableGrayRouting = false;
            options.EnableRateLimiting = true;
            options.RequestTimeoutSeconds = 5;
            options.AllowedOrigins.Add("https://a.example.com");
            options.GlobalHeaders["X-From"] = "gateway";
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanGatewayOptions>>().Value;

        Assert.False(options.EnableGrayRouting);
        Assert.True(options.EnableRateLimiting);
        Assert.Equal(5, options.RequestTimeoutSeconds);
        Assert.Contains("https://a.example.com", options.AllowedOrigins);
        Assert.Equal("gateway", options.GlobalHeaders["X-From"]);
        // 委托没碰到的字段保持默认值
        Assert.True(options.EnableRequestTracing);
        Assert.False(options.EnableCircuitBreaker);
    }

    /// <summary>
    /// 多次调用的配置委托按注册顺序依次生效
    /// </summary>
    /// <remarks>
    /// 选项配置是叠加而不是覆盖，后注册的委托能在前一个结果之上继续改。
    /// </remarks>
    [Fact]
    public void AddGateway_CalledTwice_AppliesBothConfigureActionsInOrder()
    {
        var services = new ServiceCollection();

        services.AddGateway(options =>
        {
            options.RequestTimeoutSeconds = 5;
            options.AllowedOrigins.Add("https://a.example.com");
        });
        services.AddGateway(options =>
        {
            options.RequestTimeoutSeconds = 9;
            options.AllowedOrigins.Add("https://b.example.com");
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanGatewayOptions>>().Value;

        Assert.Equal(9, options.RequestTimeoutSeconds);
        Assert.Equal(new[] { "https://a.example.com", "https://b.example.com" }, options.AllowedOrigins);
    }
}
