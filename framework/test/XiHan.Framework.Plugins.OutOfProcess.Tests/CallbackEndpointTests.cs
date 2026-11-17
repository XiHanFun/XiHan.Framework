// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Plugins.OutOfProcess.Callbacks;
using XiHan.Framework.Plugins.OutOfProcess.Processes;
using XiHan.Framework.Plugins.OutOfProcess.Runtime;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.V1;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 宿主回调端点：首选端点加回环 TCP，只给本用户（ADR-0202、ADR-0237）
/// </summary>
/// <remarks>
/// 安全审查 S-18：原先回调套接字直接放在共享的临时目录下。Unix 上套接字在新建的私有目录里（0700），文件 0600；Windows 的命名管道只允许当前用户，由 Kestrel 的选项保证。ADR-0237：以前只有一个端点，Windows 上是命名管道，Python、Java、PHP 的 gRPC 库连不上；现在另有一个回环 TCP 放在最后，两个端点服务同一个 Host 服务、同一套会话令牌
/// </remarks>
public sealed class CallbackEndpointTests
{
    [Fact]
    public async Task Endpoints_ArePreferredFirst_AndEndWithALoopbackTcp()
    {
        await using var server = PluginCallbackServer.Reserve();

        // 修之前只有一项：Windows 上是命名管道，其他系统是 Unix 域套接字
        Assert.Equal(2, server.Endpoints.Count);
        Assert.Equal(OperatingSystem.IsWindows() ? PluginEndpoint.NamedPipe : PluginEndpoint.Unix, server.Endpoints[0].Network);
        var tcp = server.Endpoints[^1];
        Assert.Equal(PluginEndpoint.Tcp, tcp.Network);
        Assert.StartsWith("127.0.0.1:", tcp.Address, StringComparison.Ordinal);
        Assert.NotEqual("127.0.0.1:0", tcp.Address);

        // 写进环境变量的列表读得回同样的端点
        Assert.True(PluginEndpoint.TryParseList(PluginEndpoint.FormatList(server.Endpoints), out var parsed));
        Assert.Equal(server.Endpoints, parsed);
    }

    [Fact]
    public async Task BothEndpoints_ServeTheHostService_WithTheSameSessionTokens()
    {
        var sessions = new PluginSessions();
        await using var server = PluginCallbackServer.Reserve();
        using var runtime = Runtime(server.Endpoints, sessions);
        var token = sessions.Issue(runtime);
        await server.StartAsync(sessions, new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance, CancellationToken.None);

        foreach (var endpoint in server.Endpoints)
        {
            using var channel = PluginChannelFactory.Create(endpoint);
            var client = new Host.HostClient(channel);

            // 令牌认得出才走到权限：这个运行时的清单没有声明 capability:kernel.pipeline
            var denied = await Assert.ThrowsAsync<RpcException>(async () => await client.InvokeAsync(new InvokeRequest { Operation = "pings.create" }, Headers(token), DateTime.UtcNow.AddSeconds(10)));
            Assert.Equal(StatusCode.PermissionDenied, denied.StatusCode);

            var anonymous = await Assert.ThrowsAsync<RpcException>(async () => await client.InvokeAsync(new InvokeRequest { Operation = "pings.create" }, deadline: DateTime.UtcNow.AddSeconds(10)));
            Assert.Equal(StatusCode.Unauthenticated, anonymous.StatusCode);
        }
    }

    [Fact]
    public async Task TwoSessionTokenValues_AreUnauthenticated_EvenWhenOneIsRight()
    {
        var sessions = new PluginSessions();
        await using var server = PluginCallbackServer.Reserve();
        using var runtime = Runtime(server.Endpoints, sessions);
        var token = sessions.Issue(runtime);
        await server.StartAsync(sessions, new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance, CancellationToken.None);
        using var channel = PluginChannelFactory.Create(server.Endpoints[^1]);
        var client = new Host.HostClient(channel);

        foreach (var tokens in new[] { new[] { "wrong", token }, [token, "wrong"], [token, token] })
        {
            var headers = new Metadata();
            foreach (var value in tokens)
            {
                headers.Add(PluginProtocol.SessionMetadataKey, value);
            }

            // 修之前 GetValue 只看最后一个值：错的在前、对的在后就过了
            var refused = await Assert.ThrowsAsync<RpcException>(async () => await client.InvokeAsync(new InvokeRequest { Operation = "pings.create" }, headers, DateTime.UtcNow.AddSeconds(10)));
            Assert.Equal(StatusCode.Unauthenticated, refused.StatusCode);
        }
    }

    [Fact]
    public async Task UnixSocket_LivesInAPrivateDirectory_AndIsRemovedWithIt()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows uses a named pipe restricted to the current user.");
            return;
        }

        string directory;
        await using (var server = PluginCallbackServer.Reserve())
        {
            var endpoint = server.Endpoints[0];
            directory = Path.GetDirectoryName(endpoint.Address)!;
            await server.StartAsync(new PluginSessions(), new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance, CancellationToken.None);

            // 修之前套接字直接在临时目录下，目录是所有用户共享的 1777
            Assert.NotEqual(Path.GetFullPath(Path.GetTempPath()).TrimEnd('/'), directory);
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(directory));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(endpoint.Address));
        }

        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task ExistingDirectory_IsNotReused()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows uses a named pipe restricted to the current user.");
            return;
        }

        await using var server = PluginCallbackServer.Reserve();
        var directory = Path.GetDirectoryName(server.Endpoints[0].Address)!;
        Directory.CreateDirectory(directory);
        try
        {
            // 别人抢先建的目录（可能是指向别处的链接）不用
            await Assert.ThrowsAsync<InvalidOperationException>(() => server.StartAsync(new PluginSessions(), new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    /// <summary>
    /// 只带会话令牌的元数据
    /// </summary>
    private static Metadata Headers(string token) => new() { { PluginProtocol.SessionMetadataKey, token } };

    /// <summary>
    /// 一个不启动进程、清单不声明任何权限的运行时
    /// </summary>
    private static PluginRuntime Runtime(IReadOnlyList<PluginEndpoint> endpoints, PluginSessions sessions)
    {
        var configuration = new ConfigurationBuilder().Build();
        var options = OutOfProcessPluginOptions.FromConfiguration(configuration);
        var context = new PluginHostContext(endpoints, 1, "en-US", "tests", Path.GetTempPath(), options, configuration, TimeProvider.System, sessions, TimeSpan.FromSeconds(5));
        return new PluginRuntime(TestManifests.Create("xihan.test.callbacks"), Path.GetTempPath(), string.Empty, new PermissionGrant([]), context, NullLogger.Instance);
    }
}
