// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Plugins.Protocol.Handshake;

namespace XiHan.Framework.Plugins.OutOfProcess.Callbacks;

/// <summary>
/// 宿主回调服务器
/// </summary>
/// <remarks>
/// 一个只监听本机端点、只服务 <c>Host</c> 的 Kestrel 实例，与宿主应用自己的服务器无关
/// </remarks>
internal sealed class PluginCallbackServer(PluginEndpoint endpoint, PluginSessions sessions, IServiceProvider services, ILoggerFactory loggerFactory) : IAsyncDisposable
{
    /// <summary>
    /// 命名管道地址前缀
    /// </summary>
    private const string PipePrefix = @"\\.\pipe\";

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
    /// 监听的端点
    /// </summary>
    public PluginEndpoint Endpoint => endpoint;

    /// <summary>
    /// 为本机选一个新端点
    /// </summary>
    /// <remarks>
    /// Windows 用只允许当前用户的命名管道，其他系统用临时目录下一个私有目录里的 Unix 域套接字（目录在启动时新建，权限 0700，ADR-0202）；名字随机，不与其他宿主冲突
    /// </remarks>
    public static PluginEndpoint CreateEndpoint()
    {
        var name = "xihan-host-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        return OperatingSystem.IsWindows()
            ? new PluginEndpoint(PluginEndpoint.NamedPipe, PipePrefix + name)
            : new PluginEndpoint(PluginEndpoint.Unix, Path.Combine(Path.GetTempPath(), name, SocketFileName));
    }

    /// <summary>
    /// 私有目录里的套接字文件名
    /// </summary>
    private const string SocketFileName = "host.sock";

    /// <summary>
    /// 启动服务器
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (endpoint.Network == PluginEndpoint.Unix && !OperatingSystem.IsWindows())
        {
            CreatePrivateDirectory(Path.GetDirectoryName(endpoint.Address)!);
            _ownsDirectory = true;
        }

        // 空白构建器：不读环境变量、appsettings 与 Kestrel 配置节，免得继承宿主应用的监听地址
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { ApplicationName = "xihan.plugins.host" });
        builder.WebHost.UseKestrelCore();
        // 命名管道只允许当前用户连接（Kestrel 的缺省，这里写明，不随缺省变化）
        if (OperatingSystem.IsWindows())
        {
            builder.WebHost.UseNamedPipes(static pipes => pipes.CurrentUserOnly = true);
        }
        builder.WebHost.ConfigureKestrel(kestrel =>
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
                    kestrel.Listen(IPEndPoint.Parse(endpoint.Address), static listen => listen.Protocols = HttpProtocols.Http2);
                    break;
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
        if (endpoint.Network == PluginEndpoint.Unix && !OperatingSystem.IsWindows())
        {
            // 套接字文件只给本用户读写：目录已经挡住了别的用户，文件权限是第二道
            File.SetUnixFileMode(endpoint.Address, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

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

    /// <summary>
    /// 停止服务器并清理套接字文件
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_app is { } app)
        {
            _app = null;
            await app.StopAsync().ConfigureAwait(false);
            await app.DisposeAsync().ConfigureAwait(false);
        }

        if (endpoint.Network == PluginEndpoint.Unix && _ownsDirectory)
        {
            _ownsDirectory = false;
            try
            {
                File.Delete(endpoint.Address);
                // 私有目录随套接字一起删掉，临时目录里不留空目录
                Directory.Delete(Path.GetDirectoryName(endpoint.Address)!);
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
}
