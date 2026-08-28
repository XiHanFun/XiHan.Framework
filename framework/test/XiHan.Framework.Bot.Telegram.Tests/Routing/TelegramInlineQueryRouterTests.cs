// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Bot.Telegram.Routing;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Routing;

/// <summary>
/// <see cref="TelegramInlineQueryRouter"/> 内联查询路由器测试
/// </summary>
/// <remarks>
/// 内联查询命中处理器之后必须调用真实的 AnswerInlineQuery，所以「命中且处理成功」这条分支
/// 无法在离线前提下断言。这里覆盖全部不发请求的分支：无内联查询、无处理器、未注册 DI、
/// CanHandle 不命中，以及「处理器抛异常 → 吞异常记日志并返回已处理」——
/// 最后这条恰好也证明了处理器确实拿到了裁剪后的查询文本。
/// </remarks>
public class TelegramInlineQueryRouterTests
{
    /// <summary>
    /// 上下文为空时抛参数空异常
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenContextNull_Throws()
    {
        var router = CreateRouter(typeof(TestInlineQueryHandler));
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
        var router = CreateRouter(typeof(TestInlineQueryHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateInlineQueryUpdate("查询"));

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await router.HandleAsync(context, null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 非内联查询更新不处理
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenUpdateHasNoInlineQuery_ReturnsFalse()
    {
        var router = CreateRouter(typeof(TestInlineQueryHandler));
        var recorder = new HandlerRecorder();
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestInlineQueryHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate());

        Assert.False(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Empty(recorder.Invocations);
    }

    /// <summary>
    /// 没有登记内联查询处理器时不处理
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenNoHandlerRegistered_ReturnsFalse()
    {
        var router = CreateRouter();
        var recorder = new HandlerRecorder();
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder);
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateInlineQueryUpdate("查询"));

        Assert.False(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 已登记但未注册 DI 的处理器被跳过，不抛异常
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenHandlerMissingFromDi_ReturnsFalse()
    {
        var router = CreateRouter(typeof(TestInlineQueryHandler));
        var recorder = new HandlerRecorder();
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder);
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateInlineQueryUpdate("查询"));

        Assert.False(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Empty(recorder.Invocations);
    }

    /// <summary>
    /// CanHandle 返回 false 时不处理
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenHandlerCannotHandle_ReturnsFalse()
    {
        var router = CreateRouter(typeof(TestInlineQueryHandler));
        var recorder = new HandlerRecorder { InlineCanHandle = false };
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestInlineQueryHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateInlineQueryUpdate("查询"));

        Assert.False(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Empty(recorder.Invocations);
    }

    /// <summary>
    /// 查询文本裁剪首尾空白后交给处理器；处理器抛异常时吞掉并仍然返回「已处理」
    /// </summary>
    /// <remarks>
    /// 返回 true 很关键：内联查询没有 chat 上下文，交回分发器也没有别的链能处理它，
    /// 继续往下走只会白跑一遍管线。
    /// </remarks>
    [Fact]
    public async Task HandleAsync_TrimsQueryAndSwallowsHandlerException()
    {
        var router = CreateRouter(typeof(TestInlineQueryHandler));
        var recorder = new HandlerRecorder { ExceptionToThrow = new InvalidOperationException("内联处理器炸了") };
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestInlineQueryHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateInlineQueryUpdate("   订单 A-1   "));

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal(1, recorder.CountOf(TestInlineQueryHandler.HandlerName));
        Assert.Equal("订单 A-1", recorder.Invocations[0].Data);
    }

    /// <summary>
    /// 查询文本为 null 时按空串传给处理器，处理器不必自己判空
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenQueryNull_PassesEmptyString()
    {
        var router = CreateRouter(typeof(TestInlineQueryHandler));
        var recorder = new HandlerRecorder { ExceptionToThrow = new InvalidOperationException("内联处理器炸了") };
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestInlineQueryHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateInlineQueryUpdate(null));

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal(string.Empty, recorder.Invocations[0].Data);
    }

    /// <summary>
    /// 构造内联查询路由器
    /// </summary>
    /// <param name="handlerTypes">登记进目录的处理器类型</param>
    /// <returns>内联查询路由器</returns>
    private static TelegramInlineQueryRouter CreateRouter(params Type[] handlerTypes)
    {
        return new TelegramInlineQueryRouter(
            TelegramTestFactory.CreateCatalog(handlerTypes),
            NullLogger<TelegramInlineQueryRouter>.Instance);
    }
}
