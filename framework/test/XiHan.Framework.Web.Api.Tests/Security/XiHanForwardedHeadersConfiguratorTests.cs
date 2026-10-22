// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using XiHan.Framework.Web.Api.Security;

namespace XiHan.Framework.Web.Api.Tests.Security;

/// <summary>
/// 受信代理配置：转发头只在请求来自受信代理时才被采信，客户端伪造的转发头一律忽略
/// </summary>
/// <remarks>
/// 用框架的配置节装出 <see cref="ForwardedHeadersOptions"/>，再跑 ASP.NET Core 的转发头中间件，断言还原出的连接地址。
/// </remarks>
public class XiHanForwardedHeadersConfiguratorTests
{
    /// <summary>
    /// 来源不受信：伪造的 X-Forwarded-For、X-Real-IP 都被忽略，连接地址不变
    /// </summary>
    [Fact]
    public async Task UntrustedSource_SpoofedHeadersAreIgnored()
    {
        var address = await ResolveAsync([], "203.0.113.9", forwardedFor: "1.2.3.4", realIp: "5.6.7.8");

        Assert.Equal(IPAddress.Parse("203.0.113.9"), address);
    }

    /// <summary>
    /// 缺省只信本机回环：同机 nginx 转发的地址照常还原
    /// </summary>
    [Fact]
    public async Task LoopbackProxy_IsTrustedByDefault()
    {
        var address = await ResolveAsync([], "127.0.0.1", forwardedFor: "198.51.100.7");

        Assert.Equal(IPAddress.Parse("198.51.100.7"), address);
    }

    /// <summary>
    /// 配了受信代理地址：经它转发的真实地址还原出来
    /// </summary>
    [Fact]
    public async Task ConfiguredProxy_RestoresClientAddress()
    {
        var settings = new Dictionary<string, string?> { ["KnownProxies:0"] = "10.0.0.5" };

        Assert.Equal(IPAddress.Parse("198.51.100.7"), await ResolveAsync(settings, "10.0.0.5", forwardedFor: "198.51.100.7"));
        Assert.Equal(IPAddress.Parse("10.0.0.6"), await ResolveAsync(settings, "10.0.0.6", forwardedFor: "198.51.100.7"));
    }

    /// <summary>
    /// 多级代理：按 ForwardLimit 从右往左剥受信代理；左端客户端自己塞的地址剥不到
    /// </summary>
    [Fact]
    public async Task MultipleProxies_HonorForwardLimit()
    {
        var oneHop = new Dictionary<string, string?> { ["KnownNetworks:0"] = "10.0.0.0/8" };
        var twoHops = new Dictionary<string, string?> { ["KnownNetworks:0"] = "10.0.0.0/8", ["ForwardLimit"] = "2" };

        // 客户端 198.51.100.7 → 负载均衡 10.0.0.6 → nginx 10.0.0.5 → 应用
        Assert.Equal(IPAddress.Parse("10.0.0.6"), await ResolveAsync(oneHop, "10.0.0.5", forwardedFor: "198.51.100.7, 10.0.0.6"));
        Assert.Equal(IPAddress.Parse("198.51.100.7"), await ResolveAsync(twoHops, "10.0.0.5", forwardedFor: "198.51.100.7, 10.0.0.6"));

        // 客户端在头里预先塞了 1.1.1.1：取最右边那个不受信的地址
        Assert.Equal(IPAddress.Parse("198.51.100.7"), await ResolveAsync(twoHops, "10.0.0.5", forwardedFor: "1.1.1.1, 198.51.100.7"));
    }

    /// <summary>
    /// IPv4 映射地址与 IPv6：受信代理按地址比较，IPv6 网段同样可配
    /// </summary>
    [Fact]
    public async Task IPv6AndMappedAddresses_AreMatched()
    {
        var ipv4Proxy = new Dictionary<string, string?> { ["KnownProxies:0"] = "10.0.0.5" };
        var ipv6Network = new Dictionary<string, string?> { ["KnownNetworks:0"] = "2001:db8::/32" };

        Assert.Equal(IPAddress.Parse("198.51.100.7"), await ResolveAsync(ipv4Proxy, "::ffff:10.0.0.5", forwardedFor: "198.51.100.7"));
        Assert.Equal(IPAddress.Parse("2001:db8:1::7"), await ResolveAsync(ipv6Network, "2001:db8::5", forwardedFor: "2001:db8:1::7"));
        Assert.Equal(IPAddress.Parse("2001:db9::5"), await ResolveAsync(ipv6Network, "2001:db9::5", forwardedFor: "2001:db8:1::7"));
    }

    /// <summary>
    /// 配错了启动即失败：不是合法地址、网段主机位不为 0（不悄悄截成更大的网段）、网段没写前缀长度、ForwardLimit 小于 1
    /// </summary>
    [Theory]
    [InlineData("KnownProxies:0", "10.0.0.256")]
    [InlineData("KnownNetworks:0", "10.0.0.1/8")]
    [InlineData("KnownNetworks:0", "2001:db8::5/32")]
    [InlineData("KnownNetworks:0", "10.0.0.0")]
    [InlineData("KnownNetworks:0", "proxy.local")]
    [InlineData("ForwardLimit", "0")]
    public void InvalidSettings_FailFast(string key, string value)
    {
        using var provider = BuildServices(new Dictionary<string, string?> { [key] = value });

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value);
    }

    private static async Task<IPAddress?> ResolveAsync(Dictionary<string, string?> settings, string remoteAddress, string forwardedFor, string? realIp = null)
    {
        using var provider = BuildServices(settings);
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>();
        IPAddress? resolved = null;
        var middleware = new ForwardedHeadersMiddleware(
            context =>
            {
                resolved = context.Connection.RemoteIpAddress;
                return Task.CompletedTask;
            },
            NullLoggerFactory.Instance,
            options);

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
        httpContext.Request.Headers["X-Forwarded-For"] = forwardedFor;
        if (realIp is not null)
        {
            httpContext.Request.Headers["X-Real-IP"] = realIp;
        }

        await middleware.Invoke(httpContext);
        return resolved;
    }

    private static ServiceProvider BuildServices(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(item => $"{XiHanForwardedHeadersOptions.SectionName}:{item.Key}", item => item.Value))
            .Build();
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<XiHanForwardedHeadersOptions>(configuration.GetSection(XiHanForwardedHeadersOptions.SectionName));
        services.AddSingleton<IConfigureOptions<ForwardedHeadersOptions>, XiHanForwardedHeadersConfigurator>();
        return services.BuildServiceProvider();
    }
}
