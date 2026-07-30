// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Net.Sse;

/// <summary>
/// SSE服务器处理上下文
/// </summary>
public class SseEventContext
{
    /// <summary>
    /// 初始化SSE事件上下文
    /// </summary>
    /// <param name="stream">输出流</param>
    /// <param name="lastEventId">最后事件唯一标识</param>
    /// <param name="server">服务器实例</param>
    public SseEventContext(Stream stream, string? lastEventId, SseServer server)
    {
        Stream = stream;
        LastEventId = lastEventId;
        Server = server;
    }

    /// <summary>
    /// 输出流
    /// </summary>
    public Stream Stream { get; }

    /// <summary>
    /// 最后事件唯一标识
    /// </summary>
    public string? LastEventId { get; }

    /// <summary>
    /// SSE服务器实例
    /// </summary>
    public SseServer Server { get; }

    /// <summary>
    /// 发送事件
    /// </summary>
    /// <param name="data">数据内容</param>
    /// <param name="eventType">事件类型</param>
    /// <param name="id">事件唯一标识</param>
    /// <returns>异步任务</returns>
    public Task SendEventAsync(string data, string? eventType = null, string? id = null)
    {
        return SseServer.SendEventAsync(Stream, data, eventType, id);
    }

    /// <summary>
    /// 发送事件
    /// </summary>
    /// <param name="message">SSE消息</param>
    /// <returns>异步任务</returns>
    public Task SendEventAsync(SseMessage message)
    {
        return SseServer.SendEventAsync(Stream, message);
    }
}
