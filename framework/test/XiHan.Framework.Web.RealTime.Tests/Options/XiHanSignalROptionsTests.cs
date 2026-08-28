// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Web.RealTime.Options;

namespace XiHan.Framework.Web.RealTime.Tests.Options;

/// <summary>
/// 曦寒 SignalR 配置选项测试
/// </summary>
/// <remarks>
/// 该选项类没有 Validate 方法，契约全在默认值上：默认值同时决定了未配置时桥接给 HubOptions 的实际取值。
/// 配置节名被 appsettings 直接引用，改名会让线上配置静默失效，因此单独锁死。
/// </remarks>
public class XiHanSignalROptionsTests
{
    /// <summary>
    /// 配置节路径是对外契约，不允许漂移
    /// </summary>
    [Fact]
    public void SectionName_IsStableConfigurationPath()
    {
        Assert.Equal("XiHan:Web:RealTime:SignalR", XiHanSignalROptions.SectionName);
    }

    /// <summary>
    /// 默认关闭详细错误信息
    /// </summary>
    /// <remarks>
    /// 详细错误会把服务端异常文本推给浏览器，默认必须是关闭的。
    /// </remarks>
    [Fact]
    public void Constructor_ByDefault_DisablesDetailedErrors()
    {
        var options = new XiHanSignalROptions();

        Assert.False(options.EnableDetailedErrors);
    }

    /// <summary>
    /// 默认的三个时间窗口取值
    /// </summary>
    [Fact]
    public void Constructor_ByDefault_UsesFrameworkAlignedTimeouts()
    {
        var options = new XiHanSignalROptions();

        Assert.Equal(TimeSpan.FromSeconds(15), options.KeepAliveInterval);
        Assert.Equal(TimeSpan.FromSeconds(30), options.ClientTimeoutInterval);
        Assert.Equal(TimeSpan.FromSeconds(15), options.HandshakeTimeout);
    }

    /// <summary>
    /// 客户端超时窗口大于保活间隔
    /// </summary>
    /// <remarks>
    /// 保活间隔若不小于客户端超时窗口，连接会在保活包送达前先被判定超时，这是一条必须成立的不变量。
    /// </remarks>
    [Fact]
    public void Constructor_ByDefault_KeepsClientTimeoutLargerThanKeepAlive()
    {
        var options = new XiHanSignalROptions();

        Assert.True(options.ClientTimeoutInterval > options.KeepAliveInterval);
    }

    /// <summary>
    /// 默认的容量类取值
    /// </summary>
    [Fact]
    public void Constructor_ByDefault_UsesFrameworkAlignedCapacities()
    {
        var options = new XiHanSignalROptions();

        Assert.Equal(32 * 1024, options.MaximumReceiveMessageSize);
        Assert.Equal(10, options.StreamBufferCapacity);
        Assert.Equal(1, options.MaximumParallelInvocationsPerClient);
    }

    /// <summary>
    /// 默认启用连接指标
    /// </summary>
    [Fact]
    public void Constructor_ByDefault_EnablesConnectionMetrics()
    {
        var options = new XiHanSignalROptions();

        Assert.True(options.EnableConnectionMetrics);
    }

    /// <summary>
    /// 最大接收消息大小可以置空表示不限制
    /// </summary>
    [Fact]
    public void MaximumReceiveMessageSize_CanBeClearedToUnlimited()
    {
        var options = new XiHanSignalROptions
        {
            MaximumReceiveMessageSize = null
        };

        Assert.Null(options.MaximumReceiveMessageSize);
    }

    /// <summary>
    /// 全部选项可写
    /// </summary>
    [Fact]
    public void Properties_AreMutable()
    {
        var options = new XiHanSignalROptions
        {
            EnableDetailedErrors = true,
            KeepAliveInterval = TimeSpan.FromSeconds(5),
            ClientTimeoutInterval = TimeSpan.FromSeconds(11),
            HandshakeTimeout = TimeSpan.FromSeconds(7),
            MaximumReceiveMessageSize = 4096,
            StreamBufferCapacity = 3,
            MaximumParallelInvocationsPerClient = 4,
            EnableConnectionMetrics = false
        };

        Assert.True(options.EnableDetailedErrors);
        Assert.Equal(TimeSpan.FromSeconds(5), options.KeepAliveInterval);
        Assert.Equal(TimeSpan.FromSeconds(11), options.ClientTimeoutInterval);
        Assert.Equal(TimeSpan.FromSeconds(7), options.HandshakeTimeout);
        Assert.Equal(4096, options.MaximumReceiveMessageSize);
        Assert.Equal(3, options.StreamBufferCapacity);
        Assert.Equal(4, options.MaximumParallelInvocationsPerClient);
        Assert.False(options.EnableConnectionMetrics);
    }
}
