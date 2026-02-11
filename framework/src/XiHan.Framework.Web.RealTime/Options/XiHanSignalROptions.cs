#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanSignalROptions
// Guid:8c1a4f3e-6b2d-4e1f-9a3c-7d8e5f4c3b2a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/06 04:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
