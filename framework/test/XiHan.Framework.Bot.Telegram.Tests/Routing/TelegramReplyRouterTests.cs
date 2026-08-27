// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Bot.Telegram.Routing;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Routing;

/// <summary>
/// <see cref="TelegramReplyRouter"/> 回复消息路由器测试
/// </summary>
/// <remarks>
/// 回复链与消息链结构相同（按 Order 排序、首个命中即停），
/// 但登记来源是目录的 ReplyHandlerTypes 而不是 MessageHandlerTypes——
/// 两条链一旦串了，用户回复消息会被普通消息处理器抢走。
/// </remarks>
public class TelegramReplyRouterTests
{
    /// <summary>
    /// 上下文为空时抛参数空异常
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenContextNull_Throws()
    {
        var router = CreateRouter(typeof(TestReplyHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(new HandlerRecorder());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await router.HandleAsync(null!, provider, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 服务提供者为空时抛参数空异常
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenProviderNull_Throws()
    {
        var router = CreateRouter(typeof(TestReplyHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await router.HandleAsync(context, null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 没有登记回复处理器时不处理
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenNoHandlerRegistered_ReturnsFalse()
    {
        var router = CreateRouter();
        var recorder = new HandlerRecorder();
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder);
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, CreateReplyUpdate());

        Assert.False(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 命中的回复处理器接手消息
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenHandlerMatches_InvokesItAndReturnsTrue()
    {
        var router = CreateRouter(typeof(TestReplyHandler));
        var recorder = new HandlerRecorder();
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestReplyHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, CreateReplyUpdate());

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal(1, recorder.CountOf(TestReplyHandler.HandlerName));
        Assert.Equal("回复内容", recorder.Invocations[0].Data);
    }

    /// <summary>
    /// CanHandle 返回 false 时不处理
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenHandlerCannotHandle_ReturnsFalse()
    {
        var router = CreateRouter(typeof(TestReplyHandler));
        var recorder = new HandlerRecorder { ReplyCanHandle = false };
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestReplyHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, CreateReplyUpdate());

        Assert.False(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Empty(recorder.Invocations);
    }

    /// <summary>
    /// 已登记但未注册 DI 的回复处理器被跳过，不抛异常
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenHandlerMissingFromDi_ReturnsFalse()
    {
        var router = CreateRouter(typeof(TestReplyHandler));
        var recorder = new HandlerRecorder();
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder);
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, CreateReplyUpdate());

        Assert.False(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 同时实现消息链与回复链的处理器会被回复路由解析出来
    /// </summary>
    [Fact]
    public async Task HandleAsync_ResolvesHandlerImplementingBothChains()
    {
        var router = CreateRouter(typeof(TestMessageAndReplyHandler));
        var recorder = new HandlerRecorder();
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestMessageAndReplyHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, CreateReplyUpdate());

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal(1, recorder.CountOf(TestMessageAndReplyHandler.HandlerName));
    }

    /// <summary>
    /// 处理器抛出的异常原样冒泡
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenHandlerThrows_PropagatesException()
    {
        var router = CreateRouter(typeof(TestReplyHandler));
        var recorder = new HandlerRecorder { ExceptionToThrow = new InvalidOperationException("回复处理器炸了") };
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestReplyHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, CreateReplyUpdate());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));

        Assert.Equal("回复处理器炸了", exception.Message);
    }

    /// <summary>
    /// 构造一条回复型 Update
    /// </summary>
    /// <returns>Update</returns>
    private static global::Telegram.Bot.Types.Update CreateReplyUpdate()
    {
        var message = TelegramTestFactory.CreateMessage(text: "回复内容");
        message.ReplyToMessage = TelegramTestFactory.CreateMessage(text: "被回复的消息", messageId: 5);
        return new global::Telegram.Bot.Types.Update { Id = 1, Message = message };
    }

    /// <summary>
    /// 构造回复路由器
    /// </summary>
    /// <param name="handlerTypes">登记进目录的处理器类型</param>
    /// <returns>回复路由器</returns>
    private static TelegramReplyRouter CreateRouter(params Type[] handlerTypes)
    {
        return new TelegramReplyRouter(
            TelegramTestFactory.CreateCatalog(handlerTypes),
            NullLogger<TelegramReplyRouter>.Instance);
    }
}
