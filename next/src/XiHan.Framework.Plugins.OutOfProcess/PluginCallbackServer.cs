// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace XiHan.Framework.Plugins.OutOfProcess;

/// <summary>
/// 宿主回调服务器：一个只监听本机端点、只服务 <c>Host</c> 的 Kestrel 实例，与宿主应用自己的服务器无关。
/// </summary>
internal sealed class PluginCallbackServer(PluginEndpoint endpoint, PluginSessions sessions, IServiceProvider services, ILoggerFactory loggerFactory) : IAsyncDisposable
{
    /// <summary>
    /// 命名管道地址前缀。
    /// </summary>
    private const string PipePrefix = @"\\.\pipe\";

    /// <summary>
    /// 运行中的应用。
    /// </summary>
    private WebApplication? _app;

    /// <summary>
    /// 监听的端点。
    /// </summary>
    public PluginEndpoint Endpoint => endpoint;

    /// <summary>
    /// 为本机选一个新端点：Windows 用命名管道，其他系统用临时目录下的 Unix 域套接字；名字随机，不与其他宿主冲突。
    /// </summary>
    public static PluginEndpoint CreateEndpoint()
    {
        var name = "xihan-host-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        return OperatingSystem.IsWindows()
            ? new PluginEndpoint(PluginEndpoint.NamedPipe, PipePrefix + name)
            : new PluginEndpoint(PluginEndpoint.Unix, Path.Combine(Path.GetTempPath(), name + ".sock"));
    }

    /// <summary>
    /// 启动服务器。
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // 空白构建器：不读环境变量、appsettings 与 Kestrel 配置节，免得继承宿主应用的监听地址
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { ApplicationName = "xihan.plugins.host" });
        builder.WebHost.UseKestrelCore();
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
    }

    /// <summary>
    /// 停止服务器并清理套接字文件。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_app is { } app)
        {
            _app = null;
            await app.StopAsync().ConfigureAwait(false);
            await app.DisposeAsync().ConfigureAwait(false);
        }

        if (endpoint.Network == PluginEndpoint.Unix)
        {
            try
            {
                File.Delete(endpoint.Address);
            }
            catch (IOException)
            {
                // 套接字文件可能已被 Kestrel 删除
            }
            catch (UnauthorizedAccessException)
            {
                // 无权删除时留给临时目录清理
            }
        }
    }
}
