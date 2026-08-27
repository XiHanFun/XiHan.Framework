// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Bot.Strategy;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// <see cref="BotStrategyNames"/> 常量测试
/// </summary>
/// <remarks>
/// 策略名同时是消息 Data 里的选择键与选项默认值，必须与各策略实现的 Name 属性一致。
/// </remarks>
public class BotStrategyNamesTests
{
    /// <summary>
    /// 策略名称取值不漂移
    /// </summary>
    [Fact]
    public void StrategyNames_AreStable()
    {
        Assert.Equal("Broadcast", BotStrategyNames.Broadcast);
        Assert.Equal("Failover", BotStrategyNames.Failover);
        Assert.Equal("Priority", BotStrategyNames.Priority);
    }

    /// <summary>
    /// 常量与策略实现的 Name 属性一一对应
    /// </summary>
    [Fact]
    public void StrategyNames_MatchStrategyImplementations()
    {
        var options = new TestOptionsWrapper<XiHanBotOptions>(new XiHanBotOptions());

        Assert.Equal(BotStrategyNames.Broadcast, new BroadcastStrategy(options, NullLogger<BroadcastStrategy>.Instance).Name);
        Assert.Equal(BotStrategyNames.Failover, new FailoverStrategy(NullLogger<FailoverStrategy>.Instance).Name);
        Assert.Equal(BotStrategyNames.Priority, new PriorityStrategy(NullLogger<PriorityStrategy>.Instance).Name);
    }

    /// <summary>
    /// 默认策略指向广播
    /// </summary>
    [Fact]
    public void DefaultStrategy_IsBroadcast()
    {
        Assert.Equal(BotStrategyNames.Broadcast, new XiHanBotOptions().DefaultStrategy);
    }
}
