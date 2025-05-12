#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SseServer
// Guid:4d6e8f9a-0b1c-45d2-9e3f-7a8b9c0d1e2f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/04/27 09:19:05
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;

namespace XiHan.Framework.Utils.Net.Sse;

/// <summary>
/// Server-Sent Events
/// SSE服务器，用于实现服务端事件推送
/// </summary>
public class SseServer
{
    private readonly SseServerOptions _options;

    /// <summary>
    /// 初始化SSE服务器
    /// </summary>
    /// <param name="options">服务器配置选项</param>
    public SseServer(SseServerOptions? options = null)
    {
        _options = options ?? new SseServerOptions();
    }

    /// <summary>
    /// 发送SSE消息
    /// </summary>
    /// <param name="stream">输出流</param>
    /// <param name="message">SSE消息</param>
    /// <returns>异步任务</returns>
    public static async Task SendEventAsync(Stream stream, SseMessage message)
    {
        var messageBuilder = new StringBuilder();

        // 添加事件ID
        if (!string.IsNullOrEmpty(message.Id))
        {
            messageBuilder.AppendLine($"id: {message.Id}");
        }

        // 添加事件类型
        if (!string.IsNullOrEmpty(message.Event) && message.Event != "message")
        {
            messageBuilder.AppendLine($"event: {message.Event}");
        }

        // 添加重试时间
        if (message.Retry.HasValue)
        {
            messageBuilder.AppendLine($"retry: {message.Retry.Value}");
        }

        // 添加数据
        foreach (var line in message.Data.Split('\n'))
        {
            messageBuilder.AppendLine($"data: {line}");
        }

        // 消息结束添加空行
        messageBuilder.AppendLine();

        var messageBytes = Encoding.UTF8.GetBytes(messageBuilder.ToString());
        await stream.WriteAsync(messageBytes);
        await stream.FlushAsync();
    }

    /// <summary>
    /// 发送数据
    /// </summary>
    /// <param name="stream">输出流</param>
    /// <param name="data">数据内容</param>
    /// <param name="eventType">事件类型</param>
    /// <param name="id">事件ID</param>
    /// <returns>异步任务</returns>
    public static Task SendEventAsync(Stream stream, string data, string? eventType = null, string? id = null)
    {
        var message = new SseMessage(data, eventType, id);
        return SendEventAsync(stream, message);
    }

    /// <summary>
    /// 处理SSE请求
    /// </summary>
    /// <param name="stream">输出流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    public Task ProcessRequestAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        return ProcessRequestAsync(stream, null, cancellationToken);
    }

    /// <summary>
    /// 处理SSE请求
    /// </summary>
    /// <param name="stream">输出流</param>
    /// <param name="lastEventId">客户端发送的Last-Event-ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    public async Task ProcessRequestAsync(Stream stream, string? lastEventId, CancellationToken cancellationToken = default)
    {
        // 写入SSE响应头
        await WriteHeadersAsync(stream);

        // 如果设置了保持连接心跳，启动心跳定时器
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_options.KeepAliveInterval > TimeSpan.Zero)
        {
            _ = StartKeepAliveTimerAsync(stream, linkedCts.Token);
        }

        try
        {
            // 提供流以供事件处理
            await _options.EventHandler(new SseEventContext(stream, lastEventId, this), linkedCts.Token);
        }
        finally
        {
            // 确保取消心跳定时器
            linkedCts.Cancel();
        }
    }

    /// <summary>
    /// 写入SSE响应头
    /// </summary>
    /// <param name="stream">输出流</param>
    /// <returns>异步任务</returns>
    private async Task WriteHeadersAsync(Stream stream)
    {
        var headers = new StringBuilder();

        // 添加自定义响应头
        foreach (var header in _options.CustomHeaders)
        {
            headers.AppendLine($"{header.Key}: {header.Value}");
        }

        // 添加标准SSE响应头
        headers.AppendLine("Content-Type: text/event-stream");
        headers.AppendLine("Cache-Control: no-cache");
        headers.AppendLine("Connection: keep-alive");
        headers.AppendLine();

        var headerBytes = Encoding.UTF8.GetBytes(headers.ToString());
        await stream.WriteAsync(headerBytes);
        await stream.FlushAsync();
    }

    /// <summary>
    /// 启动心跳定时器
    /// </summary>
    /// <param name="stream">输出流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    private async Task StartKeepAliveTimerAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_options.KeepAliveInterval, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    continue;
                }

                // 发送注释作为心跳
                var commentBytes = Encoding.UTF8.GetBytes($": {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n");
                await stream.WriteAsync(commentBytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消，忽略异常
        }
        catch (Exception)
        {
            // 连接可能已关闭，忽略异常
        }
    }
}
