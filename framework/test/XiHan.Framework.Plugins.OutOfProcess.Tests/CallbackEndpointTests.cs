// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Plugins.OutOfProcess.Callbacks;
using XiHan.Framework.Plugins.Protocol.Handshake;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 宿主回调端点只给本用户（ADR-0202）
/// </summary>
/// <remarks>
/// 安全审查 S-18：原先回调套接字直接放在共享的临时目录下。Unix 上套接字在新建的私有目录里（0700），文件 0600；Windows 的命名管道只允许当前用户，由 Kestrel 的选项保证
/// </remarks>
public sealed class CallbackEndpointTests
{
    [Fact]
    public async Task UnixSocket_LivesInAPrivateDirectory_AndIsRemovedWithIt()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows uses a named pipe restricted to the current user.");
            return;
        }

        var endpoint = PluginCallbackServer.CreateEndpoint();
        var directory = Path.GetDirectoryName(endpoint.Address)!;
        await using (var server = Server(endpoint))
        {
            await server.StartAsync(CancellationToken.None);

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

        var endpoint = PluginCallbackServer.CreateEndpoint();
        var directory = Path.GetDirectoryName(endpoint.Address)!;
        Directory.CreateDirectory(directory);
        try
        {
            await using var server = Server(endpoint);

            // 别人抢先建的目录（可能是指向别处的链接）不用
            await Assert.ThrowsAsync<InvalidOperationException>(() => server.StartAsync(CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    /// <summary>
    /// 不服务任何插件的回调服务器
    /// </summary>
    private static PluginCallbackServer Server(PluginEndpoint endpoint)
        => new(endpoint, new PluginSessions(), new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance);
}
