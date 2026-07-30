// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.RealTime.Options;

/// <summary>
/// 曦寒 SignalR 配置选项
/// </summary>
public class XiHanSignalROptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Web:RealTime:SignalR";

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanSignalROptions()
    {
        EnableDetailedErrors = false;
        KeepAliveInterval = TimeSpan.FromSeconds(15);
        ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        HandshakeTimeout = TimeSpan.FromSeconds(15);
        MaximumReceiveMessageSize = 32 * 1024; // 32KB
        StreamBufferCapacity = 10;
        MaximumParallelInvocationsPerClient = 1;
        EnableConnectionMetrics = true;
    }

    /// <summary>
    /// 是否启用详细错误信息
    /// </summary>
    public bool EnableDetailedErrors { get; set; }

    /// <summary>
    /// 保持连接间隔时间
    /// </summary>
    public TimeSpan KeepAliveInterval { get; set; }

    /// <summary>
    /// 客户端超时时间
    /// </summary>
    public TimeSpan ClientTimeoutInterval { get; set; }

    /// <summary>
    /// 握手超时时间
    /// </summary>
    public TimeSpan HandshakeTimeout { get; set; }

    /// <summary>
    /// 最大接收消息大小（字节）
    /// </summary>
    public long? MaximumReceiveMessageSize { get; set; }

    /// <summary>
    /// 流缓冲容量
    /// </summary>
    public int StreamBufferCapacity { get; set; }

    /// <summary>
    /// 每个客户端的最大并行调用数
    /// </summary>
    public int MaximumParallelInvocationsPerClient { get; set; }

    /// <summary>
    /// 是否启用连接指标
    /// </summary>
    public bool EnableConnectionMetrics { get; set; }
}
