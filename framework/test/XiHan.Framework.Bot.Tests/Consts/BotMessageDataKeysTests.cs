// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Bot.Pipeline;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Strategy;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// <see cref="BotMessageDataKeys"/> 常量测试
/// </summary>
/// <remarks>
/// Strategy 键是调用方在消息上指定策略的唯一入口，键名变化会让所有既有调用静默退回默认策略，
/// 所以除了锁死字面量，还要验证调度器确实按这个键取策略。
/// </remarks>
public class BotMessageDataKeysTests
{
    /// <summary>
    /// 策略键名取值不漂移
    /// </summary>
    [Fact]
    public void Strategy_KeyName_IsStable()
    {
        Assert.Equal("Strategy", BotMessageDataKeys.Strategy);
    }

    /// <summary>
    /// 调度器按该键名读取策略
    /// </summary>
    [Fact]
    public async Task Strategy_Key_IsHonoredByDispatcher()
    {
        var options = new XiHanBotOptions();
        var wrapped = new TestOptionsWrapper<XiHanBotOptions>(options);
        var primary = FakeBotProvider.AlwaysSuccess("A");
        var secondary = FakeBotProvider.AlwaysSuccess("B");
        var manager = new BotProviderManager(new IBotProvider[] { primary, secondary }, wrapped);
        var dispatcher = new BotDispatcher(
            manager,
            Array.Empty<IBotPipeline>(),
            new IBotStrategy[]
            {
                new BroadcastStrategy(wrapped, NullLogger<BroadcastStrategy>.Instance),
                new PriorityStrategy(NullLogger<PriorityStrategy>.Instance)
            },
            wrapped,
            NullLogger<BotDispatcher>.Instance);

        var message = new BotMessage { Content = "hi" };
        message.Data[BotMessageDataKeys.Strategy] = BotStrategyNames.Priority;

        var result = await dispatcher.DispatchAsync(message, null, TestContext.Current.CancellationToken);

        Assert.Single(result.Results);
        Assert.Equal(1, primary.CallCount);
        Assert.Equal(0, secondary.CallCount);
    }
}
