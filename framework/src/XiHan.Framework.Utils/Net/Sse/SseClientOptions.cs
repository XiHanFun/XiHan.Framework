// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Net.Sse;

/// <summary>
/// SSE客户端配置选项
/// </summary>
public class SseClientOptions
{
    /// <summary>
    /// 获取或设置HTTP客户端实例。如果为null，将创建新的HttpClient实例
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>
    /// 获取或设置请求超时时间。默认为100秒
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>
    /// 获取或设置在连接断开后是否自动重连
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// 获取或设置重连尝试次数。设置为-1表示无限次尝试
    /// </summary>
    public int ReconnectAttempts { get; set; } = 3;

    /// <summary>
    /// 获取或设置重连基础延迟时间(毫秒)
    /// </summary>
    public int ReconnectDelayMs { get; set; } = 1000;

    /// <summary>
    /// 获取或设置最大重连延迟时间(毫秒)
    /// </summary>
    public int MaxReconnectDelayMs { get; set; } = 30000;

    /// <summary>
    /// 获取或设置是否使用指数回退策略进行重连
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// 获取或设置重连时是否携带上次收到的事件唯一标识 (Last-Event-Id)
    /// </summary>
    public bool IncludeLastEventIdOnReconnect { get; set; } = true;
}
