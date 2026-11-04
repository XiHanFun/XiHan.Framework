// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel.Security;

namespace XiHan.Framework.Kernel.Tests.Security;

/// <summary>
/// 不是 HTTP 的连接过出站地址策略（ADR-0126）
/// </summary>
/// <remarks>
/// SMTP 这类协议库接受一个连好的套接字：判定与连接用同一份解析结果，连的就是检查过的地址，规则与 HTTP 路径相同
/// </remarks>
public sealed class OutboundConnectTests
{
    [Fact]
    public async Task AllowedAddress_ReturnsAConnectedSocket()
    {
        using var server = Listen();
        var policy = new OutboundAddressPolicy(new OutboundAddressOptions { AllowLoopback = true }, NullLoggerFactory.Instance);

        using var socket = await policy.ConnectAsync("127.0.0.1", Port(server), TestContext.Current.CancellationToken);

        Assert.True(socket.Connected);
        Assert.Equal(Port(server), ((IPEndPoint)socket.RemoteEndPoint!).Port);
        using var accepted = await server.AcceptSocketAsync(TestContext.Current.CancellationToken);
        Assert.True(accepted.Connected);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("localhost")]
    public async Task DeniedAddress_ThrowsWithoutConnecting(string host)
    {
        using var server = Listen();
        var policy = new OutboundAddressPolicy(new OutboundAddressOptions(), NullLoggerFactory.Instance);

        var denied = await Assert.ThrowsAsync<OutboundAddressDeniedException>(async () => await policy.ConnectAsync(host, Port(server), TestContext.Current.CancellationToken));

        Assert.Contains(host, denied.Message, StringComparison.Ordinal);
        Assert.False(server.Pending());
    }

    [Fact]
    public async Task TheResolvedAddressIsChecked_NotTheName()
    {
        // 一个看起来是公网的名字解析到内网：重绑定就是这样，判定看的是解析结果
        using var server = Listen();
        var policy = Policy(new OutboundAddressOptions(), ["127.0.0.1"]);

        await Assert.ThrowsAsync<OutboundAddressDeniedException>(async () => await policy.ConnectAsync("mail.example.test", Port(server), TestContext.Current.CancellationToken));

        Assert.False(server.Pending());
    }

    [Fact]
    public async Task MixedAnswer_ConnectsOnlyToTheAllowedAddresses()
    {
        using var server = Listen();
        var policy = Policy(new OutboundAddressOptions { AllowedNetworks = [IPNetwork.Parse("127.0.0.1/32")] }, ["10.0.0.1", "127.0.0.1"]);

        using var socket = await policy.ConnectAsync("mail.example.test", Port(server), TestContext.Current.CancellationToken);

        Assert.Equal(IPAddress.Loopback, ((IPEndPoint)socket.RemoteEndPoint!).Address);
    }

    [Fact]
    public async Task AllowedHost_SkipsTheAddressCheck()
    {
        using var server = Listen();
        var policy = Policy(new OutboundAddressOptions { AllowedHosts = ["mail.internal"] }, ["127.0.0.1"]);

        using var socket = await policy.ConnectAsync("MAIL.internal", Port(server), TestContext.Current.CancellationToken);

        Assert.True(socket.Connected);
    }

    [Fact]
    public async Task UnreachableAddress_FallsThroughToTheNextOne()
    {
        // 服务只听 IPv4：::1 连不上，换下一个
        using var server = Listen();
        var policy = Policy(new OutboundAddressOptions { AllowLoopback = true }, ["::1", "127.0.0.1"]);

        using var socket = await policy.ConnectAsync("mail.example.test", Port(server), TestContext.Current.CancellationToken);

        Assert.Equal(IPAddress.Loopback, ((IPEndPoint)socket.RemoteEndPoint!).Address);
    }

    [Fact]
    public async Task EveryAddressUnreachable_IsAConnectionFailure_NotADenial()
    {
        int port;
        using (var closed = Listen())
        {
            port = Port(closed);
        }

        var policy = new OutboundAddressPolicy(new OutboundAddressOptions { AllowLoopback = true }, NullLoggerFactory.Instance);

        await Assert.ThrowsAsync<SocketException>(async () => await policy.ConnectAsync("127.0.0.1", port, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancelledToken_Throws()
    {
        using var server = Listen();
        var policy = new OutboundAddressPolicy(new OutboundAddressOptions { AllowLoopback = true }, NullLoggerFactory.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await policy.ConnectAsync("127.0.0.1", Port(server), new CancellationToken(canceled: true)));
    }

    [Theory]
    [InlineData("", 25)]
    [InlineData(" ", 25)]
    [InlineData("mail.example.test", 0)]
    [InlineData("mail.example.test", 65536)]
    public async Task InvalidArguments_AreRejected(string host, int port)
    {
        var policy = new OutboundAddressPolicy(new OutboundAddressOptions(), NullLoggerFactory.Instance);

        await Assert.ThrowsAnyAsync<ArgumentException>(async () => await policy.ConnectAsync(host, port, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 在回环地址上监听一个随机端口
    /// </summary>
    private static TcpListener Listen()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return listener;
    }

    /// <summary>
    /// 监听的端口
    /// </summary>
    private static int Port(TcpListener listener) => ((IPEndPoint)listener.LocalEndpoint).Port;

    /// <summary>
    /// 建策略，任何主机名都解析成 <paramref name="addresses"/>
    /// </summary>
    private static OutboundAddressPolicy Policy(OutboundAddressOptions options, string[] addresses)
        => new(options, NullLoggerFactory.Instance, (_, _) => Task.FromResult(addresses.Select(IPAddress.Parse).ToArray()));
}
