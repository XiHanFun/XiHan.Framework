#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SseEventContext
// Guid:bb8245af-e7a4-4283-a943-35377f7a726b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/27 15:34:06
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
    /// <param name="lastEventId">最后事件Id</param>
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
    /// 最后事件Id
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
    /// <param name="id">事件Id</param>
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
