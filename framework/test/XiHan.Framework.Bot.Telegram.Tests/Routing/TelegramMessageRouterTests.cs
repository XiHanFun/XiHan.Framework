// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Bot.Telegram.Routing;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Routing;

/// <summary>
/// <see cref="TelegramMessageRouter"/> 普通消息路由器测试
/// </summary>
/// <remarks>
/// 消息链是「按 Order 升序、首个 CanHandle 命中即停」。
/// 登记顺序与执行顺序必须解耦：目录按登记顺序存类型，路由器负责按 Order 重排，
/// 否则应用层调整 Order 就不生效了。
/// </remarks>
public class TelegramMessageRouterTests
{
    /// <summary>
    /// 上下文为空时抛参数空异常
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenContextNull_Throws()
    {
        var router = CreateRouter(typeof(TestEarlyMessageHandler));
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
        var router = CreateRouter(typeof(TestEarlyMessageHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate());

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await router.HandleAsync(context, null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 没有登记任何消息处理器时不处理
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenNoHandlerRegistered_ReturnsFalse()
    {
        var router = CreateRouter();
        var recorder = new HandlerRecorder();
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder);
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate());

        Assert.False(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Empty(recorder.Invocations);
    }

    /// <summary>
    /// 命中的首个处理器接手消息，返回已处理
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenHandlerMatches_InvokesItAndReturnsTrue()
    {
        var router = CreateRouter(typeof(TestEarlyMessageHandler));
        var recorder = new HandlerRecorder();
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestEarlyMessageHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "你好"));

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal(1, recorder.CountOf(TestEarlyMessageHandler.HandlerName));
        Assert.Equal("你好", recorder.Invocations[0].Data);
    }

    /// <summary>
    /// 多个处理器按 Order 升序执行，且登记顺序不影响执行顺序
    /// </summary>
    [Fact]
    public async Task HandleAsync_ExecutesHandlersOrderedByOrderProperty()
    {
        var router = CreateRouter(typeof(TestLateMessageHandler), typeof(TestEarlyMessageHandler));
        var recorder = new HandlerRecorder();
        using var provider = TelegramTestFactory.CreateHandlerProvider(
            recorder, typeof(TestLateMessageHandler), typeof(TestEarlyMessageHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate());

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Single(recorder.Invocations);
        Assert.Equal(TestEarlyMessageHandler.HandlerName, recorder.Invocations[0].Handler);
    }

    /// <summary>
    /// 首个命中即停，后续处理器不再执行
    /// </summary>
    [Fact]
    public async Task HandleAsync_StopsAfterFirstMatch()
    {
        var router = CreateRouter(typeof(TestEarlyMessageHandler), typeof(TestLateMessageHandler));
        var recorder = new HandlerRecorder();
        using var provider = TelegramTestFactory.CreateHandlerProvider(
            recorder, typeof(TestEarlyMessageHandler), typeof(TestLateMessageHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate());

        await router.HandleAsync(context, provider, TestContext.Current.CancellationToken);

        Assert.Equal(0, recorder.CountOf(TestLateMessageHandler.HandlerName));
    }

    /// <summary>
    /// CanHandle 全部返回 false 时不处理
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenNoHandlerCanHandle_ReturnsFalse()
    {
        var router = CreateRouter(typeof(TestEarlyMessageHandler));
        var recorder = new HandlerRecorder { MessageCanHandle = false };
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestEarlyMessageHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate());

        Assert.False(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Empty(recorder.Invocations);
    }

    /// <summary>
    /// 不命中的处理器会被跳过，由后面的处理器接手
    /// </summary>
    [Fact]
    public async Task HandleAsync_SkipsHandlersThatCannotHandle()
    {
        var router = CreateRouter(typeof(TestNeverMessageHandler), typeof(TestEarlyMessageHandler));
        var recorder = new HandlerRecorder();
        using var provider = TelegramTestFactory.CreateHandlerProvider(
            recorder, typeof(TestNeverMessageHandler), typeof(TestEarlyMessageHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate());

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal(0, recorder.CountOf(TestNeverMessageHandler.HandlerName));
        Assert.Equal(1, recorder.CountOf(TestEarlyMessageHandler.HandlerName));
    }

    /// <summary>
    /// 已登记但未注册 DI 的处理器被跳过，不影响其余处理器
    /// </summary>
    [Fact]
    public async Task HandleAsync_SkipsHandlersMissingFromDi()
    {
        var router = CreateRouter(typeof(TestLateMessageHandler), typeof(TestEarlyMessageHandler));
        var recorder = new HandlerRecorder();
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestLateMessageHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate());

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal(1, recorder.CountOf(TestLateMessageHandler.HandlerName));
    }

    /// <summary>
    /// 全部登记的处理器都没注册 DI 时不处理，也不抛异常
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenAllHandlersMissingFromDi_ReturnsFalse()
    {
        var router = CreateRouter(typeof(TestEarlyMessageHandler));
        var recorder = new HandlerRecorder();
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder);
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate());

        Assert.False(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 处理器抛出的异常原样冒泡给分发器统一兜底
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenHandlerThrows_PropagatesException()
    {
        var router = CreateRouter(typeof(TestEarlyMessageHandler));
        var recorder = new HandlerRecorder { ExceptionToThrow = new InvalidOperationException("消息处理器炸了") };
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestEarlyMessageHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));

        Assert.Equal("消息处理器炸了", exception.Message);
    }

    /// <summary>
    /// 构造消息路由器
    /// </summary>
    /// <param name="handlerTypes">登记进目录的处理器类型</param>
    /// <returns>消息路由器</returns>
    private static TelegramMessageRouter CreateRouter(params Type[] handlerTypes)
    {
        return new TelegramMessageRouter(
            TelegramTestFactory.CreateCatalog(handlerTypes),
            NullLogger<TelegramMessageRouter>.Instance);
    }
}
