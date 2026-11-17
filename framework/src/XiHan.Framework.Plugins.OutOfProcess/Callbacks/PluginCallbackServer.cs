// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Plugins.Protocol.Handshake;

namespace XiHan.Framework.Plugins.OutOfProcess.Callbacks;

/// <summary>
/// 宿主回调服务器
/// </summary>
/// <remarks>
/// 一个只监听本机端点、只服务 <c>Host</c> 的 Kestrel 实例，与宿主应用自己的服务器无关。同时监听首选端点（Windows 上只允许当前用户的命名管道，其他系统上私有目录里的 Unix 域套接字）与一个回环 TCP（<c>handshake.md</c> §4，ADR-0237）：Python、Java、PHP 的 gRPC 库连不上命名管道，Node 与不用 unsafe 的 Rust 给不了命名管道的访问控制，回环 TCP 是各语言都连得上的那一个。各端点服务同一个 <c>Host</c> 服务、校验同一套会话令牌
/// </remarks>
internal sealed class PluginCallbackServer : IAsyncDisposable
{
    /// <summary>
    /// 命名管道地址前缀
    /// </summary>
    private const string PipePrefix = @"\\.\pipe\";

    /// <summary>
    /// 私有目录里的套接字文件名
    /// </summary>
    private const string SocketFileName = "host.sock";

    /// <summary>
    /// 预先绑定的回环 TCP 套接字
    /// </summary>
    /// <remarks>
    /// 插件在配置阶段启动、环境变量那时就要写出端口，Kestrel 要到宿主附着时才监听：先由系统分配端口并占住，监听时交给 Kestrel。交出去之后为 <see langword="null"/>，由 Kestrel 负责释放
    /// </remarks>
    private Socket? _reserved;

    /// <summary>
    /// 运行中的应用
    /// </summary>
    private WebApplication? _app;

    /// <summary>
    /// 套接字的私有目录是否由本服务器新建
    /// </summary>
    /// <remarks>
    /// 只删自己建的：启动时拒用的已有目录不是本服务器的
    /// </remarks>
    private bool _ownsDirectory;

    /// <summary>
    /// 以选定的端点创建
    /// </summary>
    /// <param name="endpoints">按偏好排序的端点，最后一项是 <paramref name="reserved"/> 的回环 TCP</param>
    /// <param name="reserved">已绑定、尚未监听的回环 TCP 套接字</param>
    private PluginCallbackServer(IReadOnlyList<PluginEndpoint> endpoints, Socket reserved)
    {
        Endpoints = endpoints;
        _reserved = reserved;
    }

    /// <summary>
    /// 按偏好排序的端点
    /// </summary>
    /// <remarks>
    /// 写进 <c>XIHAN_PLUGIN_HOST_ADDRESS</c>；第一项同时写进 <c>HandshakeRequest.host_endpoint</c>（只供诊断），回环 TCP 总在最后
    /// </remarks>
    public IReadOnlyList<PluginEndpoint> Endpoints { get; }

    /// <summary>
    /// 为本机选定端点，并占住回环 TCP 的端口
    /// </summary>
    /// <remarks>
    /// Windows 用只允许当前用户的命名管道，其他系统用临时目录下一个私有目录里的 Unix 域套接字（目录在启动时新建，权限 0700，ADR-0202）；临时目录的路径含空白或 <c>;</c> 时写不进端点列表，不提供这一项。名字随机，不与其他宿主冲突。回环 TCP 绑定 <c>127.0.0.1</c>、端口由系统分配
    /// </remarks>
    public static PluginCallbackServer Reserve()
    {
        var endpoints = new List<PluginEndpoint>(2);
        var name = "xihan-host-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        if (OperatingSystem.IsWindows())
        {
            endpoints.Add(new PluginEndpoint(PluginEndpoint.NamedPipe, PipePrefix + name));
        }
        else if (PluginEndpoint.TryParseList($"{PluginEndpoint.Unix} {Path.Combine(Path.GetTempPath(), name, SocketFileName)}", out var unix))
        {
            endpoints.Add(unix[0]);
        }

        var socket = SocketTransportOptions.CreateDefaultBoundListenSocket(new IPEndPoint(IPAddress.Loopback, 0));
        try
        {
            var bound = (IPEndPoint)socket.LocalEndPoint!;
            endpoints.Add(new PluginEndpoint(PluginEndpoint.Tcp, bound.ToString()));
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        return new PluginCallbackServer(endpoints, socket);
    }

    /// <summary>
    /// 启动服务器
    /// </summary>
    /// <param name="sessions">会话登记表</param>
    /// <param name="services">宿主应用的根容器</param>
    /// <param name="loggerFactory">日志工厂</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task StartAsync(PluginSessions sessions, IServiceProvider services, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        var reserved = _reserved ?? throw new InvalidOperationException("The callback server has already been started or disposed.");
        var reservedEndPoint = (IPEndPoint)reserved.LocalEndPoint!;
        if (Unix() is { } unix && !OperatingSystem.IsWindows())
        {
            CreatePrivateDirectory(Path.GetDirectoryName(unix.Address)!);
            _ownsDirectory = true;
        }

        // 空白构建器：不读环境变量、appsettings 与 Kestrel 配置节，免得继承宿主应用的监听地址
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { ApplicationName = "xihan.plugins.host" });
        builder.WebHost.UseKestrelCore();
        // 回环 TCP 用预先占住的套接字：端口在插件启动时就已经写进了环境变量
        builder.WebHost.UseSockets(sockets => sockets.CreateBoundListenSocket = endpoint =>
            endpoint is IPEndPoint ip && ip.Equals(reservedEndPoint) && Interlocked.Exchange(ref _reserved, null) is { } socket
                ? socket
                : SocketTransportOptions.CreateDefaultBoundListenSocket(endpoint));
        // 命名管道只允许当前用户连接（Kestrel 的缺省，这里写明，不随缺省变化）
        if (OperatingSystem.IsWindows())
        {
            builder.WebHost.UseNamedPipes(static pipes => pipes.CurrentUserOnly = true);
        }

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            foreach (var endpoint in Endpoints)
            {
                switch (endpoint.Network)
                {
                    case PluginEndpoint.Unix:
                        kestrel.ListenUnixSocket(endpoint.Address, static listen => listen.Protocols = HttpProtocols.Http2);
                        break;
                    case PluginEndpoint.NamedPipe:
                        kestrel.ListenNamedPipe(endpoint.Address.StartsWith(PipePrefix, StringComparison.OrdinalIgnoreCase) ? endpoint.Address[PipePrefix.Length..] : endpoint.Address, static listen => listen.Protocols = HttpProtocols.Http2);
                        break;
                    default:
                        kestrel.Listen(reservedEndPoint, static listen => listen.Protocols = HttpProtocols.Http2);
                        break;
                }
            }
        });
        builder.Services.AddSingleton(loggerFactory);
        builder.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        builder.Services.AddRoutingCore();
        builder.Services.AddGrpc(static options => options.Interceptors.Add<SessionInterceptor>());
        builder.Services.AddSingleton(sessions);
        builder.Services.AddSingleton(new HostServices(services));

        var app = builder.Build();
        app.MapGrpcService<HostCallbackService>();
        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        _app = app;
        if (Unix() is { } socketFile && !OperatingSystem.IsWindows())
        {
            // 套接字文件只给本用户读写：目录已经挡住了别的用户，文件权限是第二道
            File.SetUnixFileMode(socketFile.Address, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    /// <summary>
    /// 停止服务器，清理套接字文件与没有交出去的 TCP 套接字
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_app is { } app)
        {
            _app = null;
            await app.StopAsync().ConfigureAwait(false);
            await app.DisposeAsync().ConfigureAwait(false);
        }

        // 没有启动过（宿主在附着之前就释放了）时端口还占着
        Interlocked.Exchange(ref _reserved, null)?.Dispose();

        if (Unix() is { } unix && _ownsDirectory)
        {
            _ownsDirectory = false;
            try
            {
                File.Delete(unix.Address);
                // 私有目录随套接字一起删掉，临时目录里不留空目录
                Directory.Delete(Path.GetDirectoryName(unix.Address)!);
            }
            catch (IOException)
            {
                // 套接字文件可能已被 Kestrel 删除，目录可能不是空的或已不存在
            }
            catch (UnauthorizedAccessException)
            {
                // 无权删除时留给临时目录清理
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 端点列表的文本，与 <c>XIHAN_PLUGIN_HOST_ADDRESS</c> 相同（日志用）
    /// </remarks>
    public override string ToString() => PluginEndpoint.FormatList(Endpoints);

    /// <summary>
    /// Unix 域套接字那一项
    /// </summary>
    /// <remarks>
    /// Windows 上与临时目录路径写不进列表时没有
    /// </remarks>
    private PluginEndpoint? Unix() => Endpoints.FirstOrDefault(static e => e.Network == PluginEndpoint.Unix);

    /// <summary>
    /// 新建只有本用户能进入的目录
    /// </summary>
    /// <remarks>
    /// 名字随机；已经存在就不用：可能是别的用户抢先建的、或指向别处的链接（handshake.md §4，ADR-0202）
    /// </remarks>
    [UnsupportedOSPlatform("windows")]
    private static void CreatePrivateDirectory(string directory)
    {
        if (Directory.Exists(directory) || File.Exists(directory))
        {
            throw new InvalidOperationException($"The callback socket directory '{directory}' already exists; it must be created by this host.");
        }

        Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}
