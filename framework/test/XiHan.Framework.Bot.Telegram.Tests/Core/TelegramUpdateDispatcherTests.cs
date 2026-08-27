// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Telegram.Bot.Types.Enums;
using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Core;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Routing;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Core;

/// <summary>
/// <see cref="TelegramUpdateDispatcher"/> Update 分发管线测试
/// </summary>
/// <remarks>
/// 分发次序是这个库最核心的行为契约：
/// 群组/频道白名单守卫 → update_id 幂等 → 内联查询 → 会话状态机 → 回调 → /start 深链 →
/// 命令 → 回复 → 消息 → 兜底回复。次序错一步就会出现「未授权群里的消息被处理了」
/// 或「多步会话被普通消息处理器抢走」这类问题，所以每一段短路都单独立一条用例。
/// 全部依赖（发送门面、幂等器、状态存储、设置存储）均为手写替身，回调更新的 Id 一律留空，
/// 因此整组用例不会发出任何真实请求。
/// </remarks>
public class TelegramUpdateDispatcherTests
{
    /// <summary>
    /// 机器人实例为空时抛参数空异常
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenBotNull_Throws()
    {
        using var harness = CreateHarness([], []);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await harness.Dispatcher.DispatchAsync(null!, TelegramTestFactory.CreateMessageUpdate(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Update 为空时抛参数空异常
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenUpdateNull_Throws()
    {
        using var harness = CreateHarness([], []);
        using var bot = TelegramTestFactory.CreateBot();

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await harness.Dispatcher.DispatchAsync(bot, null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 群组白名单为空时拒收所有群消息（fail-closed），连幂等标记都不占
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenGroupNotWhitelisted_IgnoresUpdateBeforeDeduplication()
    {
        using var harness = CreateHarness([typeof(TestEarlyMessageHandler)], [typeof(TestEarlyMessageHandler)]);
        using var bot = TelegramTestFactory.CreateBot();
        var update = TelegramTestFactory.CreateMessageUpdate(text: "你好", chatId: -100123, chatType: ChatType.Supergroup);

        await harness.Dispatcher.DispatchAsync(bot, update, TestContext.Current.CancellationToken);

        Assert.Empty(harness.Deduplicator.Marked);
        Assert.Empty(harness.Recorder.Invocations);
        Assert.Empty(harness.Notifier.SentTexts);
    }

    /// <summary>
    /// 频道贴文与群组共用同一条白名单守卫
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenChannelNotWhitelisted_IgnoresUpdate()
    {
        using var harness = CreateHarness([typeof(TestEarlyMessageHandler)], [typeof(TestEarlyMessageHandler)]);
        using var bot = TelegramTestFactory.CreateBot();
        var update = TelegramTestFactory.CreateMessageUpdate(text: "公告", chatId: -100999, chatType: ChatType.Channel);

        await harness.Dispatcher.DispatchAsync(bot, update, TestContext.Current.CancellationToken);

        Assert.Empty(harness.Deduplicator.Marked);
        Assert.Empty(harness.Recorder.Invocations);
    }

    /// <summary>
    /// 群组在白名单内时正常进入管线
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenGroupWhitelisted_ProcessesUpdate()
    {
        using var harness = CreateHarness([typeof(TestEarlyMessageHandler)], [typeof(TestEarlyMessageHandler)]);
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(allowedGroupChatIds: [-100123L]));
        var update = TelegramTestFactory.CreateMessageUpdate(text: "你好", chatId: -100123, chatType: ChatType.Supergroup);

        await harness.Dispatcher.DispatchAsync(bot, update, TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Deduplicator.Marked.Count);
        Assert.Equal(1, harness.Recorder.CountOf(TestEarlyMessageHandler.HandlerName));
    }

    /// <summary>
    /// 永久放行命令在未授权群里也能穿过白名单守卫（否则用户在群里连 /start 都发不动）
    /// </summary>
    /// <param name="text">消息文本</param>
    [Theory]
    [InlineData("/start")]
    [InlineData("/help")]
    [InlineData("/myid")]
    [InlineData("/id@my_bot")]
    public async Task DispatchAsync_AlwaysAvailableCommandBypassesGroupGuard(string text)
    {
        using var harness = CreateHarness([], []);
        using var bot = TelegramTestFactory.CreateBot();
        var update = TelegramTestFactory.CreateMessageUpdate(text: text, chatId: -100123, chatType: ChatType.Supergroup);

        await harness.Dispatcher.DispatchAsync(bot, update, TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Deduplicator.Marked.Count);
    }

    /// <summary>
    /// 私聊不受群组白名单约束
    /// </summary>
    [Fact]
    public async Task DispatchAsync_PrivateChatIsNotAffectedByGroupWhitelist()
    {
        using var harness = CreateHarness([typeof(TestEarlyMessageHandler)], [typeof(TestEarlyMessageHandler)]);
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "你好"), TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Recorder.CountOf(TestEarlyMessageHandler.HandlerName));
    }

    /// <summary>
    /// 幂等标记按「机器人名 + UpdateId」占位，不使用 Token 作为键
    /// </summary>
    [Fact]
    public async Task DispatchAsync_MarksUpdateByBotNameAndUpdateId()
    {
        using var harness = CreateHarness([], []);
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(updateId: 4242), TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Deduplicator.Marked.Count);
        Assert.Equal(TelegramTestFactory.BotName, harness.Deduplicator.Marked[0].BotName);
        Assert.Equal(4242, harness.Deduplicator.Marked[0].UpdateId);
    }

    /// <summary>
    /// 命中幂等（重复投递）时整条管线短路
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenDuplicateUpdate_SkipsWholePipeline()
    {
        using var harness = CreateHarness([typeof(TestEarlyMessageHandler)], [typeof(TestEarlyMessageHandler)]);
        harness.Deduplicator.MarkResult = false;
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(enableFallbackReply: true));

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "你好"), TestContext.Current.CancellationToken);

        Assert.Empty(harness.Recorder.Invocations);
        Assert.Empty(harness.Notifier.SentTexts);
    }

    /// <summary>
    /// 内联查询在会话状态机之前被直接路由掉
    /// </summary>
    [Fact]
    public async Task DispatchAsync_RoutesInlineQueryBeforeOtherChains()
    {
        using var harness = CreateHarness(
            [typeof(TestInlineQueryHandler), typeof(TestEarlyMessageHandler)],
            [typeof(TestInlineQueryHandler), typeof(TestEarlyMessageHandler)]);
        // 内联处理器抛异常可以在不触达真实 AnswerInlineQuery 的前提下证明它确实被调用了
        harness.Recorder.ExceptionToThrow = new InvalidOperationException("内联处理器炸了");
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateInlineQueryUpdate("订单"), TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Recorder.CountOf(TestInlineQueryHandler.HandlerName));
        Assert.Equal(0, harness.Recorder.CountOf(TestEarlyMessageHandler.HandlerName));
        Assert.Equal(1, harness.Deduplicator.Marked.Count);
    }

    /// <summary>
    /// 存在活跃会话状态时，非命令非回调消息优先交给状态处理器
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenConversationStateActive_RoutesToStateHandler()
    {
        using var harness = CreateHarness(
            [typeof(TestStateHandler), typeof(TestEarlyMessageHandler)],
            [typeof(TestStateHandler), typeof(TestEarlyMessageHandler)]);
        harness.StateStore.State = new ConversationState { Step = "awaiting_amount", Payload = """{"orderId":"A-1"}""" };
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "100"), TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Recorder.CountOf(TestStateHandler.HandlerName));
        Assert.Equal("awaiting_amount", harness.Recorder.Invocations[0].Data);
        Assert.Equal("""{"orderId":"A-1"}""", harness.Recorder.Invocations[0].Args[0]);
        Assert.Equal(0, harness.Recorder.CountOf(TestEarlyMessageHandler.HandlerName));
    }

    /// <summary>
    /// 状态步骤为空白时视为无活跃状态，消息继续走后面的链
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenStateStepBlank_FallsThroughToMessageChain()
    {
        using var harness = CreateHarness(
            [typeof(TestStateHandler), typeof(TestEarlyMessageHandler)],
            [typeof(TestStateHandler), typeof(TestEarlyMessageHandler)]);
        harness.StateStore.State = new ConversationState { Step = "   " };
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "100"), TestContext.Current.CancellationToken);

        Assert.Equal(0, harness.Recorder.CountOf(TestStateHandler.HandlerName));
        Assert.Equal(1, harness.Recorder.CountOf(TestEarlyMessageHandler.HandlerName));
    }

    /// <summary>
    /// 没有任何状态处理器命中该步骤时消息继续走后面的链，且不清除状态
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenNoStateHandlerMatches_KeepsStateAndFallsThrough()
    {
        using var harness = CreateHarness(
            [typeof(TestStateHandler), typeof(TestEarlyMessageHandler)],
            [typeof(TestStateHandler), typeof(TestEarlyMessageHandler)]);
        harness.Recorder.StateCanHandle = false;
        harness.StateStore.State = new ConversationState { Step = "awaiting_amount" };
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "100"), TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Recorder.CountOf(TestEarlyMessageHandler.HandlerName));
        Assert.Equal(0, harness.StateStore.RemoveCount);
        Assert.NotNull(harness.StateStore.State);
    }

    /// <summary>
    /// 命令消息不会被会话状态机劫持
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenCommandDuringActiveState_SkipsStateMachine()
    {
        using var harness = CreateHarness(
            [typeof(TestStateHandler), typeof(TestOrderCommandHandler)],
            [typeof(TestStateHandler), typeof(TestOrderCommandHandler)]);
        harness.StateStore.State = new ConversationState { Step = "awaiting_amount" };
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "/order A-1"), TestContext.Current.CancellationToken);

        Assert.Equal(0, harness.Recorder.CountOf(TestStateHandler.HandlerName));
        Assert.Equal(1, harness.Recorder.CountOf(TestOrderCommandHandler.HandlerName));
    }

    /// <summary>
    /// 按钮回调被路由到回调处理器，且不进入命令 / 消息链
    /// </summary>
    [Fact]
    public async Task DispatchAsync_RoutesCallbackToCallbackHandler()
    {
        using var harness = CreateHarness(
            [typeof(TestConfirmCallbackHandler), typeof(TestEarlyMessageHandler)],
            [typeof(TestConfirmCallbackHandler), typeof(TestEarlyMessageHandler)]);
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateCallbackUpdate("confirm:A-1"), TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Recorder.CountOf(TestConfirmCallbackHandler.HandlerName));
        Assert.Equal("confirm:A-1", harness.Recorder.Invocations[0].Data);
        Assert.Equal(0, harness.Recorder.CountOf(TestEarlyMessageHandler.HandlerName));
    }

    /// <summary>
    /// /start 携带深链参数时先交给深链处理器，消费后不再进入命令链
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenStartCarriesPayload_RoutesToStartPayloadHandler()
    {
        using var harness = CreateHarness(
            [typeof(TestStartPayloadHandler), typeof(TestEarlyMessageHandler)],
            [typeof(TestStartPayloadHandler), typeof(TestEarlyMessageHandler)]);
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "/start invite-A1"), TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Recorder.CountOf(TestStartPayloadHandler.HandlerName));
        Assert.Equal("invite-A1", harness.Recorder.Invocations[0].Data);
        Assert.Equal(0, harness.Recorder.CountOf(TestEarlyMessageHandler.HandlerName));
    }

    /// <summary>
    /// 带 @机器人用户名 的 /start 深链同样被识别
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenStartWithBotSuffixCarriesPayload_StillRoutes()
    {
        using var harness = CreateHarness([typeof(TestStartPayloadHandler)], [typeof(TestStartPayloadHandler)]);
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "/start@my_bot invite-A1"), TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Recorder.CountOf(TestStartPayloadHandler.HandlerName));
    }

    /// <summary>
    /// 无参数的 /start 不触发深链处理器
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenStartHasNoPayload_SkipsStartPayloadChain()
    {
        using var harness = CreateHarness([typeof(TestStartPayloadHandler)], [typeof(TestStartPayloadHandler)]);
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "/start"), TestContext.Current.CancellationToken);

        Assert.Empty(harness.Recorder.Invocations);
    }

    /// <summary>
    /// 其它命令携带参数不会被误当作 /start 深链
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenOtherCommandCarriesArgs_SkipsStartPayloadChain()
    {
        using var harness = CreateHarness(
            [typeof(TestStartPayloadHandler), typeof(TestOrderCommandHandler)],
            [typeof(TestStartPayloadHandler), typeof(TestOrderCommandHandler)]);
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "/order invite-A1"), TestContext.Current.CancellationToken);

        Assert.Equal(0, harness.Recorder.CountOf(TestStartPayloadHandler.HandlerName));
        Assert.Equal(1, harness.Recorder.CountOf(TestOrderCommandHandler.HandlerName));
    }

    /// <summary>
    /// 深链处理器声明未消费时消息继续往命令链走
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenStartPayloadNotConsumed_FallsThroughToCommandChain()
    {
        using var harness = CreateHarness(
            [typeof(TestStartPayloadHandler), typeof(TestEarlyMessageHandler)],
            [typeof(TestStartPayloadHandler), typeof(TestEarlyMessageHandler)]);
        harness.Recorder.StartPayloadHandled = false;
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(enableFallbackReply: true));

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "/start invite-A1"), TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Recorder.CountOf(TestStartPayloadHandler.HandlerName));
        Assert.Equal(1, harness.Recorder.CountOf(TestEarlyMessageHandler.HandlerName));
    }

    /// <summary>
    /// 深链处理器抛异常时被吞掉记日志，不触发全局异常兜底文案
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenStartPayloadHandlerThrows_DoesNotBreakPipeline()
    {
        using var harness = CreateHarness([typeof(TestStartPayloadHandler)], [typeof(TestStartPayloadHandler)]);
        harness.Recorder.ExceptionToThrow = new InvalidOperationException("深链处理器炸了");
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "/start invite-A1"), TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Recorder.CountOf(TestStartPayloadHandler.HandlerName));
        Assert.Empty(harness.Notifier.SentTexts);
    }

    /// <summary>
    /// 命令消息被路由到命令处理器
    /// </summary>
    [Fact]
    public async Task DispatchAsync_RoutesCommandToCommandHandler()
    {
        using var harness = CreateHarness([typeof(TestOrderCommandHandler)], [typeof(TestOrderCommandHandler)]);
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "/order A-1"), TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Recorder.CountOf(TestOrderCommandHandler.HandlerName));
        Assert.Equal(new[] { "A-1" }, harness.Recorder.Invocations[0].Args);
    }

    /// <summary>
    /// 回复消息优先交给回复链，不落到普通消息链
    /// </summary>
    [Fact]
    public async Task DispatchAsync_RoutesReplyToReplyHandlerBeforeMessageChain()
    {
        using var harness = CreateHarness(
            [typeof(TestReplyHandler), typeof(TestEarlyMessageHandler)],
            [typeof(TestReplyHandler), typeof(TestEarlyMessageHandler)]);
        using var bot = TelegramTestFactory.CreateBot();

        var message = TelegramTestFactory.CreateMessage(text: "回复内容");
        message.ReplyToMessage = TelegramTestFactory.CreateMessage(text: "被回复的消息", messageId: 5);
        var update = new global::Telegram.Bot.Types.Update { Id = 9, Message = message };

        await harness.Dispatcher.DispatchAsync(bot, update, TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Recorder.CountOf(TestReplyHandler.HandlerName));
        Assert.Equal(0, harness.Recorder.CountOf(TestEarlyMessageHandler.HandlerName));
    }

    /// <summary>
    /// 回复链不命中时消息继续落到普通消息链
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenReplyChainMisses_FallsThroughToMessageChain()
    {
        using var harness = CreateHarness(
            [typeof(TestReplyHandler), typeof(TestEarlyMessageHandler)],
            [typeof(TestReplyHandler), typeof(TestEarlyMessageHandler)]);
        harness.Recorder.ReplyCanHandle = false;
        using var bot = TelegramTestFactory.CreateBot();

        var message = TelegramTestFactory.CreateMessage(text: "回复内容");
        message.ReplyToMessage = TelegramTestFactory.CreateMessage(text: "被回复的消息", messageId: 5);
        var update = new global::Telegram.Bot.Types.Update { Id = 9, Message = message };

        await harness.Dispatcher.DispatchAsync(bot, update, TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Recorder.CountOf(TestEarlyMessageHandler.HandlerName));
    }

    /// <summary>
    /// 兜底回复关闭时无人处理的消息不产生任何回复
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenFallbackDisabled_SendsNothing()
    {
        using var harness = CreateHarness([], []);
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "无人处理"), TestContext.Current.CancellationToken);

        Assert.Empty(harness.Notifier.SentTexts);
    }

    /// <summary>
    /// 单机器人配置开启兜底回复时发送兜底文案
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenBotConfigEnablesFallback_SendsUnhandledReply()
    {
        using var harness = CreateHarness([], []);
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(enableFallbackReply: true));

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "无人处理"), TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Notifier.SentTexts.Count);
        Assert.Equal(new TelegramBotTexts().UnhandledMessageReply, harness.Notifier.SentTexts[0].Text);
        Assert.Equal(100L, harness.Notifier.SentTexts[0].ChatId);
        Assert.Equal(11, harness.Notifier.SentTexts[0].ReplyToMessageId);
    }

    /// <summary>
    /// 平台全局设置开启兜底回复时同样生效（与单机器人配置任一开启即生效）
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenGlobalSettingsEnableFallback_SendsUnhandledReply()
    {
        using var harness = CreateHarness([], []);
        harness.SettingsStore.Settings = new TelegramBotSettings { EnableFallbackReply = true };
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "无人处理"), TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Notifier.SentTexts.Count);
        Assert.Equal(new TelegramBotTexts().UnhandledMessageReply, harness.Notifier.SentTexts[0].Text);
    }

    /// <summary>
    /// 设置存储不可用时退回单机器人配置的兜底开关，不影响主流程
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenSettingsStoreThrows_FallsBackToBotConfigFlag()
    {
        using var harness = CreateHarness([], []);
        harness.SettingsStore.ExceptionToThrow = new InvalidOperationException("设置存储不可用");
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(enableFallbackReply: true));

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "无人处理"), TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Notifier.SentTexts.Count);
        Assert.Equal(new TelegramBotTexts().UnhandledMessageReply, harness.Notifier.SentTexts[0].Text);
    }

    /// <summary>
    /// 未注册设置存储时按单机器人配置处理
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenSettingsStoreNotRegistered_UsesBotConfigFlag()
    {
        using var harness = CreateHarness([], [], registerSettingsStore: false);
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(enableFallbackReply: true));

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "无人处理"), TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Notifier.SentTexts.Count);
    }

    /// <summary>
    /// 兜底文案被配置为空白时不发送（应用层可用空串关掉这条回复）
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenFallbackTextBlank_SendsNothing()
    {
        using var harness = CreateHarness([], [], configureOptions: x => x.Texts.UnhandledMessageReply = "   ");
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(enableFallbackReply: true));

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "无人处理"), TestContext.Current.CancellationToken);

        Assert.Empty(harness.Notifier.SentTexts);
    }

    /// <summary>
    /// 兜底回复发送失败只记日志，不抛给调用方
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenFallbackReplyFails_SwallowsException()
    {
        using var harness = CreateHarness([], []);
        harness.Notifier.ExceptionToThrow = new InvalidOperationException("发送失败");
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(enableFallbackReply: true));

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "无人处理"), TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 处理器抛异常时向用户发送统一的异常兜底文案，异常不外泄
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenHandlerThrows_SendsInternalErrorReply()
    {
        using var harness = CreateHarness([typeof(TestOrderCommandHandler)], [typeof(TestOrderCommandHandler)]);
        harness.Recorder.ExceptionToThrow = new InvalidOperationException("处理器炸了");
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "/order A-1"), TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Notifier.SentTexts.Count);
        Assert.Equal(new TelegramBotTexts().InternalErrorReply, harness.Notifier.SentTexts[0].Text);
    }

    /// <summary>
    /// 异常兜底文案为空白时不发送
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenInternalErrorTextBlank_SendsNothing()
    {
        using var harness = CreateHarness(
            [typeof(TestOrderCommandHandler)],
            [typeof(TestOrderCommandHandler)],
            configureOptions: x => x.Texts.InternalErrorReply = string.Empty);
        harness.Recorder.ExceptionToThrow = new InvalidOperationException("处理器炸了");
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "/order A-1"), TestContext.Current.CancellationToken);

        Assert.Empty(harness.Notifier.SentTexts);
    }

    /// <summary>
    /// 处理被取消时回滚幂等标记，且回滚不带已取消的令牌
    /// </summary>
    /// <remarks>
    /// 回滚必须用 CancellationToken.None：沿用已取消的原令牌会让回滚本身被取消，
    /// 结果是这条 Update 既没处理完又永远被幂等挡住，at-least-once 语义就破了。
    /// </remarks>
    [Fact]
    public async Task DispatchAsync_WhenCanceled_UnmarksWithNoneToken()
    {
        using var harness = CreateHarness([typeof(TestOrderCommandHandler)], [typeof(TestOrderCommandHandler)]);
        using var cts = new CancellationTokenSource();
        harness.Recorder.ExceptionToThrow = new OperationCanceledException(cts.Token);
        using var bot = TelegramTestFactory.CreateBot();
        await cts.CancelAsync();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(text: "/order A-1", updateId: 77), cts.Token);

        Assert.Equal(1, harness.Deduplicator.Unmarked.Count);
        Assert.Equal(TelegramTestFactory.BotName, harness.Deduplicator.Unmarked[0].BotName);
        Assert.Equal(77, harness.Deduplicator.Unmarked[0].UpdateId);
        Assert.False(harness.Deduplicator.LastUnmarkCancellationToken.CanBeCanceled);
    }

    /// <summary>
    /// 未占到幂等标记（重复投递）时不会误回滚
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenDuplicateUpdate_DoesNotUnmark()
    {
        using var harness = CreateHarness([], []);
        harness.Deduplicator.MarkResult = false;
        using var bot = TelegramTestFactory.CreateBot();

        await harness.Dispatcher.DispatchAsync(bot, TelegramTestFactory.CreateMessageUpdate(), TestContext.Current.CancellationToken);

        Assert.Empty(harness.Deduplicator.Unmarked);
    }

    /// <summary>
    /// 构造分发器测试装置
    /// </summary>
    /// <param name="catalogHandlers">登记进处理器目录的类型</param>
    /// <param name="diHandlers">注册进 DI 的类型</param>
    /// <param name="configureOptions">平台选项配置委托</param>
    /// <param name="registerSettingsStore">是否注册平台设置存储</param>
    /// <returns>测试装置</returns>
    private static DispatcherHarness CreateHarness(
        Type[] catalogHandlers,
        Type[] diHandlers,
        Action<TelegramBotPlatformOptions>? configureOptions = null,
        bool registerSettingsStore = true)
    {
        var recorder = new HandlerRecorder();
        var notifier = new FakeTelegramNotifier();
        var deduplicator = new FakeTelegramUpdateDeduplicator();
        var stateStore = new FakeConversationStateStore();
        var settingsStore = new FakeTelegramBotSettingsStore();

        var services = new ServiceCollection();
        _ = services.AddSingleton(recorder);
        if (registerSettingsStore)
        {
            _ = services.AddSingleton<ITelegramBotSettingsStore>(settingsStore);
        }

        foreach (var handlerType in diHandlers)
        {
            _ = services.AddTransient(handlerType);
        }

        var provider = services.BuildServiceProvider();
        var catalog = TelegramTestFactory.CreateCatalog(catalogHandlers);
        var options = TelegramTestFactory.CreatePlatformOptions(configureOptions);

        var dispatcher = new TelegramUpdateDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            catalog,
            new TelegramCommandRouter(catalog, notifier, options, NullLogger<TelegramCommandRouter>.Instance),
            new TelegramCallbackRouter(catalog, notifier, options, NullLogger<TelegramCallbackRouter>.Instance),
            new TelegramReplyRouter(catalog, NullLogger<TelegramReplyRouter>.Instance),
            new TelegramMessageRouter(catalog, NullLogger<TelegramMessageRouter>.Instance),
            new TelegramInlineQueryRouter(catalog, NullLogger<TelegramInlineQueryRouter>.Instance),
            deduplicator,
            stateStore,
            notifier,
            options,
            NullLogger<TelegramUpdateDispatcher>.Instance);

        return new DispatcherHarness(dispatcher, recorder, notifier, deduplicator, stateStore, settingsStore, provider);
    }

    /// <summary>
    /// 分发器测试装置：持有分发器与全部手写替身
    /// </summary>
    private sealed class DispatcherHarness : IDisposable
    {
        private readonly ServiceProvider _provider;

        public DispatcherHarness(
            TelegramUpdateDispatcher dispatcher,
            HandlerRecorder recorder,
            FakeTelegramNotifier notifier,
            FakeTelegramUpdateDeduplicator deduplicator,
            FakeConversationStateStore stateStore,
            FakeTelegramBotSettingsStore settingsStore,
            ServiceProvider provider)
        {
            Dispatcher = dispatcher;
            Recorder = recorder;
            Notifier = notifier;
            Deduplicator = deduplicator;
            StateStore = stateStore;
            SettingsStore = settingsStore;
            _provider = provider;
        }

        public TelegramUpdateDispatcher Dispatcher { get; }

        public HandlerRecorder Recorder { get; }

        public FakeTelegramNotifier Notifier { get; }

        public FakeTelegramUpdateDeduplicator Deduplicator { get; }

        public FakeConversationStateStore StateStore { get; }

        public FakeTelegramBotSettingsStore SettingsStore { get; }

        public void Dispose()
        {
            _provider.Dispose();
        }
    }
}
