// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.IO.Pipes;
using System.Net.Sockets;
using Grpc.Net.Client;

namespace XiHan.Framework.Plugins.OutOfProcess;

/// <summary>
/// 为本机端点创建 gRPC 通道：Unix 域套接字与命名管道经 <see cref="SocketsHttpHandler.ConnectCallback"/> 连接，TCP 直接连接回环地址。
/// </summary>
internal static class PluginChannelFactory
{
    /// <summary>
    /// 命名管道地址前缀。
    /// </summary>
    private const string PipePrefix = @"\\.\pipe\";

    /// <summary>
    /// 创建通道（HTTP/2 明文）。
    /// </summary>
    public static GrpcChannel Create(PluginEndpoint endpoint)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
            KeepAlivePingDelay = TimeSpan.FromSeconds(30),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(10),
            EnableMultipleHttp2Connections = true,
        };

        var address = "http://localhost";
        switch (endpoint.Network)
        {
            case PluginEndpoint.Unix:
                var socketPath = endpoint.Address;
                handler.ConnectCallback = async (_, cancellationToken) =>
                {
                    var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    try
                    {
                        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken).ConfigureAwait(false);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                };
                break;
            case PluginEndpoint.NamedPipe:
                var pipeName = endpoint.Address.StartsWith(PipePrefix, StringComparison.OrdinalIgnoreCase) ? endpoint.Address[PipePrefix.Length..] : endpoint.Address;
                handler.ConnectCallback = async (_, cancellationToken) =>
                {
                    var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    try
                    {
                        await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);
                        return pipe;
                    }
                    catch
                    {
                        await pipe.DisposeAsync().ConfigureAwait(false);
                        throw;
                    }
                };
                break;
            default:
                address = "http://" + endpoint.Address;
                break;
        }

        return GrpcChannel.ForAddress(address, new GrpcChannelOptions { HttpHandler = handler, DisposeHttpClient = true });
    }
}
