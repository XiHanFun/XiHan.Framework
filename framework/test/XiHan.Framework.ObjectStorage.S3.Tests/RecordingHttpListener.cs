// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 本机回环上记录请求头的假 HTTP 对端
/// </summary>
/// <remarks>
/// 既能当 S3 的端点，也能当 HTTP 代理：只读没有请求体的请求（<c>HEAD</c>），把每个请求头原样记下，再按设定应答——不应答（核对超时）、要求代理凭据（没带就 407）、其余一律回给定的状态码。不转发任何请求：用例核对的是本进程发出了什么
/// </remarks>
internal sealed class RecordingHttpListener : IAsyncDisposable
{
    /// <summary>
    /// 请求头的结束标记
    /// </summary>
    private const string HeadTerminator = "\r\n\r\n";

    /// <summary>
    /// 监听的套接字
    /// </summary>
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

    /// <summary>
    /// 停止接受与应答
    /// </summary>
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>
    /// 保护 <see cref="_requests"/> 与 <see cref="_connections"/>
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// 收到的请求头，按到达顺序
    /// </summary>
    private readonly List<string> _requests = [];

    /// <summary>
    /// 每条连接的处理任务
    /// </summary>
    private readonly List<Task> _connections = [];

    /// <summary>
    /// 接受连接的循环
    /// </summary>
    private readonly Task _accepting;

    /// <summary>
    /// 是否只收不答
    /// </summary>
    private readonly bool _silent;

    /// <summary>
    /// 应答的状态行（不含协议版本）
    /// </summary>
    private readonly string _status;

    /// <summary>
    /// 要求的 <c>Proxy-Authorization</c> 头的值
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时不要求代理凭据
    /// </remarks>
    private readonly string? _proxyAuthorization;

    /// <summary>
    /// 创建并开始监听
    /// </summary>
    private RecordingHttpListener(bool silent, string status, string? proxyAuthorization)
    {
        _silent = silent;
        _status = status;
        _proxyAuthorization = proxyAuthorization;
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _accepting = AcceptAsync();
    }

    /// <summary>
    /// 监听的端口
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// 收到的请求头的快照，按到达顺序
    /// </summary>
    public IReadOnlyList<string> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>
    /// 接受过的连接数
    /// </summary>
    public int ConnectionCount
    {
        get
        {
            lock (_gate)
            {
                return _connections.Count;
            }
        }
    }

    /// <summary>
    /// 每个请求都回 <paramref name="status"/>（如 <c>502 Bad Gateway</c>）的对端
    /// </summary>
    public static RecordingHttpListener Answering(string status) => new(silent: false, status, proxyAuthorization: null);

    /// <summary>
    /// 要求基本认证的代理：没带凭据的请求回 407，带对了的回 <paramref name="status"/>
    /// </summary>
    public static RecordingHttpListener ProxyRequiring(string userName, string password, string status)
        => new(silent: false, status, "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(userName + ":" + password)));

    /// <summary>
    /// 只收不答的对端
    /// </summary>
    public static RecordingHttpListener Silent() => new(silent: true, string.Empty, proxyAuthorization: null);

    /// <summary>
    /// 请求头里某个头的值
    /// </summary>
    /// <remarks>
    /// 头名不区分大小写；没有这个头为 <see langword="null"/>
    /// </remarks>
    public static string? Header(string request, string name)
    {
        foreach (var line in request.Split("\r\n").Skip(1))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && line.AsSpan(0, colon).Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return line[(colon + 1)..].Trim();
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listener.Stop();
        Task[] connections;
        lock (_gate)
        {
            connections = [.. _connections];
        }

        // 停止信号让每条连接的读取以取消结束，处理任务自己吞掉它
        await Task.WhenAll([_accepting, .. connections]);
        _stopping.Dispose();
    }

    /// <summary>
    /// 接受连接，直到停止
    /// </summary>
    private async Task AcceptAsync()
    {
        try
        {
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync(_stopping.Token);
                lock (_gate)
                {
                    _connections.Add(ServeAsync(client));
                }
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // 停止监听
        }
    }

    /// <summary>
    /// 在一条连接上逐个读请求头并应答，直到对方关闭或停止
    /// </summary>
    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var pending = new StringBuilder();
                var buffer = new byte[4096];
                while (true)
                {
                    var read = await stream.ReadAsync(buffer, _stopping.Token);
                    if (read == 0)
                    {
                        return;
                    }

                    // 请求头只有 ASCII；用例只发没有请求体的请求，头结束即一个完整的请求
                    pending.Append(Encoding.ASCII.GetString(buffer, 0, read));
                    for (var end = pending.ToString().IndexOf(HeadTerminator, StringComparison.Ordinal); end >= 0; end = pending.ToString().IndexOf(HeadTerminator, StringComparison.Ordinal))
                    {
                        var request = pending.ToString(0, end);
                        pending.Remove(0, end + HeadTerminator.Length);
                        lock (_gate)
                        {
                            _requests.Add(request);
                        }

                        if (_silent)
                        {
                            continue;
                        }

                        var challenged = _proxyAuthorization is not null && Header(request, "Proxy-Authorization") != _proxyAuthorization;
                        var response = challenged
                            ? "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"test\"\r\nContent-Length: 0\r\n\r\n"
                            : $"HTTP/1.1 {_status}\r\nContent-Length: 0\r\n\r\n";
                        await stream.WriteAsync(Encoding.ASCII.GetBytes(response), _stopping.Token);
                    }
                }
            }
            catch (Exception exception) when (exception is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
            {
                // 停止，或对方断开
            }
        }
    }
}
