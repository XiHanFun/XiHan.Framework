// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Net.Sse;

/// <summary>
/// SSE客户端，用于处理服务器发送事件
/// </summary>
public class SseClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly SseClientOptions _options;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// 初始化SSE客户端
    /// </summary>
    /// <param name="options">客户端配置选项</param>
    public SseClient(SseClientOptions? options)
    {
        _options = options ?? new SseClientOptions();
        _httpClient = _options.HttpClient ?? new HttpClient();

        // 设置默认超时
        if (_options.Timeout > TimeSpan.Zero)
        {
            _httpClient.Timeout = _options.Timeout;
        }
    }

    /// <summary>
    /// 事件处理的委托
    /// </summary>
    /// <param name="message">接收到的SSE消息</param>
    public delegate void MessageReceivedHandler(SseMessage message);

    /// <summary>
    /// 连接关闭的委托
    /// </summary>
    /// <param name="exception">关闭原因，如果是正常关闭则为null</param>
    public delegate void ConnectionClosedHandler(Exception? exception);

    /// <summary>
    /// 收到消息时触发
    /// </summary>
    public event MessageReceivedHandler? OnMessage;

    /// <summary>
    /// 连接关闭时触发
    /// </summary>
    public event ConnectionClosedHandler? OnClosed;

    /// <summary>
    /// 连接到SSE服务器并开始接收事件
    /// </summary>
    /// <param name="url">服务器URL</param>
    /// <param name="headers">请求头信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    public async Task ConnectAsync(string url, Dictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            // 添加SSE必需的请求头
            request.Headers.Add("Accept", "text/event-stream");
            request.Headers.Add("Cache-Control", "no-cache");

            // 添加自定义请求头
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            // 发送请求并获取响应
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _cts.Token);

            // 确保请求成功
            response.EnsureSuccessStatusCode();

            // 开始处理SSE流
            await ProcessSseStreamAsync(response, _cts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            OnClosed?.Invoke(ex);
            throw;
        }
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public void Disconnect()
    {
        _cts?.Cancel();
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Disconnect();

        if (_options.HttpClient == null)
        {
            _httpClient.Dispose();
        }

        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 处理SSE流
    /// </summary>
    /// <param name="response">HTTP响应</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    private async Task ProcessSseStreamAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var lineReader = new SseLineReader(stream);
        if (_options.MaxLineBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(_options.MaxLineBytes), "SSE 行长度上限必须大于零。");
        }

        if (_options.MaxEventBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(_options.MaxEventBytes), "SSE 事件长度上限必须大于零。");
        }

        var eventBuilder = new SseMessageBuilder();
        long eventBytes = 0;

        try
        {
            // 逐行读取SSE流
            while (!cancellationToken.IsCancellationRequested
                   && await lineReader.ReadLineAsync(_options.MaxLineBytes, cancellationToken) is { } line)
            {
                // 空行表示消息结束
                if (string.IsNullOrEmpty(line.Text))
                {
                    var message = eventBuilder.Build();
                    if (!string.IsNullOrEmpty(message.Data))
                    {
                        OnMessage?.Invoke(message);
                    }
                    eventBuilder.Reset();
                    eventBytes = 0;
                    continue;
                }

                eventBytes += line.ByteCount + 1;
                if (eventBytes > _options.MaxEventBytes)
                {
                    throw new InvalidDataException($"SSE 事件超过最大长度 {_options.MaxEventBytes} 字节。");
                }

                // 解析SSE格式的行
                if (line.Text.StartsWith("data:"))
                {
                    eventBuilder.AppendData(line.Text[5..].TrimStart());
                }
                else if (line.Text.StartsWith("event:"))
                {
                    eventBuilder.SetEvent(line.Text[6..].TrimStart());
                }
                else if (line.Text.StartsWith("id:"))
                {
                    eventBuilder.SetId(line.Text[3..].TrimStart());
                }
                else if (line.Text.StartsWith("retry:"))
                {
                    if (int.TryParse(line.Text[6..].TrimStart(), out var retry))
                    {
                        eventBuilder.SetRetry(retry);
                    }
                }
                // 注释行，忽略
                else if (line.Text.StartsWith(':'))
                {
                }
            }

            // 正常结束
            OnClosed?.Invoke(null);
        }
        catch (OperationCanceledException)
        {
            // 取消操作，正常结束
            OnClosed?.Invoke(null);
        }
    }
}
