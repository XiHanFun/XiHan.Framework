// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using XiHan.Framework.Configuration.Abstractions;

namespace XiHan.Framework.Configuration.Nacos.Tests;

/// <summary>
/// 凭据与令牌怎样送到 Nacos、失败原因里有什么（ADR-0220）
/// </summary>
/// <remarks>
/// 令牌不进查询串（会进访问日志）；带凭据的非本机 http 要显式允许；失败原因不带响应正文
/// </remarks>
public sealed class NacosTransportTests
{
    [Fact]
    public async Task Token_TravelsInTheAuthorizationHeader_NotTheQueryString()
    {
        using var handler = new NacosStubHandler { Content = """{"Greeting":"hello"}""", ContentMd5 = "c2d231ffe3913f930fc713f76c0bfbf1", SendContentMd5Header = true };
        using var http = new HttpClient(handler, disposeHandler: false);
        using var client = new NacosConfigurationClient(new NacosOptions { ServerAddresses = ["https://nacos.test:8848"], UserName = "nacos", Password = "secret" }, http, TimeProvider.System);

        var document = (await client.FetchAsync("orders", null, CancellationToken.None)).ValueOrThrow().ValueOrThrow();
        Assert.False((await client.FetchAsync("orders", document.Version, CancellationToken.None)).ValueOrThrow().HasValue);

        // 取配置与监听都带 Bearer 头，查询串里没有令牌
        Assert.Equal(2, handler.Credentials.Count);
        Assert.All(handler.Credentials, static sent =>
        {
            Assert.Equal("Bearer stub-token", sent.Authorization);
            Assert.DoesNotContain("accessToken", sent.Query, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData("http://nacos.test:8848", false, true)]
    [InlineData("http://nacos.test:8848", true, false)]
    [InlineData("https://nacos.test:8848", false, false)]
    [InlineData("http://localhost:8848", false, false)]
    [InlineData("http://127.0.0.1:8848", false, false)]
    public void Credentials_OverNonLoopbackHttp_RequireAnExplicitOptIn(string address, bool allowInsecureHttp, bool rejected)
    {
        using var http = new HttpClient();
        var options = new NacosOptions { ServerAddresses = [address], UserName = "nacos", Password = "secret", AllowInsecureHttp = allowInsecureHttp };
        if (rejected)
        {
            var exception = Assert.Throws<ArgumentException>(() => new NacosConfigurationClient(options, http, TimeProvider.System));
            Assert.Contains(address, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
        }
        else
        {
            using var client = new NacosConfigurationClient(options, http, TimeProvider.System);
        }

        // 没有凭据时 http 照常
        using var anonymous = new NacosConfigurationClient(new NacosOptions { ServerAddresses = [address] }, http, TimeProvider.System);
    }

    [Fact]
    public async Task FailureReason_CarriesTheStatus_ButNotTheResponseBody()
    {
        using var handler = new NacosStubHandler { FailFetch = (HttpStatusCode.Forbidden, "user nacos has no permission on dataId orders, token=leaked") };
        using var http = new HttpClient(handler, disposeHandler: false);
        using var client = new NacosConfigurationClient(new NacosOptions { ServerAddresses = ["https://nacos.test:8848"] }, http, TimeProvider.System);

        var failed = await client.FetchAsync("orders", null, CancellationToken.None);

        Assert.Equal(ConfigurationErrors.RemoteUnavailable, failed.Error?.Code);
        var reason = failed.Error!.Details!["reason"]!.ToString()!;
        Assert.Contains("403", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("leaked", reason, StringComparison.Ordinal);
    }
}
