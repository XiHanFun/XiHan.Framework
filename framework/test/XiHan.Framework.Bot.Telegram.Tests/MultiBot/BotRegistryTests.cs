// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using XiHan.Framework.Bot.Telegram.MultiBot;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.MultiBot;

/// <summary>
/// <see cref="BotRegistry"/> 机器人注册表测试
/// </summary>
/// <remarks>
/// 注册表是热路径（每条 Webhook 请求都要查一次），实现上用无锁并发字典且声明线程安全，
/// 所以除了常规增删查，这里还写了真并发用例。
/// 被替换/被移除的实例是延迟释放的（宽限期内仍在被在途请求使用），
/// 因此「移除后立刻不可查」与「实例还没被 Dispose」这两件事必须同时成立。
/// </remarks>
public class BotRegistryTests
{
    /// <summary>
    /// 新建注册表为空
    /// </summary>
    [Fact]
    public void Count_WhenEmpty_IsZero()
    {
        Assert.Equal(0, new BotRegistry().Count);
        Assert.Empty(new BotRegistry().GetAll());
    }

    /// <summary>
    /// 添加空实例时抛参数空异常
    /// </summary>
    [Fact]
    public void AddOrUpdate_WhenBotNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BotRegistry().AddOrUpdate(null!));
    }

    /// <summary>
    /// 添加后可按名称查出同一实例
    /// </summary>
    [Fact]
    public void AddOrUpdate_ThenTryGet_ReturnsSameInstance()
    {
        var registry = new BotRegistry();
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));

        registry.AddOrUpdate(bot);

        Assert.Equal(1, registry.Count);
        Assert.True(registry.TryGet("main-bot", out var found));
        Assert.Same(bot, found);
    }

    /// <summary>
    /// 名称查找忽略大小写并裁剪首尾空白
    /// </summary>
    /// <param name="name">查询名称</param>
    [Theory]
    [InlineData("main-bot")]
    [InlineData("MAIN-BOT")]
    [InlineData("  main-bot  ")]
    public void TryGet_IgnoresCaseAndSurroundingWhitespace(string name)
    {
        var registry = new BotRegistry();
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));
        registry.AddOrUpdate(bot);

        Assert.True(registry.TryGet(name, out var found));
        Assert.Same(bot, found);
    }

    /// <summary>
    /// 名称为空时查不到，且不抛异常
    /// </summary>
    /// <param name="name">查询名称</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGet_WhenNameBlank_ReturnsFalse(string? name)
    {
        var registry = new BotRegistry();
        using var bot = TelegramTestFactory.CreateBot();
        registry.AddOrUpdate(bot);

        Assert.False(registry.TryGet(name!, out var found));
        Assert.Null(found);
    }

    /// <summary>
    /// 未注册的名称查不到
    /// </summary>
    [Fact]
    public void TryGet_WhenNotRegistered_ReturnsFalse()
    {
        Assert.False(new BotRegistry().TryGet("missing-bot", out var found));
        Assert.Null(found);
    }

    /// <summary>
    /// GetRequired 在未注册时抛 KeyNotFoundException，并在消息里带上名称
    /// </summary>
    [Fact]
    public void GetRequired_WhenNotRegistered_Throws()
    {
        var exception = Assert.Throws<KeyNotFoundException>(() => new BotRegistry().GetRequired("missing-bot"));

        Assert.Contains("missing-bot", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// GetRequired 在已注册时返回实例
    /// </summary>
    [Fact]
    public void GetRequired_WhenRegistered_ReturnsInstance()
    {
        var registry = new BotRegistry();
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));
        registry.AddOrUpdate(bot);

        Assert.Same(bot, registry.GetRequired("MAIN-BOT"));
    }

    /// <summary>
    /// 同名重复添加视为替换，查出来的是新实例
    /// </summary>
    [Fact]
    public void AddOrUpdate_WhenSameName_ReplacesExistingInstance()
    {
        var registry = new BotRegistry();
        var first = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));
        using var second = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));

        registry.AddOrUpdate(first);
        registry.AddOrUpdate(second);

        Assert.Equal(1, registry.Count);
        Assert.True(registry.TryGet("main-bot", out var found));
        Assert.Same(second, found);
    }

    /// <summary>
    /// 替换是原子的：任一时刻按名称只能查出一个实例，旧实例立刻从表里消失
    /// </summary>
    /// <remarks>
    /// 旧实例本身是延迟释放的（宽限期内仍被在途请求使用），
    /// 释放时机由内部定时器决定，单测只锁「查表结果立即切换」这一半可观测契约。
    /// </remarks>
    [Fact]
    public void AddOrUpdate_WhenReplacing_SwapsLookupResultAtomically()
    {
        var registry = new BotRegistry();
        var first = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));
        using var second = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));

        registry.AddOrUpdate(first);
        Assert.Same(first, registry.GetRequired("main-bot"));

        registry.AddOrUpdate(second);

        Assert.Equal(1, registry.Count);
        Assert.Same(second, registry.GetRequired("main-bot"));
        Assert.Single(registry.GetAll());

        first.Dispose();
    }

    /// <summary>
    /// 重复添加同一个实例不触发替换逻辑
    /// </summary>
    [Fact]
    public void AddOrUpdate_WhenSameInstanceTwice_KeepsIt()
    {
        var registry = new BotRegistry();
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));

        registry.AddOrUpdate(bot);
        registry.AddOrUpdate(bot);

        Assert.Equal(1, registry.Count);
        Assert.Same(bot, registry.GetRequired("main-bot"));
    }

    /// <summary>
    /// 名称同样忽略大小写：大小写不同的同名机器人被视为同一个
    /// </summary>
    [Fact]
    public void AddOrUpdate_TreatsNameCaseInsensitively()
    {
        var registry = new BotRegistry();
        var lower = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));
        using var upper = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "MAIN-BOT"));

        registry.AddOrUpdate(lower);
        registry.AddOrUpdate(upper);

        Assert.Equal(1, registry.Count);
        lower.Dispose();
    }

    /// <summary>
    /// GetAll 按名称忽略大小写排序，输出稳定
    /// </summary>
    [Fact]
    public void GetAll_IsOrderedByNameIgnoringCase()
    {
        var registry = new BotRegistry();
        using var beta = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "Beta"));
        using var alpha = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "alpha"));
        using var gamma = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "gamma"));

        registry.AddOrUpdate(beta);
        registry.AddOrUpdate(gamma);
        registry.AddOrUpdate(alpha);

        var names = registry.GetAll().Select(x => x.Name).ToArray();

        Assert.Equal(new[] { "alpha", "Beta", "gamma" }, names);
    }

    /// <summary>
    /// 移除名称为空时返回 false
    /// </summary>
    /// <param name="name">机器人名称</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Remove_WhenNameBlank_ReturnsFalse(string? name)
    {
        Assert.False(new BotRegistry().Remove(name!));
    }

    /// <summary>
    /// 移除未注册的机器人返回 false
    /// </summary>
    [Fact]
    public void Remove_WhenNotRegistered_ReturnsFalse()
    {
        Assert.False(new BotRegistry().Remove("missing-bot"));
    }

    /// <summary>
    /// 移除后立即查不到，且计数归零
    /// </summary>
    [Fact]
    public void Remove_WhenRegistered_RemovesImmediately()
    {
        var registry = new BotRegistry();
        var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));
        registry.AddOrUpdate(bot);

        Assert.True(registry.Remove("  MAIN-BOT  "));
        Assert.Equal(0, registry.Count);
        Assert.False(registry.TryGet("main-bot", out _));

        bot.Dispose();
    }

    /// <summary>
    /// 重复移除同一个名称只成功一次
    /// </summary>
    [Fact]
    public void Remove_CalledTwice_SucceedsOnlyOnce()
    {
        var registry = new BotRegistry();
        var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));
        registry.AddOrUpdate(bot);

        Assert.True(registry.Remove("main-bot"));
        Assert.False(registry.Remove("main-bot"));

        bot.Dispose();
    }

    /// <summary>
    /// 并发添加不同名称的机器人，全部可见且计数正确
    /// </summary>
    /// <remarks>
    /// 注册表宣称线程安全（无锁并发字典），管理器刷新与 Webhook 热路径会同时访问它。
    /// </remarks>
    [Fact]
    public void AddOrUpdate_ConcurrentDistinctNames_AllVisible()
    {
        const int botCount = 64;
        var registry = new BotRegistry();
        var bots = new ConcurrentBag<BotInstance>();

        Parallel.For(0, botCount, index =>
        {
            var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: $"bot-{index}"));
            bots.Add(bot);
            registry.AddOrUpdate(bot);
        });

        Assert.Equal(botCount, registry.Count);
        for (var index = 0; index < botCount; index++)
        {
            Assert.True(registry.TryGet($"bot-{index}", out _));
        }

        foreach (var bot in bots)
        {
            bot.Dispose();
        }
    }

    /// <summary>
    /// 并发读写混合执行不抛异常，且读到的实例始终非空
    /// </summary>
    [Fact]
    public void TryGet_ConcurrentWithWrites_NeverThrows()
    {
        const int iterations = 200;
        var registry = new BotRegistry();
        var bots = new ConcurrentBag<BotInstance>();
        using var seed = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "hot-bot"));
        registry.AddOrUpdate(seed);

        Parallel.Invoke(
            () =>
            {
                for (var index = 0; index < iterations; index++)
                {
                    var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: $"bot-{index}"));
                    bots.Add(bot);
                    registry.AddOrUpdate(bot);
                }
            },
            () =>
            {
                for (var index = 0; index < iterations; index++)
                {
                    if (registry.TryGet("hot-bot", out var found))
                    {
                        Assert.NotNull(found);
                    }
                }
            },
            () =>
            {
                for (var index = 0; index < iterations; index++)
                {
                    _ = registry.GetAll();
                }
            });

        Assert.True(registry.TryGet("hot-bot", out _));

        foreach (var bot in bots)
        {
            bot.Dispose();
        }
    }
}
