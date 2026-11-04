// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 在 <c>127.0.0.1</c> 上监听的最小 HTTP 服务
/// </summary>
/// <remarks>
/// 记下接受到的连接数，用来断言请求到没到达；每个响应都关连接，下一个请求一定建新连接
/// </remarks>
internal sealed class LoopbackHttpServer : IDisposable
{
    /// <summary>
    /// 监听器
    /// </summary>
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

    /// <summary>
    /// 固定的响应
    /// </summary>
    private readonly byte[] _response;

    /// <summary>
    /// 接受到的连接数
    /// </summary>
    private int _accepted;

    /// <summary>
    /// 开始监听
    /// </summary>
    /// <param name="redirectTo">给出时答 302 到这个地址，否则答 200</param>
    public LoopbackHttpServer(string? redirectTo = null)
    {
        var status = redirectTo is null ? "200 OK" : $"302 Found\r\nLocation: {redirectTo}";
        _response = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        _listener.Start();
        _ = ServeAsync();
    }

    /// <summary>
    /// 端口
    /// </summary>
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>
    /// 接受到的连接数
    /// </summary>
    public int Accepted => Volatile.Read(ref _accepted);

    /// <inheritdoc />
    public void Dispose() => _listener.Dispose();

    /// <summary>
    /// 接受连接并作答，直到监听器释放
    /// </summary>
    private async Task ServeAsync()
    {
        try
        {
            while (true)
            {
                using var client = await _listener.AcceptTcpClientAsync();
                Interlocked.Increment(ref _accepted);
                var stream = client.GetStream();
                _ = await stream.ReadAsync(new byte[4096]);
                await stream.WriteAsync(_response);
            }
        }
        catch (ObjectDisposedException)
        {
            // 监听器释放：结束
        }
        catch (SocketException)
        {
            // 监听器释放：结束
        }
    }
}
