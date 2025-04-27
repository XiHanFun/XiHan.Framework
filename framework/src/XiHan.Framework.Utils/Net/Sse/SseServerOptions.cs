#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SseServerOptions
// Guid:2a3b4c5d-6e7f-48a9-b0c1-d2e3f4a5b6c7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/04/27 09:19:05
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
    /// <param name="lastEventId">最后事件ID</param>
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
    /// 最后事件ID
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
    /// <param name="id">事件ID</param>
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

/// <summary>
/// SSE服务器配置选项
/// </summary>
public class SseServerOptions
{
    /// <summary>
    /// 初始化SSE服务器配置选项
    /// </summary>
    public SseServerOptions()
    {
        // 默认事件处理程序，保持连接打开但不发送任何事件
        EventHandler = (_, ct) => Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }

    /// <summary>
    /// 获取或设置事件处理委托
    /// </summary>
    public Func<SseEventContext, CancellationToken, Task> EventHandler { get; set; }

    /// <summary>
    /// 获取或设置心跳间隔
    /// </summary>
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 获取自定义响应头集合
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; } = [];
}
