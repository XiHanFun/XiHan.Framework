// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Tests.Options;

/// <summary>
/// <see cref="TelegramBotRetryOptions"/> 发送重试配置测试
/// </summary>
/// <remarks>
/// 默认值直接决定发送门面在 429 限流下的行为曲线：3 次重试、500ms 起指数退避、单次最长等 10 秒。
/// 这四个默认值是运维预期，调整需要有意识地改这里的断言。
/// </remarks>
public class TelegramBotRetryOptionsTests
{
    /// <summary>
    /// 默认重试 3 次，退避基数 500ms、上限 10 秒，最终失败通知管理员
    /// </summary>
    [Fact]
    public void Defaults_AreThreeRetriesWithExponentialBackoff()
    {
        var options = new TelegramBotRetryOptions();

        Assert.Equal(3, options.MaxRetries);
        Assert.Equal(500, options.BaseDelayMs);
        Assert.Equal(10_000, options.MaxDelayMs);
        Assert.True(options.NotifyAdminOnFinalFailure);
    }

    /// <summary>
    /// 退避上限不小于退避基数，否则指数退避会被立即钳死成一个常量
    /// </summary>
    [Fact]
    public void Defaults_MaxDelayIsGreaterThanBaseDelay()
    {
        var options = new TelegramBotRetryOptions();

        Assert.True(options.MaxDelayMs > options.BaseDelayMs);
    }

    /// <summary>
    /// 全部属性可写，便于应用层整体覆盖
    /// </summary>
    [Fact]
    public void Properties_AreMutable()
    {
        var options = new TelegramBotRetryOptions
        {
            MaxRetries = 0,
            BaseDelayMs = 1,
            MaxDelayMs = 2,
            NotifyAdminOnFinalFailure = false
        };

        Assert.Equal(0, options.MaxRetries);
        Assert.Equal(1, options.BaseDelayMs);
        Assert.Equal(2, options.MaxDelayMs);
        Assert.False(options.NotifyAdminOnFinalFailure);
    }
}
