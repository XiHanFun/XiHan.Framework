// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Security.Cryptography;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace EchoPlugin;

/// <summary>
/// 插件自己的监听端点：Windows 用命名管道，其他系统用临时目录下的 Unix 域套接字。
/// </summary>
internal sealed class PluginListener
{
    /// <summary>
    /// 命名管道地址前缀。
    /// </summary>
    private const string PipePrefix = @"\\.\pipe\";

    private PluginListener(string network, string address)
    {
        Network = network;
        Address = address;
    }

    /// <summary>
    /// 网络类型（<c>unix</c> / <c>npipe</c>）。
    /// </summary>
    public string Network { get; }

    /// <summary>
    /// 地址。
    /// </summary>
    public string Address { get; }

    /// <summary>
    /// 握手行。
    /// </summary>
    public string HandshakeLine => $"xihan-plugin/1 {Network} {Address}";

    /// <summary>
    /// 选一个随机名字的端点。
    /// </summary>
    public static PluginListener Create()
    {
        var name = "xihan-echo-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        return OperatingSystem.IsWindows()
            ? new PluginListener("npipe", PipePrefix + name)
            : new PluginListener("unix", Path.Combine(Path.GetTempPath(), name + ".sock"));
    }

    /// <summary>
    /// 让 Kestrel 以 HTTP/2 明文监听本端点。
    /// </summary>
    public void Listen(KestrelServerOptions kestrel)
    {
        if (Network == "npipe")
        {
            kestrel.ListenNamedPipe(Address[PipePrefix.Length..], static listen => listen.Protocols = HttpProtocols.Http2);
        }
        else
        {
            kestrel.ListenUnixSocket(Address, static listen => listen.Protocols = HttpProtocols.Http2);
        }
    }
}
