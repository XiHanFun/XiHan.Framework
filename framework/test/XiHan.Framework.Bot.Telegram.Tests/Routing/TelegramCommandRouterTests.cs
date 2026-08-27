// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Bot.Telegram.Handlers.Builtin;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Routing;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Routing;

/// <summary>
/// <see cref="TelegramCommandRouter"/> 命令路由器测试
/// </summary>
/// <remarks>
/// 路由器承担三件事：把文本切成命令 + 参数、执行命令白名单与 AdminOnly 两道守卫、把请求交给处理器。
/// 关键契约是「守卫命中时也要返回 true」——返回 false 会让分发器继续往下走消息路由，
/// 结果是被拒绝的命令又被当成普通消息处理了一遍。
/// 发送全部走手写替身，全程零网络。
/// </remarks>
public class TelegramCommandRouterTests
{
    /// <summary>
    /// 上下文为空时抛参数空异常
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenContextNull_Throws()
    {
        var router = CreateRouter(out _, out _, typeof(TestOrderCommandHandler));
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
        var router = CreateRouter(out _, out _, typeof(TestOrderCommandHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/order"));

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await router.HandleAsync(context, null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 命令未登记时不处理，交回分发器继续走后续管线
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenCommandNotRegistered_ReturnsFalse()
    {
        var router = CreateRouter(out var recorder, out var notifier, typeof(TestOrderCommandHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestOrderCommandHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/unknown"));

        var handled = await router.HandleAsync(context, provider, TestContext.Current.CancellationToken);

        Assert.False(handled);
        Assert.Empty(recorder.Invocations);
        Assert.Empty(notifier.SentTexts);
    }

    /// <summary>
    /// 普通文本既不命中命令也不命中正则时不处理
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenPlainTextWithoutPattern_ReturnsFalse()
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestOrderCommandHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestOrderCommandHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "随便说点什么"));

        Assert.False(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 命中命令时把参数原样交给处理器
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenCommandMatched_InvokesHandlerWithArgs()
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestOrderCommandHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestOrderCommandHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/order A-1 2"));

        var handled = await router.HandleAsync(context, provider, TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(1, recorder.CountOf(TestOrderCommandHandler.HandlerName));
        Assert.Equal(new[] { "A-1", "2" }, recorder.Invocations[0].Args);
    }

    /// <summary>
    /// 群里带 @机器人用户名 的命令同样命中，@ 后缀不参与查表
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenCommandCarriesBotSuffix_StillMatches()
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestOrderCommandHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestOrderCommandHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/order@my_bot A-1"));

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal(new[] { "A-1" }, recorder.Invocations[0].Args);
    }

    /// <summary>
    /// 命令查表忽略大小写，并支持别名
    /// </summary>
    /// <param name="text">消息文本</param>
    [Theory]
    [InlineData("/order")]
    [InlineData("/ORDER")]
    [InlineData("/Order")]
    [InlineData("/o")]
    [InlineData("/O")]
    public async Task HandleAsync_CommandLookupIgnoresCaseAndSupportsAliases(string text)
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestOrderCommandHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestOrderCommandHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: text));

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal(1, recorder.CountOf(TestOrderCommandHandler.HandlerName));
    }

    /// <summary>
    /// 命令白名单不含该命令时回复「命令未开启」并吃掉本次消息
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenCommandNotAllowed_RepliesDisabledAndStopsPipeline()
    {
        var router = CreateRouter(out var recorder, out var notifier, typeof(TestOrderCommandHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestOrderCommandHandler));
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(allowedCommands: ["/other"]));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/order"));

        var handled = await router.HandleAsync(context, provider, TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Empty(recorder.Invocations);
        Assert.Equal(1, notifier.SentTexts.Count);
        Assert.Equal(new TelegramBotTexts().CommandDisabledReply, notifier.SentTexts[0].Text);
        Assert.Equal(100L, notifier.SentTexts[0].ChatId);
        Assert.Equal(11, notifier.SentTexts[0].ReplyToMessageId);
    }

    /// <summary>
    /// 命令白名单命中别名时同样放行
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenAllowedCommandsMatchAlias_Passes()
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestOrderCommandHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestOrderCommandHandler));
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(allowedCommands: ["/o"]));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/order"));

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal(1, recorder.CountOf(TestOrderCommandHandler.HandlerName));
    }

    /// <summary>
    /// 命令白名单为空表示不限制命令
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenAllowedCommandsEmpty_DoesNotRestrict()
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestOrderCommandHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestOrderCommandHandler));
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(allowedCommands: []));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/order"));

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal(1, recorder.CountOf(TestOrderCommandHandler.HandlerName));
    }

    /// <summary>
    /// 永久放行命令仍然受命令白名单约束（只豁免群组白名单守卫）
    /// </summary>
    [Fact]
    public async Task HandleAsync_AlwaysAvailableCommandStillHonorsCommandWhitelist()
    {
        var router = CreateRouter(out var recorder, out var notifier, typeof(StartCommandHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder);
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(allowedCommands: ["/order"]));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/start"));

        var handled = await router.HandleAsync(context, provider, TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(1, notifier.SentTexts.Count);
        Assert.Equal(new TelegramBotTexts().CommandDisabledReply, notifier.SentTexts[0].Text);
    }

    /// <summary>
    /// 非管理员执行管理员命令时回复提示并吃掉本次消息
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenAdminOnlyCommandFromNormalUser_RepliesAdminOnly()
    {
        var router = CreateRouter(out var recorder, out var notifier, typeof(TestAdminCommandHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestAdminCommandHandler));
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(adminUsers: [999L]));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/ban 5", userId: 200));

        var handled = await router.HandleAsync(context, provider, TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Empty(recorder.Invocations);
        Assert.Equal(1, notifier.SentTexts.Count);
        Assert.Equal(new TelegramBotTexts().AdminOnlyCommandReply, notifier.SentTexts[0].Text);
    }

    /// <summary>
    /// 管理员执行管理员命令时正常进入处理器
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenAdminOnlyCommandFromAdmin_InvokesHandler()
    {
        var router = CreateRouter(out var recorder, out var notifier, typeof(TestAdminCommandHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestAdminCommandHandler));
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(adminUsers: [200L]));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/ban 5", userId: 200));

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal(1, recorder.CountOf(TestAdminCommandHandler.HandlerName));
        Assert.Empty(notifier.SentTexts);
    }

    /// <summary>
    /// 命令已登记但处理器没注册 DI 时降级为「未处理」，不抛异常打断分发
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenHandlerNotRegisteredInDi_ReturnsFalse()
    {
        var router = CreateRouter(out var recorder, out var notifier, typeof(TestOrderCommandHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder);
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/order"));

        Assert.False(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Empty(recorder.Invocations);
        Assert.Empty(notifier.SentTexts);
    }

    /// <summary>
    /// 正则直达：非命令文本命中正则后按捕获组作为参数进入处理器
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenTextMatchesPattern_RoutesWithCaptureGroups()
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestPatternCommandHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestPatternCommandHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "查单 12345"));

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal(1, recorder.CountOf(TestPatternCommandHandler.HandlerName));
        Assert.Equal(new[] { "12345" }, recorder.Invocations[0].Args);
    }

    /// <summary>
    /// 正则没有捕获组时，把命中的整段文本作为唯一参数
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenPatternHasNoCaptureGroup_PassesMatchedText()
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestNoGroupPatternCommandHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestNoGroupPatternCommandHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "重复这句话"));

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Equal(new[] { "重复这句话" }, recorder.Invocations[0].Args);
    }

    /// <summary>
    /// 正则不命中时不处理
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenTextDoesNotMatchPattern_ReturnsFalse()
    {
        var router = CreateRouter(out var recorder, out _, typeof(TestPatternCommandHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestPatternCommandHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "查单 abc"));

        Assert.False(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Empty(recorder.Invocations);
    }

    /// <summary>
    /// 正则直达同样执行命令白名单守卫
    /// </summary>
    [Fact]
    public async Task HandleAsync_PatternRouteAlsoHonorsCommandWhitelist()
    {
        var router = CreateRouter(out var recorder, out var notifier, typeof(TestPatternCommandHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestPatternCommandHandler));
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(allowedCommands: ["/order"]));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "查单 12345"));

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Empty(recorder.Invocations);
        Assert.Equal(new TelegramBotTexts().CommandDisabledReply, notifier.SentTexts[0].Text);
    }

    /// <summary>
    /// 守卫回复文案为空时不发送任何消息（应用层可用空串关掉这条回复）
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenGuardReplyTextBlank_SendsNothing()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestOrderCommandHandler));
        var notifier = new FakeTelegramNotifier();
        var options = TelegramTestFactory.CreatePlatformOptions(x => x.Texts.CommandDisabledReply = "   ");
        var router = new TelegramCommandRouter(catalog, notifier, options, NullLogger<TelegramCommandRouter>.Instance);

        var recorder = new HandlerRecorder();
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestOrderCommandHandler));
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(allowedCommands: ["/other"]));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/order"));

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
        Assert.Empty(notifier.SentTexts);
    }

    /// <summary>
    /// 守卫回复发送失败只记日志，不把异常抛给分发器
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenGuardReplyFails_SwallowsException()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestOrderCommandHandler));
        var notifier = new FakeTelegramNotifier { ExceptionToThrow = new InvalidOperationException("发送失败") };
        var options = TelegramTestFactory.CreatePlatformOptions();
        var router = new TelegramCommandRouter(catalog, notifier, options, NullLogger<TelegramCommandRouter>.Instance);

        var recorder = new HandlerRecorder();
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestOrderCommandHandler));
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(allowedCommands: ["/other"]));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/order"));

        Assert.True(await router.HandleAsync(context, provider, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 取消令牌原样透传给发送门面
    /// </summary>
    [Fact]
    public async Task HandleAsync_PassesCancellationTokenToNotifier()
    {
        var router = CreateRouter(out var recorder, out var notifier, typeof(TestOrderCommandHandler));
        using var provider = TelegramTestFactory.CreateHandlerProvider(recorder, typeof(TestOrderCommandHandler));
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(allowedCommands: ["/other"]));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/order"));
        using var cts = new CancellationTokenSource();

        await router.HandleAsync(context, provider, cts.Token);

        Assert.Equal(cts.Token, notifier.LastCancellationToken);
    }

    /// <summary>
    /// 构造命令路由器
    /// </summary>
    /// <param name="recorder">共享记录器</param>
    /// <param name="notifier">发送门面替身</param>
    /// <param name="handlerTypes">登记进目录的处理器类型</param>
    /// <returns>命令路由器</returns>
    private static TelegramCommandRouter CreateRouter(
        out HandlerRecorder recorder,
        out FakeTelegramNotifier notifier,
        params Type[] handlerTypes)
    {
        recorder = new HandlerRecorder();
        notifier = new FakeTelegramNotifier();
        return new TelegramCommandRouter(
            TelegramTestFactory.CreateCatalog(handlerTypes),
            notifier,
            TelegramTestFactory.CreatePlatformOptions(),
            NullLogger<TelegramCommandRouter>.Instance);
    }
}
