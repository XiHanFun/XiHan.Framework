// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.IO.Pipes;
using System.Net.Sockets;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using XiHan.Framework.Plugins.Protocol.V1;
using PluginHost = XiHan.Framework.Plugins.Protocol.V1.Host;

namespace EchoPlugin;

/// <summary>
/// 回调宿主 <c>Host</c> 服务的客户端：按环境变量连接宿主端点，每次调用带会话令牌。
/// </summary>
internal sealed class HostClient : IDisposable
{
    /// <summary>
    /// 命名管道地址前缀。
    /// </summary>
    private const string PipePrefix = @"\\.\pipe\";

    /// <summary>
    /// 会话令牌。
    /// </summary>
    private readonly string _session = Environment.GetEnvironmentVariable("XIHAN_PLUGIN_SESSION") ?? string.Empty;

    /// <summary>
    /// 按需创建的通道。
    /// </summary>
    private readonly Lazy<GrpcChannel> _channel = new(CreateChannel);

    /// <summary>
    /// 经宿主管道调用一个操作。
    /// </summary>
    /// <remarks>
    /// 返回宿主的结果与载荷。
    /// </remarks>
    public async Task<InvokeResponse> InvokeAsync(string operation, ByteString payload, InvocationMetadata? metadata, CancellationToken cancellationToken)
    {
        var client = new PluginHost.HostClient(_channel.Value);
        var headers = new Metadata { { "xihan-plugin-session", _session } };
        var request = new InvokeRequest { Operation = operation, Payload = payload };
        if (metadata is not null)
        {
            request.Metadata = metadata;
        }

        return await client.InvokeAsync(request, headers, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_channel.IsValueCreated)
        {
            _channel.Value.Dispose();
        }
    }

    /// <summary>
    /// 从 <c>XIHAN_PLUGIN_HOST_ADDRESS</c>（<c>&lt;network&gt; &lt;address&gt;</c>）建通道。
    /// </summary>
    private static GrpcChannel CreateChannel()
    {
        var text = Environment.GetEnvironmentVariable("XIHAN_PLUGIN_HOST_ADDRESS") ?? throw new InvalidOperationException("XIHAN_PLUGIN_HOST_ADDRESS is not set.");
        var parts = text.Split(' ', 2);
        // 命名管道在没有服务端时会一直等：连接也要有超时
        var handler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(5) };
        switch (parts[0])
        {
            case "unix":
                var socketPath = parts[1];
                handler.ConnectCallback = async (_, cancellationToken) =>
                {
                    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                };
                return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = handler });
            case "npipe":
                var pipeName = parts[1].StartsWith(PipePrefix, StringComparison.OrdinalIgnoreCase) ? parts[1][PipePrefix.Length..] : parts[1];
                handler.ConnectCallback = async (_, cancellationToken) =>
                {
                    var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    return pipe;
                };
                return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = handler });
            default:
                return GrpcChannel.ForAddress("http://" + parts[1], new GrpcChannelOptions { HttpHandler = handler });
        }
    }
}
