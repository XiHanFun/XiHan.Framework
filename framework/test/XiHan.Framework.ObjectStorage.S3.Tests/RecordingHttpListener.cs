// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 本机回环上记录请求头的假 HTTP 对端
/// </summary>
/// <remarks>
/// 既能当 S3 的端点，也能当 HTTP 代理：把每个请求头原样记下，按 <c>Content-Length</c> 读掉请求体（带 <c>Expect: 100-continue</c> 时先回 100），再按设定应答——不应答（核对超时）、要求代理凭据（没带就 407）、其余一律回给定的状态码、响应头与响应体（缺省没有响应体）。不转发任何请求：用例核对的是本进程发出了什么
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
    /// 应答另带的响应头，每行以 CRLF 结尾
    /// </summary>
    private readonly string _headers;

    /// <summary>
    /// 应答的响应体
    /// </summary>
    /// <remarks>
    /// 按 UTF-8 编码写出，<c>Content-Length</c> 是它的字节数；缺省为空
    /// </remarks>
    private readonly byte[] _body;

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
    private RecordingHttpListener(bool silent, string status, string? proxyAuthorization, string[]? headers = null, string? body = null)
    {
        _silent = silent;
        _status = status;
        _headers = string.Concat((headers ?? []).Select(static header => header + "\r\n"));
        _body = Encoding.UTF8.GetBytes(body ?? string.Empty);
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
    /// <remarks>
    /// <paramref name="headers"/> 是另带的响应头，每项一整行（如 <c>ETag: "abc"</c>），原样写出，不带 <c>Content-Length</c>（它总是 0）
    /// </remarks>
    public static RecordingHttpListener Answering(string status, params string[] headers) => new(silent: false, status, proxyAuthorization: null, headers);

    /// <summary>
    /// 每个请求都回 <paramref name="status"/> 与响应体 <paramref name="body"/> 的对端
    /// </summary>
    /// <remarks>
    /// 让 SDK 解析一份给定的应答（如列出分片的 XML）；<paramref name="headers"/> 同 <see cref="Answering"/>，<c>Content-Length</c> 按响应体的 UTF-8 字节数写出
    /// </remarks>
    public static RecordingHttpListener AnsweringWithBody(string status, string body, params string[] headers) => new(silent: false, status, proxyAuthorization: null, headers, body);

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
    /// 在一条连接上逐个读请求并应答，直到对方关闭或停止
    /// </summary>
    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                // 按 ASCII 解码，每个字节恰好是一个字符：请求头只有 ASCII，请求体只按长度跳过，不关心内容
                var pending = new StringBuilder();
                while (true)
                {
                    int end;
                    while ((end = pending.ToString().IndexOf(HeadTerminator, StringComparison.Ordinal)) < 0)
                    {
                        if (!await ReadMoreAsync(stream, pending))
                        {
                            return;
                        }
                    }

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
                    if (!challenged && int.TryParse(Header(request, "Content-Length"), out var length) && length > 0)
                    {
                        // 客户端等 100 才发请求体；读掉请求体再应答，同一连接上的下一个请求才从头开始
                        if (string.Equals(Header(request, "Expect"), "100-continue", StringComparison.OrdinalIgnoreCase))
                        {
                            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n"), _stopping.Token);
                        }

                        while (pending.Length < length)
                        {
                            if (!await ReadMoreAsync(stream, pending))
                            {
                                return;
                            }
                        }

                        pending.Remove(0, length);
                    }

                    var response = challenged
                        ? "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"test\"\r\nContent-Length: 0\r\n\r\n"
                        : $"HTTP/1.1 {_status}\r\n{_headers}Content-Length: {_body.Length.ToString(CultureInfo.InvariantCulture)}\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(response), _stopping.Token);
                    if (!challenged && _body.Length > 0)
                    {
                        await stream.WriteAsync(_body, _stopping.Token);
                    }
                }
            }
            catch (Exception exception) when (exception is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
            {
                // 停止，或对方断开
            }
        }
    }

    /// <summary>
    /// 再读一段，追加到 <paramref name="pending"/>
    /// </summary>
    /// <remarks>
    /// 对方关闭连接时返回 <see langword="false"/>
    /// </remarks>
    private async Task<bool> ReadMoreAsync(NetworkStream stream, StringBuilder pending)
    {
        var buffer = new byte[4096];
        var read = await stream.ReadAsync(buffer, _stopping.Token);
        if (read == 0)
        {
            return false;
        }

        pending.Append(Encoding.ASCII.GetString(buffer, 0, read));
        return true;
    }
}
