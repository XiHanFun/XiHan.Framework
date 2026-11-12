// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Security.Cryptography;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace EchoPlugin;

/// <summary>
/// 插件自己的监听端点：Windows 用命名管道，其他系统用临时目录下的 Unix 域套接字
/// </summary>
/// <remarks>
/// 套接字放在新建的私有目录里（0700），文件 0600；命名管道只允许当前用户（handshake.md §4）
/// </remarks>
internal sealed class PluginListener
{
    /// <summary>
    /// 命名管道地址前缀
    /// </summary>
    private const string PipePrefix = @"\\.\pipe\";

    /// <summary>
    /// 以网络类型与地址创建
    /// </summary>
    private PluginListener(string network, string address)
    {
        Network = network;
        Address = address;
    }

    /// <summary>
    /// 网络类型（<c>unix</c> / <c>npipe</c>）
    /// </summary>
    public string Network { get; }

    /// <summary>
    /// 地址
    /// </summary>
    public string Address { get; }

    /// <summary>
    /// 握手行
    /// </summary>
    public string HandshakeLine => $"xihan-plugin/1 {Network} {Address}";

    /// <summary>
    /// 选一个随机名字的端点
    /// </summary>
    public static PluginListener Create()
    {
        var name = "xihan-echo-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        if (OperatingSystem.IsWindows())
        {
            return new PluginListener("npipe", PipePrefix + name);
        }

        // 新建只有本用户能进入的目录；随机名字已经存在说明有人抢先建了，不用
        var directory = Path.Combine(Path.GetTempPath(), name);
        if (Directory.Exists(directory) || File.Exists(directory))
        {
            throw new InvalidOperationException($"The plugin socket directory '{directory}' already exists.");
        }

        Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return new PluginListener("unix", Path.Combine(directory, "plugin.sock"));
    }

    /// <summary>
    /// 监听开始后把套接字文件收紧到只给本用户读写
    /// </summary>
    public void Restrict()
    {
        if (Network == "unix" && !OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(Address, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    /// <summary>
    /// 退出时删掉套接字与私有目录
    /// </summary>
    public void Delete()
    {
        if (Network != "unix")
        {
            return;
        }

        try
        {
            File.Delete(Address);
            Directory.Delete(Path.GetDirectoryName(Address)!);
        }
        catch (IOException)
        {
            // 留给临时目录清理
        }
    }

    /// <summary>
    /// 让 Kestrel 以 HTTP/2 明文监听本端点
    /// </summary>
    public void Listen(KestrelServerOptions kestrel)
    {
        if (Network == "npipe")
        {
            // 命名管道只允许当前用户连接
            kestrel.ListenNamedPipe(Address[PipePrefix.Length..], static listen => listen.Protocols = HttpProtocols.Http2);
        }
        else
        {
            kestrel.ListenUnixSocket(Address, static listen => listen.Protocols = HttpProtocols.Http2);
        }
    }
}
