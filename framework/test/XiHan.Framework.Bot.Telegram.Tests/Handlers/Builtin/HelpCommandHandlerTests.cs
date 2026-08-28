// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Bot.Telegram.Handlers;
using XiHan.Framework.Bot.Telegram.Handlers.Builtin;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Routing;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Handlers.Builtin;

/// <summary>
/// <see cref="HelpCommandHandler"/> 内置 /help 命令处理器测试
/// </summary>
/// <remarks>
/// /help 的可见性必须与命令菜单同一套过滤逻辑：按机器人的命令白名单过滤、非管理员看不到 AdminOnly 命令。
/// 这里最值得守的是「非管理员不得从 /help 里看到管理员命令」——
/// 泄漏出去等于把内部运维命令的存在直接告诉所有群成员。
/// </remarks>
public class HelpCommandHandlerTests
{
    /// <summary>
    /// 标注了 /help 命令与 /h 别名，且未限定管理员
    /// </summary>
    [Fact]
    public void Attribute_BindsHelpCommandWithShortAlias()
    {
        var attribute = Assert.Single(
            typeof(HelpCommandHandler).GetCustomAttributes(typeof(BotCommandAttribute), inherit: false)
                .Cast<BotCommandAttribute>());

        Assert.Equal("/help", attribute.Command);
        Assert.False(attribute.AdminOnly);
        Assert.Equal(new[] { "/h" }, attribute.GetNormalizedAliases());
    }

    /// <summary>
    /// 上下文为空时抛参数空异常
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenContextNull_Throws()
    {
        var handler = CreateHandler(out _, out _, typeof(TestOrderCommandHandler));

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await handler.HandleAsync(null!, [], TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 普通用户看到的帮助文本按「头部 + 命令 - 描述」逐行输出，不含管理员命令
    /// </summary>
    [Fact]
    public async Task HandleAsync_ForNormalUser_ListsOnlyPublicCommands()
    {
        var handler = CreateHandler(out var notifier, out _, typeof(TestOrderCommandHandler), typeof(TestAdminCommandHandler));
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(adminUsers: [999L]));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/help", userId: 200));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);

        Assert.Single(notifier.SentTexts);
        Assert.Equal(
            "可用命令：" + Environment.NewLine + "/order - 下单",
            notifier.SentTexts[0].Text);
        Assert.DoesNotContain("/ban", notifier.SentTexts[0].Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 管理员看到的帮助文本包含仅管理员命令
    /// </summary>
    [Fact]
    public async Task HandleAsync_ForAdmin_IncludesAdminOnlyCommands()
    {
        var handler = CreateHandler(out var notifier, out _, typeof(TestOrderCommandHandler), typeof(TestAdminCommandHandler));
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(adminUsers: [200L]));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/help", userId: 200));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);

        Assert.Equal(
            "可用命令：" + Environment.NewLine + "/order - 下单" + Environment.NewLine + "/ban - 封禁用户",
            notifier.SentTexts[0].Text);
    }

    /// <summary>
    /// 没有描述的命令只输出命令本身，不带多余的分隔符
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenCommandHasNoDescription_PrintsCommandOnly()
    {
        var handler = CreateHandler(out var notifier, out _, typeof(TestNoDescriptionCommandHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/help"));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);

        Assert.Equal("可用命令：" + Environment.NewLine + "/ping", notifier.SentTexts[0].Text);
    }

    /// <summary>
    /// 命令白名单同样约束帮助文本
    /// </summary>
    [Fact]
    public async Task HandleAsync_HonorsCommandWhitelist()
    {
        var handler = CreateHandler(out var notifier, out _, typeof(TestOrderCommandHandler), typeof(TestNoDescriptionCommandHandler));
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(allowedCommands: ["/ping"]));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/help"));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);

        Assert.Equal("可用命令：" + Environment.NewLine + "/ping", notifier.SentTexts[0].Text);
    }

    /// <summary>
    /// 帮助头部可被应用层整体覆盖
    /// </summary>
    [Fact]
    public async Task HandleAsync_UsesConfiguredHelpHeader()
    {
        var handler = CreateHandler(out var notifier, out var options, typeof(TestOrderCommandHandler));
        options.CurrentValue.Texts.HelpHeader = "支持的指令如下：";
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/help"));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);

        Assert.Equal("支持的指令如下：" + Environment.NewLine + "/order - 下单", notifier.SentTexts[0].Text);
    }

    /// <summary>
    /// 头部为空白时只输出命令列表
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenHeaderBlank_PrintsCommandsOnly()
    {
        var handler = CreateHandler(out var notifier, out var options, typeof(TestOrderCommandHandler));
        options.CurrentValue.Texts.HelpHeader = "   ";
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/help"));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);

        Assert.Equal("/order - 下单", notifier.SentTexts[0].Text);
    }

    /// <summary>
    /// 既没有头部也没有可见命令时不发送空白消息
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenNothingToShow_SendsNothing()
    {
        var handler = CreateHandler(out var notifier, out var options);
        options.CurrentValue.Texts.HelpHeader = string.Empty;
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/help"));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);

        Assert.Empty(notifier.SentTexts);
    }

    /// <summary>
    /// 定位不到会话时不发送
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenChatIdZero_SendsNothing()
    {
        var handler = CreateHandler(out var notifier, out _, typeof(TestOrderCommandHandler));
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateEmptyUpdate());

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);

        Assert.Empty(notifier.SentTexts);
    }

    /// <summary>
    /// 发送失败只记日志，不把异常抛回分发器
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenSendFails_SwallowsException()
    {
        var handler = CreateHandler(out var notifier, out _, typeof(TestOrderCommandHandler));
        notifier.ExceptionToThrow = new InvalidOperationException("发送失败");
        using var bot = TelegramTestFactory.CreateBot();
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/help"));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 回复按 Reply 形式发给触发消息
    /// </summary>
    [Fact]
    public async Task HandleAsync_RepliesToTriggerMessage()
    {
        var handler = CreateHandler(out var notifier, out _, typeof(TestOrderCommandHandler));
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(name: "main-bot"));
        var context = TelegramTestFactory.CreateContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/help", messageId: 77));

        await handler.HandleAsync(context, [], TestContext.Current.CancellationToken);

        Assert.Equal("main-bot", notifier.SentTexts[0].BotName);
        Assert.Equal(100L, notifier.SentTexts[0].ChatId);
        Assert.Equal(77, notifier.SentTexts[0].ReplyToMessageId);
    }

    /// <summary>
    /// 构造内置 /help 处理器
    /// </summary>
    /// <param name="notifier">发送门面替身</param>
    /// <param name="options">平台选项监视器</param>
    /// <param name="handlerTypes">登记进目录的处理器类型</param>
    /// <returns>处理器</returns>
    private static HelpCommandHandler CreateHandler(
        out FakeTelegramNotifier notifier,
        out TestOptionsMonitor<TelegramBotPlatformOptions> options,
        params Type[] handlerTypes)
    {
        notifier = new FakeTelegramNotifier();
        options = TelegramTestFactory.CreatePlatformOptions();
        TelegramBotHandlerCatalog catalog = TelegramTestFactory.CreateCatalog(handlerTypes);
        return new HelpCommandHandler(catalog, notifier, options, NullLogger<HelpCommandHandler>.Instance);
    }
}
