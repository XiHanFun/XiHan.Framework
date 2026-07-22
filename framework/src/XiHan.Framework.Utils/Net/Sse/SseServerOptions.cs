// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Net.Sse;

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
