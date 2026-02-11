#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SseMessage
// Guid:c1f83a2e-6b4d-48e9-95a0-7c6d215896f2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/04/27 09:15:23
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Net.Sse;

/// <summary>
/// SSE消息，表示服务器发送的单个事件消息
/// </summary>
public class SseMessage
{
    /// <summary>
    /// 创建新的SSE消息
    /// </summary>
    public SseMessage()
    { }

    /// <summary>
    /// 创建新的SSE消息
    /// </summary>
    /// <param name="data">消息数据</param>
    /// <param name="eventType">事件类型</param>
    /// <param name="id">消息唯一标识</param>
    public SseMessage(string data, string? eventType = null, string? id = null)
    {
        Data = data;
        if (!string.IsNullOrEmpty(eventType))
        {
            Event = eventType;
        }
        Id = id;
    }

    /// <summary>
    /// 获取或设置消息唯一标识
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// 获取或设置事件类型
    /// </summary>
    public string Event { get; set; } = "message";

    /// <summary>
    /// 获取或设置消息数据
    /// </summary>
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置重试间隔(毫秒)
    /// </summary>
    public int? Retry { get; set; }

    /// <summary>
    /// 获取或设置消息接收时间
    /// </summary>
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 返回消息的字符串表示
    /// </summary>
    /// <returns>消息字符串</returns>
    public override string ToString()
    {
        return $"[{Event}] {(string.IsNullOrEmpty(Id) ? "" : $"Id: {Id}, ")}Data: {Data}";
    }
}
