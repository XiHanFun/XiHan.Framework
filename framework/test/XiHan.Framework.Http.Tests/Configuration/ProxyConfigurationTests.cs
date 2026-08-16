// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Http.Configuration;
using XiHan.Framework.Http.Enums;

namespace XiHan.Framework.Http.Tests.Configuration;

/// <summary>
/// <see cref="ProxyConfiguration"/> 的地址构建与校验测试
/// </summary>
public class ProxyConfigurationTests
{
    /// <summary>
    /// 代理类型映射到正确的地址方案
    /// </summary>
    /// <param name="type">代理类型</param>
    /// <param name="expectedAddress">期望的完整地址</param>
    [Theory]
    [InlineData(ProxyType.Http, "http://127.0.0.1:8080")]
    [InlineData(ProxyType.Https, "https://127.0.0.1:8080")]
    [InlineData(ProxyType.Socks4, "socks4://127.0.0.1:8080")]
    [InlineData(ProxyType.Socks4A, "socks4a://127.0.0.1:8080")]
    [InlineData(ProxyType.Socks5, "socks5://127.0.0.1:8080")]
    public void GetProxyAddress_MapsProxyTypeToScheme(ProxyType type, string expectedAddress)
    {
        var proxy = new ProxyConfiguration { Host = "127.0.0.1", Port = 8080, Type = type };

        Assert.Equal(expectedAddress, proxy.GetProxyAddress());
    }

    /// <summary>
    /// 带认证信息的代理地址包含编码后的凭据
    /// </summary>
    [Fact]
    public void GetProxyAddress_WithCredentials_IncludesEscapedCredentials()
    {
        var proxy = new ProxyConfiguration
        {
            Host = "proxy.example.com",
            Port = 8080,
            Type = ProxyType.Http,
            Username = "user@example.com",
            Password = "p@ss:word"
        };

        var address = proxy.GetProxyAddress();

        Assert.Equal("http://user%40example.com:p%40ss%3Aword@proxy.example.com:8080", address);
    }

    /// <summary>
    /// 校验拒绝空主机或越界端口
    /// </summary>
    [Fact]
    public void Validate_RejectsInvalidHostOrPort()
    {
        Assert.False(new ProxyConfiguration { Host = string.Empty, Port = 8080 }.Validate());
        Assert.False(new ProxyConfiguration { Host = "   ", Port = 8080 }.Validate());
        Assert.False(new ProxyConfiguration { Host = "127.0.0.1", Port = 0 }.Validate());
        Assert.False(new ProxyConfiguration { Host = "127.0.0.1", Port = 65536 }.Validate());
        Assert.True(new ProxyConfiguration { Host = "127.0.0.1", Port = 8080 }.Validate());
    }
}
