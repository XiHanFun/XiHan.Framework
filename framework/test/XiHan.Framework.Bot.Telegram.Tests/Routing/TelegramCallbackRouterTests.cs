// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Routing;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Routing;

/// <summary>
/// <see cref="TelegramCallbackRouter"/> 按钮回调路由器测试
/// </summary>
/// <remarks>
/// 用例中的 CallbackQuery.Id 一律留空：路由器只在回调 Id 非空时才调用 AnswerCallbackQuery，
/// 留空即可覆盖完整的路由与守卫逻辑而不触发任何真实 Bot API 请求。
/// 回调 Action 的切法（第一个冒号之前）与路由表大小写不敏感是这里的核心契约。
/// </remarks>
public class TelegramCallbackRouterTests
{
    /// <summary>
    /// 上下文为空时抛参数空异常
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenContextNull_Throws()
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestConfirmCallbackHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await router.HandleAsync(null!, provider, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 服务提供者为空时抛参数空异常
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenProviderNull_Throws()
    {
        var router = CreateRouter(out _, out _, typeof(TestConfirmCallbackHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateCallbackUpdate("confirm:1"));

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await router.HandleAsync(context, null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 回调数据为空时不处理
    /// </summary>
    /// <param name="data">回调数据</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_WhenCallbackDataBlank_ReturnsFalse(string? data)
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestConfirmCallbackHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestConfirmCallbackHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateCallbackUpdate(data));

        Assert.False(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Empty(recorder.Invocations);
    }

    /// <summary>
    /// 非回调更新不处理
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenNotCallbackUpdate_ReturnsFalse()
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestConfirmCallbackHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestConfirmCallbackHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "confirm:1"));

        Assert.False(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 回调动作未登记时不处理
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenActionNotRegistered_ReturnsFalse()
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestConfirmCallbackHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestConfirmCallbackHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateCallbackUpdate("unknown:1"));

        Assert.False(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Empty(recorder.Invocations);
    }

    /// <summary>
    /// 命中回调动作时把完整回调数据交给处理器（含 id 部分）
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenActionMatched_PassesFullCallbackData()
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestConfirmCallbackHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestConfirmCallbackHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateCallbackUpdate("confirm:A-1"));

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal(1, recorder.CountOf(TestConfirmCallbackHandler.HandlerName));
        Assert.Equal("confirm:A-1", recorder.Invocations[0].Data);
    }

    /// <summary>
    /// 回调动作查表忽略大小写
    /// </summary>
    [Fact]
    public async Task HandleAsync_ActionLookupIgnoresCase()
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestConfirmCallbackHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestConfirmCallbackHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateCallbackUpdate("CONFIRM:A-1"));

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal(1, recorder.CountOf(TestConfirmCallbackHandler.HandlerName));
    }

    /// <summary>
    /// 不带分隔符的回调数据整串即动作名
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenCallbackDataHasNoSeparator_UsesWholeStringAsAction()
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestConfirmCallbackHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestConfirmCallbackHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateCallbackUpdate("confirm"));

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal("confirm", recorder.Invocations[0].Data);
    }

    /// <summary>
    /// 非管理员点击管理员按钮时回复提示且不进入处理器
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenAdminOnlyCallbackFromNormalUser_RepliesAdminOnly()
    {
        var router = CreateRouter(out var recorder, out var notifier, typeof(TestAdminCallbackHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestAdminCallbackHandler));
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(adminUsers: [999L]));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateCallbackUpdate("purge:1", userId: 200));

        var handled = await router.HandleAsync(context, provider, TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Empty(recorder.Invocations);
        Assert.Equal(1, notifier.SentTexts.Count);
        Assert.Equal(new TelegramBotTexts().AdminOnlyCallbackReply, notifier.SentTexts[0].Text);
    }

    /// <summary>
    /// 管理员点击管理员按钮时正常进入处理器
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenAdminOnlyCallbackFromAdmin_InvokesHandler()
    {
        var router = CreateRouter(out var recorder, out var notifier, typeof(TestAdminCallbackHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestAdminCallbackHandler));
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(adminUsers: [200L]));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateCallbackUpdate("purge:1", userId: 200));

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal(1, recorder.CountOf(TestAdminCallbackHandler.HandlerName));
        Assert.Empty(notifier.SentTexts);
    }

    /// <summary>
    /// 回调已登记但处理器没注册 DI 时降级为「未处理」，不抛异常
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenHandlerNotRegisteredInDi_ReturnsFalse()
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestConfirmCallbackHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder);
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateCallbackUpdate("confirm:1"));

        Assert.False(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Empty(recorder.Invocations);
    }

    /// <summary>
    /// 处理器抛出的异常原样向上冒泡（由分发器统一兜底），不会被应答逻辑吞掉
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenHandlerThrows_PropagatesException()
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestConfirmCallbackHandler));
        recorder.ExceptionToThrow = new InvalidOperationException("处理器炸了");
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestConfirmCallbackHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateCallbackUpdate("confirm:1"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));

        Assert.Equal("处理器炸了", exception.Message);
        Assert.Equal(1, recorder.CountOf(TestConfirmCallbackHandler.HandlerName));
    }

    /// <summary>
    /// 处理器自行设置的应答文本被保留在上下文里，交由路由器统一应答
    /// </summary>
    [Fact]
    public async Task HandleAsync_KeepsCallbackAnswerSetByHandler()
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestConfirmCallbackHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestConfirmCallbackHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateCallbackUpdate("confirm:1"));
        context.SetCallbackAnswer("已确认", showAlert: true);

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal("已确认", context.CallbackAnswerText);
        Assert.True(context.CallbackAnswerShowAlert);
    }

    /// <summary>
    /// 管理员守卫回复文案为空时不发送任何消息
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenAdminGuardReplyBlank_SendsNothing()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestAdminCallbackHandler));
        var notifier = new FakeTelegramNotifier();
        var options = TelegramTestFactory.CreatePlatformOptions(x => x.Texts.AdminOnlyCallbackReply = string.Empty);
        var router = new TelegramCallbackRouter(catalog, notifier, options, NullLogger<TelegramCallbackRouter>.Instance);

        var recorder = new HandlerRecorder();
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestAdminCallbackHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateCallbackUpdate("purge:1"));

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Empty(notifier.SentTexts);
        Assert.Empty(recorder.Invocations);
    }

    /// <summary>
    /// 构造回调路由器
    /// </summary>
    /// <param name="recorder">共享记录器</param>
    /// <param name="notifier">发送门面替身</param>
    /// <param name="handlerTypes">登记进目录的处理器类型</param>
    /// <returns>回调路由器</returns>
    private static TelegramCallbackRouter CreateRouter(
        out HandlerRecorder recorder,
        out FakeTelegramNotifier notifier,
        params Type[] handlerTypes)
    {
        recorder = new HandlerRecorder();
        notifier = new FakeTelegramNotifier();
        return new TelegramCallbackRouter(
            TelegramTestFactory.CreateCatalog(handlerTypes),
            notifier,
            TelegramTestFactory.CreatePlatformOptions(),
            NullLogger<TelegramCallbackRouter>.Instance);
    }
}
