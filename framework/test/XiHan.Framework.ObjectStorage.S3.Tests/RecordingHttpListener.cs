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
/// 既能当 S3 的端点，也能当 HTTP 代理：把每个请求头原样记下，按 <c>Content-Length</c> 读掉请求体（带 <c>Expect: 100-continue</c> 时先回 100），再按设定应答——不应答（核对超时）、要求代理凭据（没带就 407）、其余回给定的状态码、响应头与响应体（缺省没有响应体）。应答可以按请求的到达次序各不相同（<see cref="AnsweringInTurn"/>），也可以原样写出响应头、不另加 <c>Content-Length</c>（<see cref="AnsweringVerbatim"/>）；响应头按 Latin-1 写出，经 <see cref="RawHeader"/> 能带任意字节。不转发任何请求：用例核对的是本进程发出了什么
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
    /// 按请求的到达次序给出的应答
    /// </summary>
    /// <remarks>
    /// 第 n 个请求（从 0 起）回第 n 个，之后的请求都回最后一个
    /// </remarks>
    private readonly Reply[] _replies;

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
    private RecordingHttpListener(bool silent, string? proxyAuthorization, params Reply[] replies)
    {
        _silent = silent;
        _replies = replies;
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
    public static RecordingHttpListener Answering(string status, params string[] headers) => new(silent: false, proxyAuthorization: null, new Reply(status, string.Empty, headers));

    /// <summary>
    /// 每个请求都回 <paramref name="status"/> 与响应体 <paramref name="body"/> 的对端
    /// </summary>
    /// <remarks>
    /// 让 SDK 解析一份给定的应答（如列出分片的 XML）；<paramref name="headers"/> 同 <see cref="Answering"/>，<c>Content-Length</c> 按响应体的 UTF-8 字节数写出
    /// </remarks>
    public static RecordingHttpListener AnsweringWithBody(string status, string body, params string[] headers) => new(silent: false, proxyAuthorization: null, new Reply(status, body, headers));

    /// <summary>
    /// 每个请求都回 <paramref name="status"/>、原样写出 <paramref name="headers"/> 与响应体 <paramref name="body"/>、随即关闭连接的对端
    /// </summary>
    /// <remarks>
    /// 不另加 <c>Content-Length</c>：<paramref name="headers"/> 里没有它，应答就缺它（GET 的响应体读到连接关闭为止），有就照写，写错的值也照写。每条连接只答一个请求
    /// </remarks>
    public static RecordingHttpListener AnsweringVerbatim(string status, string body, params string[] headers) => new(silent: false, proxyAuthorization: null, new Reply(status, body, headers, Verbatim: true));

    /// <summary>
    /// 按请求的到达次序依次回 <paramref name="replies"/> 的对端
    /// </summary>
    /// <remarks>
    /// 一次操作发出几个请求、每个要不同的应答时用（如先 HEAD 再删除）；请求比应答多时之后的都回最后一个
    /// </remarks>
    public static RecordingHttpListener AnsweringInTurn(params Reply[] replies)
    {
        ArgumentOutOfRangeException.ThrowIfZero(replies.Length);
        return new(silent: false, proxyAuthorization: null, replies);
    }

    /// <summary>
    /// 要求基本认证的代理：没带凭据的请求回 407，带对了的回 <paramref name="status"/>
    /// </summary>
    public static RecordingHttpListener ProxyRequiring(string userName, string password, string status)
        => new(silent: false, "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(userName + ":" + password)), new Reply(status, string.Empty, []));

    /// <summary>
    /// 只收不答的对端
    /// </summary>
    public static RecordingHttpListener Silent() => new(silent: true, proxyAuthorization: null, new Reply(string.Empty, string.Empty, []));

    /// <summary>
    /// 值原样是 <paramref name="value"/> 这些字节的一行响应头
    /// </summary>
    /// <remarks>
    /// 响应头按 Latin-1 写出，每个字节写成一个 U+0000–U+00FF 的字符即原样上线：用来构造别的客户端不按 RFC 2047、直接把 UTF-8 字节写进头的应答，也能写出不是合法 UTF-8 的字节
    /// </remarks>
    public static string RawHeader(string name, ReadOnlySpan<byte> value) => name + ": " + Encoding.Latin1.GetString(value);

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

        // 先等接受循环结束再取连接：它在停止前最后接住的那条连接也要等到
        await _accepting;
        Task[] connections;
        lock (_gate)
        {
            connections = [.. _connections];
        }

        // 停止信号让每条连接的读取以取消结束，处理任务自己吞掉它
        await Task.WhenAll(connections);
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
        catch (Exception exception) when (exception is OperationCanceledException or SocketException or ObjectDisposedException or InvalidOperationException)
        {
            // 停止监听；接住一条连接后监听器已停，下一次接受先查监听状态，抛的是 InvalidOperationException
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
                    Reply reply;
                    lock (_gate)
                    {
                        _requests.Add(request);
                        reply = _replies[Math.Min(_requests.Count - 1, _replies.Length - 1)];
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

                    var headers = string.Concat(reply.Headers.Select(static header => header + "\r\n"));
                    var body = Encoding.UTF8.GetBytes(reply.Body);
                    var response = challenged
                        ? "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"test\"\r\nContent-Length: 0\r\n\r\n"
                        : reply.Verbatim
                            ? $"HTTP/1.1 {reply.Status}\r\n{headers}Connection: close\r\n\r\n"
                            : $"HTTP/1.1 {reply.Status}\r\n{headers}Content-Length: {body.Length.ToString(CultureInfo.InvariantCulture)}\r\n\r\n";
                    // 按 Latin-1 写出：U+0000–U+00FF 的每个字符恰好写成一个字节，响应头因此能带任意字节（RawHeader），只有 ASCII 的头与以前相同
                    await stream.WriteAsync(Encoding.Latin1.GetBytes(response), _stopping.Token);
                    if (!challenged && body.Length > 0)
                    {
                        await stream.WriteAsync(body, _stopping.Token);
                    }

                    // 原样写出的应答没有可靠的 Content-Length：关闭连接给出响应体的边界
                    if (reply.Verbatim && !challenged)
                    {
                        return;
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

    /// <summary>
    /// 一个应答
    /// </summary>
    /// <param name="Status">状态行（不含协议版本），如 <c>200 OK</c></param>
    /// <param name="Body">响应体，按 UTF-8 编码写出</param>
    /// <param name="Headers">另带的响应头，每项一整行，原样写出</param>
    /// <param name="Verbatim">是否原样写出响应头：为真时不另加 <c>Content-Length</c>（给定的头里有什么就写什么），写完响应体即关闭连接，响应体的边界由连接关闭给出，用来构造缺 <c>Content-Length</c> 或把它写错的应答</param>
    public sealed record Reply(string Status, string Body, string[] Headers, bool Verbatim = false);
}
