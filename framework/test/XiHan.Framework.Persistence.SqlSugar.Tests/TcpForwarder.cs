// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 本机上的 TCP 转发
/// </summary>
/// <remarks>
/// 把从库放在它后面，停掉它就是从库宕机：连接断开、新连接被拒绝，连接池里的空闲连接照样在；再开起来就恢复了，用例不用动容器
/// </remarks>
internal sealed class TcpForwarder : IAsyncDisposable
{
    /// <summary>
    /// 上游主机
    /// </summary>
    private readonly string _host;

    /// <summary>
    /// 上游端口
    /// </summary>
    private readonly int _port;

    /// <summary>
    /// 转发中的连接
    /// </summary>
    private readonly ConcurrentDictionary<TcpClient, byte> _connections = new();

    /// <summary>
    /// 监听器
    /// </summary>
    /// <remarks>
    /// 停止时为空
    /// </remarks>
    private TcpListener? _listener;

    /// <summary>
    /// 接受连接的循环
    /// </summary>
    private Task? _accepting;

    /// <summary>
    /// 创建，还没开始监听
    /// </summary>
    public TcpForwarder(string host, int port)
    {
        _host = host;
        _port = port;
    }

    /// <summary>
    /// 监听的端口
    /// </summary>
    /// <remarks>
    /// 第一次 <see cref="Start"/> 时定下，之后不变
    /// </remarks>
    public int Port { get; private set; }

    /// <summary>
    /// 开始转发
    /// </summary>
    public void Start()
    {
        _listener = new TcpListener(IPAddress.Loopback, Port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _accepting = AcceptAsync(_listener);
    }

    /// <summary>
    /// 停止转发，断开全部连接
    /// </summary>
    public async Task StopAsync()
    {
        _listener?.Stop();
        _listener = null;
        foreach (var connection in _connections.Keys)
        {
            connection.Close();
        }

        if (_accepting is { } accepting)
        {
            await accepting;
        }

        _accepting = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync();

    /// <summary>
    /// 接受连接，直到监听器停止
    /// </summary>
    private async Task AcceptAsync(TcpListener listener)
    {
        try
        {
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                _ = ForwardAsync(client);
            }
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            // 监听器停止：结束
        }
    }

    /// <summary>
    /// 转发一条连接，任一方向结束就断开两边
    /// </summary>
    private async Task ForwardAsync(TcpClient client)
    {
        using var upstream = new TcpClient();
        _connections[client] = 0;
        _connections[upstream] = 0;
        try
        {
            await upstream.ConnectAsync(_host, _port);
            var one = client.GetStream().CopyToAsync(upstream.GetStream());
            var other = upstream.GetStream().CopyToAsync(client.GetStream());
            await Task.WhenAny(one, other);
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException or InvalidOperationException)
        {
            // 停止时连接被关掉：结束
        }
        finally
        {
            _connections.TryRemove(client, out _);
            _connections.TryRemove(upstream, out _);
            client.Dispose();
        }
    }
}
