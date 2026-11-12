// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Kernel.Tests.Fixtures;

namespace XiHan.Framework.Kernel.Tests.Security;

/// <summary>
/// 出站地址策略（RFC-0008「出站地址策略」、ADR-0117）
/// </summary>
/// <remarks>
/// 连接相关的用例都走真实的套接字与 <see cref="HttpClient"/>：要证明的正是请求有没有到达监听端口，替身证明不了。主机名由注入的解析器给出，用来造 DNS 重绑定与混合应答
/// </remarks>
public sealed class OutboundAddressPolicyTests
{
    [Theory]
    // 回环
    [InlineData("127.0.0.1")]
    [InlineData("127.255.255.254")]
    [InlineData("::1")]
    // 私网
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456::1")]
    // 链路本地与各家云的元数据地址
    [InlineData("169.254.169.254")]
    [InlineData("169.254.0.23")]
    [InlineData("fe80::1")]
    [InlineData("fe80::1%3")]
    [InlineData("100.100.100.200")]
    [InlineData("192.0.0.192")]
    [InlineData("fd00:ec2::254")]
    // 其余不可公网路由的地址
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("224.0.0.1")]
    [InlineData("ff02::1")]
    [InlineData("240.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("198.18.0.1")]
    [InlineData("203.0.113.1")]
    [InlineData("fec0::1")]
    [InlineData("2001:db8::1")]
    [InlineData("3fff::1")]
    [InlineData("2001:0:4136:e378:8000:63bf:3fff:fdd2")]
    // 内嵌 IPv4 的 IPv6 形式
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("::127.0.0.1")]
    [InlineData("64:ff9b::7f00:1")]
    [InlineData("64:ff9b::a9fe:a9fe")]
    [InlineData("64:ff9b:1::1")]
    [InlineData("2002:7f00:1::")]
    [InlineData("2002:a9fe:a9fe::1")]
    public void NonPublicAddress_IsDeniedByDefault(string address)
        => Assert.False(Policy(new OutboundAddressOptions()).IsAllowed(IPAddress.Parse(address)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("9.255.255.255")]
    [InlineData("11.0.0.0")]
    [InlineData("172.15.255.255")]
    [InlineData("172.32.0.0")]
    [InlineData("192.167.255.255")]
    [InlineData("192.169.0.0")]
    [InlineData("100.63.255.255")]
    [InlineData("100.128.0.0")]
    [InlineData("223.255.255.255")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("::ffff:8.8.8.8")]
    [InlineData("64:ff9b::808:808")]
    [InlineData("2002:808:808::")]
    public void PublicAddress_IsAllowedByDefault(string address)
        => Assert.True(Policy(new OutboundAddressOptions()).IsAllowed(IPAddress.Parse(address)));

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("::ffff:127.0.0.1", true)]
    [InlineData("0.0.0.0", false)]
    [InlineData("10.0.0.1", false)]
    [InlineData("169.254.169.254", false)]
    public void AllowLoopback_OpensOnlyLoopback(string address, bool allowed)
        => Assert.Equal(allowed, Policy(new OutboundAddressOptions { AllowLoopback = true }).IsAllowed(IPAddress.Parse(address)));

    [Theory]
    [InlineData("10.0.0.1", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("192.168.1.1", true)]
    [InlineData("fd12::1", true)]
    [InlineData("::ffff:10.0.0.1", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("100.100.100.200", false)]
    [InlineData("fd00:ec2::254", false)]
    // GCP 与 Oracle 的 IPv6 元数据地址同样在 fc00::/7 里：修之前开了私网就能连（ADR-0206）
    [InlineData("fd20:ce::254", false)]
    [InlineData("fd00:c1::a9fe:a9fe", false)]
    public void AllowPrivateNetwork_LeavesLoopbackAndMetadataDenied(string address, bool allowed)
        => Assert.Equal(allowed, Policy(new OutboundAddressOptions { AllowPrivateNetwork = true }).IsAllowed(IPAddress.Parse(address)));

    [Theory]
    [InlineData("169.254.169.254", true)]
    [InlineData("::ffff:169.254.169.254", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("169.254.169.253", false)]
    [InlineData("10.2.0.1", false)]
    public void AllowedNetworks_OpenExactlyThoseNetworks(string address, bool allowed)
        => Assert.Equal(allowed, Policy(new OutboundAddressOptions { AllowedNetworks = [IPNetwork.Parse("169.254.169.254/32"), IPNetwork.Parse("10.1.0.0/16")] }).IsAllowed(IPAddress.Parse(address)));

    [Theory]
    [InlineData("http://127.0.0.1:{0}/")]
    [InlineData("http://127.1:{0}/")]
    [InlineData("http://2130706433:{0}/")]
    [InlineData("http://0x7f.0.0.1:{0}/")]
    [InlineData("http://017700000001:{0}/")]
    [InlineData("http://localhost:{0}/")]
    [InlineData("http://[::1]:{0}/")]
    [InlineData("http://[::ffff:127.0.0.1]:{0}/")]
    public async Task LoopbackInAnySpelling_IsDenied_AndNothingReachesIt(string format)
    {
        using var server = new LoopbackHttpServer();
        using var client = Client(Policy(new OutboundAddressOptions()));

        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(string.Format(CultureInfo.InvariantCulture, format, server.Port)));

        Assert.IsType<OutboundAddressDeniedException>(thrown.InnerException);
        Assert.Equal(0, server.Accepted);
    }

    [Fact]
    public async Task AllowedAddress_IsReached()
    {
        using var server = new LoopbackHttpServer();
        using var client = Client(Policy(new OutboundAddressOptions { AllowLoopback = true }));

        using var response = await client.GetAsync($"http://127.0.0.1:{server.Port}/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, server.Accepted);
    }

    [Fact]
    public async Task AllowedHost_SkipsTheAddressCheck_ButOnlyForThatName()
    {
        using var server = new LoopbackHttpServer();
        var options = new OutboundAddressOptions { AllowedHosts = ["Gateway.Internal"] };
        using var client = Client(Policy(options, ("gateway.internal", ["127.0.0.1"]), ("other.internal", ["127.0.0.1"])));

        using var response = await client.GetAsync($"http://gateway.internal:{server.Port}/");
        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync($"http://other.internal:{server.Port}/"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.IsType<OutboundAddressDeniedException>(thrown.InnerException);
        Assert.Equal(1, server.Accepted);
    }

    [Fact]
    public async Task RedirectToADeniedAddress_IsDenied_AndTheTargetIsNotReached()
    {
        using var target = new LoopbackHttpServer();
        using var front = new LoopbackHttpServer($"http://127.0.0.1:{target.Port}/");
        using var client = Client(Policy(new OutboundAddressOptions { AllowedHosts = ["front.test"] }, ("front.test", ["127.0.0.1"])));

        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync($"http://front.test:{front.Port}/"));

        Assert.IsType<OutboundAddressDeniedException>(thrown.InnerException);
        Assert.Equal(1, front.Accepted);
        Assert.Equal(0, target.Accepted);
    }

    [Fact]
    public async Task RedirectToAnAllowedAddress_IsFollowed()
    {
        using var target = new LoopbackHttpServer();
        using var front = new LoopbackHttpServer($"http://127.0.0.1:{target.Port}/");
        using var client = Client(Policy(new OutboundAddressOptions { AllowLoopback = true }));

        using var response = await client.GetAsync($"http://127.0.0.1:{front.Port}/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, target.Accepted);
    }

    [Fact]
    public async Task DnsRebinding_IsCaughtOnTheNextConnection()
    {
        using var server = new LoopbackHttpServer();
        // 第一次解析到允许的地址，第二次换成不允许的：只查一次的实现这里就放过去了
        var answers = new Queue<IPAddress[]>([[IPAddress.Parse("127.0.0.1")], [IPAddress.Parse("127.0.0.2")]]);
        var policy = new OutboundAddressPolicy(new OutboundAddressOptions { AllowedNetworks = [IPNetwork.Parse("127.0.0.1/32")] }, NullLoggerFactory.Instance, (_, _) => Task.FromResult(answers.Dequeue()));
        using var client = Client(policy);

        using var first = await client.GetAsync($"http://rebind.test:{server.Port}/");
        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync($"http://rebind.test:{server.Port}/"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.IsType<OutboundAddressDeniedException>(thrown.InnerException);
        Assert.Equal(1, server.Accepted);
    }

    [Fact]
    public async Task MixedAnswer_ConnectsOnlyToTheAllowedAddresses()
    {
        using var server = new LoopbackHttpServer();
        var logs = new LogCapture();
        var policy = new OutboundAddressPolicy(new OutboundAddressOptions { AllowedNetworks = [IPNetwork.Parse("127.0.0.1/32")] }, logs, Resolver(("mixed.test", ["10.0.0.1", "127.0.0.1"])));
        using var client = Client(policy);

        using var response = await client.GetAsync($"http://mixed.test:{server.Port}/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var skipped = Assert.Single(logs.Entries, static entry => entry.Category == typeof(OutboundAddressPolicy).FullName);
        Assert.Equal(LogLevel.Debug, skipped.Level);
        Assert.Contains("10.0.0.1", skipped.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnreachableAddress_FallsThroughToTheNextOne()
    {
        using var server = new LoopbackHttpServer();
        // 服务只听 IPv4：::1 连不上，换下一个
        using var client = Client(Policy(new OutboundAddressOptions { AllowLoopback = true }, ("both.test", ["::1", "127.0.0.1"])));

        using var response = await client.GetAsync($"http://both.test:{server.Port}/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EveryAddressUnreachable_ReportsAConnectionFailure_NotADenial()
    {
        using var closed = new LoopbackHttpServer();
        var port = closed.Port;
        closed.Dispose();
        using var client = Client(Policy(new OutboundAddressOptions { AllowLoopback = true }));

        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync($"http://127.0.0.1:{port}/"));

        Assert.IsNotType<OutboundAddressDeniedException>(thrown.InnerException);
    }

    [Fact]
    public async Task Denial_LogsTheResolvedAddresses_ButTheExceptionDoesNotCarryThem()
    {
        var logs = new LogCapture();
        var policy = new OutboundAddressPolicy(new OutboundAddressOptions(), logs, Resolver(("erp.test", ["10.1.2.3"])));
        using var client = Client(policy);

        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://erp.test:8443/"));

        var denied = Assert.IsType<OutboundAddressDeniedException>(thrown.InnerException);
        Assert.Equal(("erp.test", 8443), (denied.Host, denied.Port));
        Assert.DoesNotContain("10.1.2.3", thrown.ToString(), StringComparison.Ordinal);
        var logged = Assert.Single(logs.Entries, static entry => entry.Category == typeof(OutboundAddressPolicy).FullName);
        Assert.Equal(LogLevel.Warning, logged.Level);
        Assert.Contains("erp.test:8443", logged.Message, StringComparison.Ordinal);
        Assert.Contains("10.1.2.3", logged.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_WhileResolving_IsThrownAsCancellation()
    {
        var resolving = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var policy = new OutboundAddressPolicy(new OutboundAddressOptions(), NullLoggerFactory.Instance, async (_, cancellationToken) =>
        {
            resolving.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return [];
        });
        using var client = Client(policy);
        using var cancellation = new CancellationTokenSource();

        var request = client.GetAsync("http://slow.test/", cancellation.Token);
        await resolving.Task;
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

    [Fact]
    public void Apply_TurnsOffTheProxy_AndIsIdempotent()
    {
        var policy = Policy(new OutboundAddressOptions());
        using var handler = new SocketsHttpHandler { Proxy = new WebProxy("http://proxy.test:3128"), UseProxy = true };

        policy.Apply(handler);
        var callback = handler.ConnectCallback;
        policy.Apply(handler);

        Assert.False(handler.UseProxy);
        Assert.NotNull(callback);
        Assert.Same(callback, handler.ConnectCallback);
    }

    [Fact]
    public void Apply_RefusesAHandlerWithAnotherConnectCallback()
    {
        using var handler = new SocketsHttpHandler { ConnectCallback = static (_, _) => throw new NotSupportedException() };

        Assert.Throws<InvalidOperationException>(() => Policy(new OutboundAddressOptions()).Apply(handler));
    }

    [Fact]
    public void Options_AreReadFromConfiguration()
    {
        var options = OutboundAddressOptions.FromConfiguration(Hosts.Configuration(
            ("XiHan:Security:Outbound:AllowLoopback", "true"),
            ("XiHan:Security:Outbound:AllowPrivateNetwork", "false"),
            ("XiHan:Security:Outbound:AllowedNetworks:0", "10.1.0.0/16"),
            ("XiHan:Security:Outbound:AllowedNetworks:1", "fd12::/16"),
            ("XiHan:Security:Outbound:AllowedHosts:0", "minio")));

        Assert.True(options.AllowLoopback);
        Assert.False(options.AllowPrivateNetwork);
        Assert.Equal([IPNetwork.Parse("10.1.0.0/16"), IPNetwork.Parse("fd12::/16")], options.AllowedNetworks);
        Assert.Equal(["minio"], options.AllowedHosts);
    }

    [Fact]
    public void Options_DefaultToPublicAddressesOnly()
    {
        var options = OutboundAddressOptions.FromConfiguration(Hosts.Configuration());

        Assert.False(options.AllowLoopback);
        Assert.False(options.AllowPrivateNetwork);
        Assert.Empty(options.AllowedNetworks);
        Assert.Empty(options.AllowedHosts);
    }

    [Theory]
    [InlineData("XiHan:Security:Outbound:AllowLoopback", "yes")]
    [InlineData("XiHan:Security:Outbound:AllowPrivateNetwork", "1")]
    [InlineData("XiHan:Security:Outbound:AllowedNetworks:0", "10.0.0.1")]
    [InlineData("XiHan:Security:Outbound:AllowedNetworks:0", "10.0.0.1/8")]
    [InlineData("XiHan:Security:Outbound:AllowedHosts:0", "http://minio:9000")]
    [InlineData("XiHan:Security:Outbound:AllowedHosts:0", "minio:9000")]
    public void Options_InvalidValues_NameTheKey(string key, string value)
    {
        var thrown = Assert.Throws<ArgumentException>(() => OutboundAddressOptions.FromConfiguration(Hosts.Configuration((key, value))));

        Assert.Contains(key, thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_RegistersThePolicy_FromConfiguration()
    {
        var (provider, _) = Hosts.BuildApp(static _ => { }, Hosts.Configuration(("XiHan:Security:Outbound:AllowLoopback", "true")));
        using (provider)
        {
            Assert.True(provider.GetRequiredService<OutboundAddressPolicy>().IsAllowed(IPAddress.Loopback));
        }
    }

    [Fact]
    public void Host_InvalidOptions_FailTheBuild()
        => Assert.Throws<ArgumentException>(static () => Hosts.BuildApp(static _ => { }, Hosts.Configuration(("XiHan:Security:Outbound:AllowedNetworks:0", "not-a-network"))));

    [Fact]
    public void Host_KeepsOptionsTheApplicationRegistered()
    {
        var (provider, _) = Hosts.BuildApp(static builder => builder.Services.AddSingleton(new OutboundAddressOptions { AllowLoopback = true }));
        using (provider)
        {
            Assert.True(provider.GetRequiredService<OutboundAddressPolicy>().IsAllowed(IPAddress.Loopback));
        }
    }

    /// <summary>
    /// 建策略，主机名按 <paramref name="names"/> 解析（字面量地址原样返回）
    /// </summary>
    private static OutboundAddressPolicy Policy(OutboundAddressOptions options, params (string Host, string[] Addresses)[] names)
        => new(options, NullLoggerFactory.Instance, Resolver(names));

    /// <summary>
    /// 按表解析主机名
    /// </summary>
    /// <remarks>
    /// 表里没有的走系统解析，字面量地址与 <c>localhost</c> 都由系统处理
    /// </remarks>
    private static Func<string, CancellationToken, Task<IPAddress[]>> Resolver(params (string Host, string[] Addresses)[] names)
        => (host, cancellationToken) =>
        {
            foreach (var (name, addresses) in names)
            {
                if (string.Equals(name, host, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(addresses.Select(IPAddress.Parse).ToArray());
                }
            }

            return Dns.GetHostAddressesAsync(host, cancellationToken);
        };

    /// <summary>
    /// 建一个过策略的客户端
    /// </summary>
    private static HttpClient Client(OutboundAddressPolicy policy)
    {
        var handler = new SocketsHttpHandler();
        policy.Apply(handler);
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }
}
