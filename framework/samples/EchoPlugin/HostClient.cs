// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using XiHan.Framework.Plugins.Protocol.V1;
using PluginHost = XiHan.Framework.Plugins.Protocol.V1.Host;
using Status = XiHan.Framework.Plugins.Protocol.V1.Status;

namespace EchoPlugin;

/// <summary>
/// 回调宿主 <c>Host</c> 服务的客户端：按环境变量选宿主端点，每次调用带会话令牌
/// </summary>
/// <remarks>
/// <c>XIHAN_PLUGIN_HOST_ADDRESS</c> 是按宿主偏好排序、以 <c>;</c> 分隔的端点列表，取第一个本进程能连的（<c>spec/plugin/handshake.md</c> §4）。变量缺失、不合法或没有能连的项时照常服务，回调一律以 <c>PLUGINS_UNAVAILABLE_001</c> + <c>plugins.host.unavailable</c> 失败、不发请求
/// </remarks>
internal sealed class HostClient : IDisposable
{
    /// <summary>
    /// 命名管道地址前缀
    /// </summary>
    private const string PipePrefix = @"\\.\pipe\";

    /// <summary>
    /// 没有可用回调端点时 <c>reason</c> 的固定文本
    /// </summary>
    private const string NoEndpoint = "no supported callback endpoint";

    /// <summary>
    /// 会话令牌
    /// </summary>
    private readonly string _session = Environment.GetEnvironmentVariable("XIHAN_PLUGIN_SESSION") ?? string.Empty;

    /// <summary>
    /// 按需创建的通道
    /// </summary>
    /// <remarks>
    /// 没有可用端点时不创建
    /// </remarks>
    private readonly Lazy<GrpcChannel>? _channel;

    /// <summary>
    /// 按环境变量选定端点
    /// </summary>
    public HostClient()
    {
        Endpoint = Select(Environment.GetEnvironmentVariable("XIHAN_PLUGIN_HOST_ADDRESS"));
        if (Endpoint is { } endpoint)
        {
            _channel = new Lazy<GrpcChannel>(() => CreateChannel(endpoint.Network, endpoint.Address));
        }
    }

    /// <summary>
    /// 选定的回调端点
    /// </summary>
    /// <remarks>
    /// 没有可用的为 <see langword="null"/>
    /// </remarks>
    public (string Network, string Address)? Endpoint { get; }

    /// <summary>
    /// 经宿主管道调用一个操作
    /// </summary>
    /// <remarks>
    /// 返回宿主的结果与载荷；没有可用端点时不发请求，返回不可用
    /// </remarks>
    public async Task<InvokeResponse> InvokeAsync(string operation, ByteString payload, InvocationMetadata? metadata, CancellationToken cancellationToken)
    {
        if (_channel is null)
        {
            return new InvokeResponse { Status = Unavailable() };
        }

        var client = new PluginHost.HostClient(_channel.Value);
        var request = new InvokeRequest { Operation = operation, Payload = payload };
        if (metadata is not null)
        {
            request.Metadata = metadata;
        }

        return await client.InvokeAsync(request, Headers(), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 经宿主的消息代理发布一批信封
    /// </summary>
    /// <remarks>
    /// 逐项结果与信封一一对应；没有可用端点时不发请求，每一项都是不可用
    /// </remarks>
    public async Task<PublishResponse> PublishAsync(IEnumerable<Envelope> envelopes, InvocationMetadata? metadata, CancellationToken cancellationToken)
    {
        var request = new PublishRequest { Envelopes = { envelopes } };
        if (metadata is not null)
        {
            request.Metadata = metadata;
        }

        if (_channel is null)
        {
            return new PublishResponse { Results = { request.Envelopes.Select(static _ => Unavailable()) } };
        }

        var client = new PluginHost.HostClient(_channel.Value);
        return await client.PublishAsync(request, Headers(), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_channel is { IsValueCreated: true } channel)
        {
            channel.Value.Dispose();
        }
    }

    /// <summary>
    /// 从端点列表里取第一个本进程能连的
    /// </summary>
    /// <remarks>
    /// 任何一项不合法整个值就不合法；Windows 上命名管道、Unix 域套接字与 TCP 都能连，其他系统没有命名管道
    /// </remarks>
    internal static (string Network, string Address)? Select(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var entries = new List<(string Network, string Address)>();
        foreach (var item in text.Split(';'))
        {
            var space = item.IndexOf(' ', StringComparison.Ordinal);
            if (space <= 0)
            {
                return null;
            }

            var network = item[..space];
            var address = item[(space + 1)..];
            if (network is not ("unix" or "tcp" or "npipe") || address.Length == 0 || address.Any(char.IsWhiteSpace) || (network == "tcp" && !IsLoopback(address)))
            {
                return null;
            }

            entries.Add((network, address));
        }

        foreach (var entry in entries)
        {
            if (entry.Network != "npipe" || OperatingSystem.IsWindows())
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// 每次调用的元数据：会话令牌
    /// </summary>
    private Metadata Headers() => new() { { "xihan-plugin-session", _session } };

    /// <summary>
    /// 没有可用回调端点时的状态
    /// </summary>
    private static Status Unavailable() => new()
    {
        Ok = false,
        Error = new Error
        {
            Code = "PLUGINS_UNAVAILABLE_001",
            MessageKey = "plugins.host.unavailable",
            Details = new JsonObject { ["id"] = EchoPluginService.PluginId, ["reason"] = NoEndpoint }.ToJsonString(),
        },
    };

    /// <summary>
    /// TCP 地址是规范写法的回环地址加端口
    /// </summary>
    /// <remarks>
    /// <c>127.0.0.0/8</c> 的点分十进制（四段、不带前导零）或 <c>[::1]</c>，端口 1–65535、不带前导零（handshake.md §2）
    /// </remarks>
    private static bool IsLoopback(string address)
    {
        var colon = address.LastIndexOf(':');
        if (colon <= 0 || !IsNumber(address[(colon + 1)..], 1, 65535))
        {
            return false;
        }

        var host = address[..colon];
        if (host == "[::1]")
        {
            return true;
        }

        var segments = host.Split('.');
        return segments.Length == 4 && segments[0] == "127" && segments.All(static s => IsNumber(s, 0, 255));
    }

    /// <summary>
    /// 不带正负号与前导零的 ASCII 十进制数，在范围内
    /// </summary>
    private static bool IsNumber(string text, int min, int max)
    {
        if (text.Length is 0 or > 5 || (text.Length > 1 && text[0] == '0') || !text.All(char.IsAsciiDigit))
        {
            return false;
        }

        var value = int.Parse(text, CultureInfo.InvariantCulture);
        return value >= min && value <= max;
    }

    /// <summary>
    /// 按端点建通道
    /// </summary>
    private static GrpcChannel CreateChannel(string network, string address)
    {
        // 命名管道在没有服务端时会一直等：连接也要有超时
        var handler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(5) };
        switch (network)
        {
            case "unix":
                handler.ConnectCallback = async (_, cancellationToken) =>
                {
                    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(address), cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                };
                return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = handler });
            case "npipe":
                var pipeName = address.StartsWith(PipePrefix, StringComparison.OrdinalIgnoreCase) ? address[PipePrefix.Length..] : address;
                handler.ConnectCallback = async (_, cancellationToken) =>
                {
                    var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    return pipe;
                };
                return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = handler });
            default:
                return GrpcChannel.ForAddress("http://" + address, new GrpcChannelOptions { HttpHandler = handler });
        }
    }
}
