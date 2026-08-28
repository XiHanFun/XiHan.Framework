// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Tests.Options;

/// <summary>
/// <see cref="TelegramBotNetworkOptions"/> 网络配置测试
/// </summary>
/// <remarks>
/// 网络配置变更会导致管理器重建全部机器人客户端，判定一旦漂移，
/// 要么代理改了不生效，要么每个刷新周期都无谓重建；这里锁死默认值与比较规则。
/// </remarks>
public class TelegramBotNetworkOptionsTests
{
    /// <summary>
    /// 默认直连官方 Bot API，超时 100 秒
    /// </summary>
    [Fact]
    public void Defaults_AreDirectConnectionWith100SecondsTimeout()
    {
        var options = new TelegramBotNetworkOptions();

        Assert.Equal(string.Empty, options.ProxyUrl);
        Assert.Equal(string.Empty, options.BaseUrl);
        Assert.Equal(100, options.TimeoutSeconds);
    }

    /// <summary>
    /// 与 null 比较恒不相同
    /// </summary>
    [Fact]
    public void IsSameAs_WhenOtherNull_ReturnsFalse()
    {
        Assert.False(new TelegramBotNetworkOptions().IsSameAs(null));
    }

    /// <summary>
    /// 两个默认实例判定相同
    /// </summary>
    [Fact]
    public void IsSameAs_TwoDefaults_ReturnsTrue()
    {
        Assert.True(new TelegramBotNetworkOptions().IsSameAs(new TelegramBotNetworkOptions()));
    }

    /// <summary>
    /// 地址按去空白 + 忽略大小写比较
    /// </summary>
    [Fact]
    public void IsSameAs_UrlComparison_IgnoresCaseAndSurroundingWhitespace()
    {
        var left = new TelegramBotNetworkOptions
        {
            ProxyUrl = "http://127.0.0.1:7890",
            BaseUrl = "https://tg.example.com"
        };
        var right = new TelegramBotNetworkOptions
        {
            ProxyUrl = "  HTTP://127.0.0.1:7890 ",
            BaseUrl = " HTTPS://TG.EXAMPLE.COM  "
        };

        Assert.True(left.IsSameAs(right));
    }

    /// <summary>
    /// 代理地址不同判定不同
    /// </summary>
    [Fact]
    public void IsSameAs_WhenProxyDiffers_ReturnsFalse()
    {
        var left = new TelegramBotNetworkOptions { ProxyUrl = "http://127.0.0.1:7890" };
        var right = new TelegramBotNetworkOptions { ProxyUrl = "socks5://127.0.0.1:1080" };

        Assert.False(left.IsSameAs(right));
    }

    /// <summary>
    /// 自建 Bot API Server 地址不同判定不同
    /// </summary>
    [Fact]
    public void IsSameAs_WhenBaseUrlDiffers_ReturnsFalse()
    {
        var left = new TelegramBotNetworkOptions { BaseUrl = "https://tg.example.com" };
        var right = new TelegramBotNetworkOptions();

        Assert.False(left.IsSameAs(right));
    }

    /// <summary>
    /// 超时秒数不同判定不同
    /// </summary>
    [Fact]
    public void IsSameAs_WhenTimeoutDiffers_ReturnsFalse()
    {
        var left = new TelegramBotNetworkOptions { TimeoutSeconds = 30 };
        var right = new TelegramBotNetworkOptions { TimeoutSeconds = 100 };

        Assert.False(left.IsSameAs(right));
    }

    /// <summary>
    /// 两侧地址均为 null 时比较不抛空引用，且判定相同
    /// </summary>
    /// <remarks>
    /// 配置绑定给出的默认值是空串而非 null，但应用层 store 可能直接回填 null，
    /// 这里保证比较逻辑对 null 是安全的。
    /// </remarks>
    [Fact]
    public void IsSameAs_WhenBothUrlsNull_DoesNotThrowAndReturnsTrue()
    {
        var left = new TelegramBotNetworkOptions { ProxyUrl = null!, BaseUrl = null! };
        var right = new TelegramBotNetworkOptions { ProxyUrl = null!, BaseUrl = null! };

        Assert.True(left.IsSameAs(right));
        Assert.True(right.IsSameAs(left));
    }
}
