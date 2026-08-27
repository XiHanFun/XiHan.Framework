// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Options;

/// <summary>
/// <see cref="TelegramBotHandlerOptions"/> 处理器登记选项测试
/// </summary>
/// <remarks>
/// 平台刻意不做程序集反射扫描，处理器必须显式登记进这个列表才会被路由。
/// 默认为空列表是关键契约：默认零处理器意味着默认零副作用。
/// </remarks>
public class TelegramBotHandlerOptionsTests
{
    /// <summary>
    /// 默认没有登记任何处理器
    /// </summary>
    [Fact]
    public void Defaults_HandlersIsEmpty()
    {
        Assert.Empty(new TelegramBotHandlerOptions().Handlers);
    }

    /// <summary>
    /// 处理器列表只读引用但内容可增删，供注册扩展追加
    /// </summary>
    [Fact]
    public void Handlers_IsMutableCollection()
    {
        var options = new TelegramBotHandlerOptions();

        options.Handlers.Add(typeof(TestOrderCommandHandler));
        options.Handlers.Add(typeof(TestConfirmCallbackHandler));

        Assert.Equal(2, options.Handlers.Count);
        Assert.Contains(typeof(TestOrderCommandHandler), options.Handlers);
        Assert.Contains(typeof(TestConfirmCallbackHandler), options.Handlers);
    }

    /// <summary>
    /// 每个实例持有独立列表，互不串改
    /// </summary>
    [Fact]
    public void Handlers_AreNotSharedBetweenInstances()
    {
        var first = new TelegramBotHandlerOptions();
        var second = new TelegramBotHandlerOptions();

        first.Handlers.Add(typeof(TestOrderCommandHandler));

        Assert.Empty(second.Handlers);
    }
}
