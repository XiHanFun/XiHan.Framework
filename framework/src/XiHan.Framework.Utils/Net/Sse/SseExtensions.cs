#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SseExtensions
// Guid:5e8d1f9a-2c3b-47d6-8e5f-0a9b1c6d2e3f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/04/27 09:18:15
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text.Json;

namespace XiHan.Framework.Utils.Net.Sse;

/// <summary>
/// SSE扩展方法
/// </summary>
public static class SseExtensions
{
    /// <summary>
    /// 使用SseClient连接到指定URL，并处理接收到的消息
    /// </summary>
    /// <param name="url">服务器URL</param>
    /// <param name="messageHandler">消息处理回调</param>
    /// <param name="closedHandler">连接关闭回调</param>
    /// <param name="options">SSE客户端选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>SSE客户端实例</returns>
    public static async Task<SseClient> ConnectToSseAsync(
        this string url,
        Action<SseMessage>? messageHandler,
        Action<Exception?>? closedHandler = null,
        SseClientOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var client = new SseClient(options ?? new SseClientOptions());

        // 注册事件处理
        client.OnMessage += message => messageHandler?.Invoke(message);

        if (closedHandler != null)
        {
            client.OnClosed += closedHandler.Invoke;
        }

        // 连接到服务器
        await client.ConnectAsync(url, null, cancellationToken);

        return client;
    }

    /// <summary>
    /// 向流中发送JSON格式的SSE事件
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="context">SSE事件上下文</param>
    /// <param name="data">要发送的数据</param>
    /// <param name="eventType">事件类型</param>
    /// <param name="id">事件Id</param>
    /// <param name="options">JSON序列化选项</param>
    /// <returns>异步任务</returns>
    public static Task SendJsonEventAsync<T>(
        this SseEventContext context,
        T data,
        string? eventType = null,
        string? id = null,
        JsonSerializerOptions? options = null)
    {
        var json = JsonSerializer.Serialize(data, options);
        return context.SendEventAsync(json, eventType, id);
    }

    /// <summary>
    /// 创建一个SSE消息，其中数据为JSON序列化的对象
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="data">要序列化的数据</param>
    /// <param name="eventType">事件类型</param>
    /// <param name="id">事件Id</param>
    /// <param name="options">JSON序列化选项</param>
    /// <returns>SSE消息</returns>
    public static SseMessage ToJsonSseMessage<T>(
        this T data,
        string? eventType = null,
        string? id = null,
        JsonSerializerOptions? options = null)
    {
        var json = JsonSerializer.Serialize(data, options);
        return new SseMessage(json, eventType, id);
    }

    /// <summary>
    /// 反序列化SSE消息的数据为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="message">SSE消息</param>
    /// <param name="options">JSON反序列化选项</param>
    /// <returns>反序列化的对象</returns>
    public static T? DeserializeData<T>(
        this SseMessage message,
        JsonSerializerOptions? options = null)
    {
        return string.IsNullOrEmpty(message.Data) ? default : JsonSerializer.Deserialize<T>(message.Data, options);
    }

    /// <summary>
    /// 尝试反序列化SSE消息的数据为指定类型
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="message">SSE消息</param>
    /// <param name="result">反序列化结果</param>
    /// <param name="options">JSON反序列化选项</param>
    /// <returns>是否成功反序列化</returns>
    public static bool TryDeserializeData<T>(
        this SseMessage message,
        out T? result,
        JsonSerializerOptions? options = null)
    {
        result = default;

        if (string.IsNullOrEmpty(message.Data))
        {
            return false;
        }

        try
        {
            result = JsonSerializer.Deserialize<T>(message.Data, options);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
