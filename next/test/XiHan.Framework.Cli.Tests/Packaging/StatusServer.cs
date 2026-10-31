// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace XiHan.Framework.Cli.Tests.Packaging;

/// <summary>
/// 回环地址上的最小 HTTP/1.1 服务。
/// </summary>
/// <remarks>
/// 请求 <c>/health/ready</c> 回 200，其余回 503，每个连接应答一次后关闭；端口由系统分配。
/// </remarks>
internal sealed class StatusServer : IDisposable
{
    /// <summary>
    /// 监听器。
    /// </summary>
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

    /// <summary>
    /// 停止接受连接。
    /// </summary>
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>
    /// 开始监听。
    /// </summary>
    public StatusServer()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = AcceptAsync();
    }

    /// <summary>
    /// 监听的端口。
    /// </summary>
    public int Port { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping.Cancel();
        _listener.Stop();
        _listener.Dispose();
        _stopping.Dispose();
    }

    /// <summary>
    /// 逐个接受连接并应答。
    /// </summary>
    private async Task AcceptAsync()
    {
        try
        {
            while (!_stopping.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(_stopping.Token);
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync(_stopping.Token) ?? string.Empty;
                var status = requestLine.StartsWith("GET /health/ready ", StringComparison.Ordinal) ? "200 OK" : "503 Service Unavailable";
                var response = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(response, _stopping.Token);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or SocketException or IOException)
        {
            // 服务已停止
        }
    }
}
