// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Handlers.Builtin;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Routing;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Routing;

/// <summary>
/// <see cref="TelegramBotHandlerCatalog"/> 处理器目录测试
/// </summary>
/// <remarks>
/// 目录在构造阶段就把「注册非法」全部炸出来（缺属性、命令/动作重复、没实现任何处理器接口），
/// 这是刻意的快速失败：宁可应用起不来，也不要机器人跑起来之后才发现某个命令永远进不到处理器。
/// 因此这里既要验证路由表建对，也要验证每一种非法注册都确实抛异常。
/// </remarks>
public class TelegramBotHandlerCatalogTests
{
    /// <summary>
    /// 选项为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void Constructor_WhenOptionsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TelegramBotHandlerCatalog(null!));
    }

    /// <summary>
    /// 未登记任何处理器时全部路由表为空
    /// </summary>
    [Fact]
    public void Constructor_WithNoHandlers_BuildsEmptyCatalog()
    {
        var catalog = TelegramTestFactory.CreateCatalog();

        Assert.Empty(catalog.CommandRoutes);
        Assert.Empty(catalog.CommandPatternRoutes);
        Assert.Empty(catalog.CallbackRoutes);
        Assert.Empty(catalog.MessageHandlerTypes);
        Assert.Empty(catalog.ReplyHandlerTypes);
        Assert.Empty(catalog.StateHandlerTypes);
        Assert.Empty(catalog.InlineQueryHandlerTypes);
        Assert.Empty(catalog.StartPayloadHandlerTypes);
        Assert.Empty(catalog.GetPublicCommands());
        Assert.Empty(catalog.GetVisibleCommands());
    }

    /// <summary>
    /// 登记未实现任何处理器接口的类型时构造失败
    /// </summary>
    [Fact]
    public void Constructor_WhenTypeImplementsNoHandlerInterface_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => TelegramTestFactory.CreateCatalog(typeof(TestNotAHandler)));

        Assert.Contains("未实现任何 IBot*Handler 接口", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 命令处理器缺少 [BotCommand] 时构造失败
    /// </summary>
    [Fact]
    public void Constructor_WhenCommandHandlerMissesAttribute_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => TelegramTestFactory.CreateCatalog(typeof(TestCommandHandlerWithoutAttribute)));

        Assert.Contains("缺少 [BotCommand] 属性", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 回调处理器缺少 [BotCallback] 时构造失败
    /// </summary>
    [Fact]
    public void Constructor_WhenCallbackHandlerMissesAttribute_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => TelegramTestFactory.CreateCatalog(typeof(TestCallbackHandlerWithoutAttribute)));

        Assert.Contains("缺少 [BotCallback] 属性", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 两个处理器绑定同一个命令时构造失败
    /// </summary>
    [Fact]
    public void Constructor_WhenCommandDuplicated_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => TelegramTestFactory.CreateCatalog(typeof(TestOrderCommandHandler), typeof(TestDuplicateOrderCommandHandler)));

        Assert.Contains("命令重复", exception.Message, StringComparison.Ordinal);
        Assert.Contains("/order", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 两个处理器绑定同一个回调动作时构造失败
    /// </summary>
    [Fact]
    public void Constructor_WhenCallbackActionDuplicated_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => TelegramTestFactory.CreateCatalog(typeof(TestConfirmCallbackHandler), typeof(TestDuplicateConfirmCallbackHandler)));

        Assert.Contains("回调动作重复", exception.Message, StringComparison.Ordinal);
        Assert.Contains("confirm", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 同一处理器类型被重复登记时按一次处理，不会误报命令重复
    /// </summary>
    /// <remarks>
    /// 注册扩展本身已做过一次去重，这里保证目录侧也扛得住重复登记，
    /// 否则应用层一句重复的 AddTelegramBotHandler 就会让整个应用起不来。
    /// </remarks>
    [Fact]
    public void Constructor_WhenSameHandlerRegisteredTwice_DeduplicatesInsteadOfThrowing()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestOrderCommandHandler), typeof(TestOrderCommandHandler));

        Assert.Equal(2, catalog.CommandRoutes.Count);
        Assert.Single(catalog.GetVisibleCommands());
    }

    /// <summary>
    /// 主命令与别名都会写进命令路由表，且查表忽略大小写
    /// </summary>
    [Fact]
    public void CommandRoutes_ContainMainCommandAndAliasesIgnoringCase()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestOrderCommandHandler));

        Assert.Equal(2, catalog.CommandRoutes.Count);
        Assert.True(catalog.CommandRoutes.ContainsKey("/order"));
        Assert.True(catalog.CommandRoutes.ContainsKey("/o"));
        Assert.True(catalog.CommandRoutes.ContainsKey("/ORDER"));
        Assert.Same(catalog.CommandRoutes["/order"], catalog.CommandRoutes["/o"]);
    }

    /// <summary>
    /// 路由项携带处理器类型、管理员标记与归一化命令集合
    /// </summary>
    [Fact]
    public void CommandRoutes_CarryHandlerTypeAdminFlagAndNormalizedCommands()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestOrderCommandHandler), typeof(TestAdminCommandHandler));

        var orderRoute = catalog.CommandRoutes["/order"];
        Assert.Equal(typeof(TestOrderCommandHandler), orderRoute.HandlerType);
        Assert.False(orderRoute.AdminOnly);
        Assert.Equal(2, orderRoute.NormalizedCommands.Length);
        Assert.Contains("/order", orderRoute.NormalizedCommands);
        Assert.Contains("/o", orderRoute.NormalizedCommands);

        var banRoute = catalog.CommandRoutes["/ban"];
        Assert.Equal(typeof(TestAdminCommandHandler), banRoute.HandlerType);
        Assert.True(banRoute.AdminOnly);
    }

    /// <summary>
    /// 内置 /start 命令的路由被标记为永久放行，业务命令则不是
    /// </summary>
    [Fact]
    public void CommandRoutes_MarkBuiltinCommandsAsAlwaysAvailable()
    {
        var catalog = TelegramTestFactory.CreateCatalog(
            typeof(StartCommandHandler),
            typeof(HelpCommandHandler),
            typeof(MyIdCommandHandler),
            typeof(TestOrderCommandHandler));

        Assert.True(catalog.CommandRoutes["/start"].IsAlwaysAvailable);
        Assert.True(catalog.CommandRoutes["/help"].IsAlwaysAvailable);
        Assert.True(catalog.CommandRoutes["/h"].IsAlwaysAvailable);
        Assert.True(catalog.CommandRoutes["/myid"].IsAlwaysAvailable);
        Assert.True(catalog.CommandRoutes["/id"].IsAlwaysAvailable);
        Assert.False(catalog.CommandRoutes["/order"].IsAlwaysAvailable);
    }

    /// <summary>
    /// 一个类上标注多个 [BotCommand] 时逐条建立路由与描述符
    /// </summary>
    [Fact]
    public void CommandRoutes_SupportMultipleAttributesOnOneHandler()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestMultiCommandHandler));

        Assert.Equal(2, catalog.CommandRoutes.Count);
        Assert.Equal(typeof(TestMultiCommandHandler), catalog.CommandRoutes["/first"].HandlerType);
        Assert.Equal(typeof(TestMultiCommandHandler), catalog.CommandRoutes["/second"].HandlerType);
        Assert.Equal(2, catalog.GetVisibleCommands().Count);
    }

    /// <summary>
    /// 只有配置了 Pattern 的命令才会进入正则路由列表
    /// </summary>
    [Fact]
    public void CommandPatternRoutes_OnlyIncludeHandlersWithPattern()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestOrderCommandHandler), typeof(TestPatternCommandHandler));

        Assert.Single(catalog.CommandPatternRoutes);
        Assert.Equal(typeof(TestPatternCommandHandler), catalog.CommandPatternRoutes[0].Route.HandlerType);
        Assert.Matches(catalog.CommandPatternRoutes[0].Regex, "查单 12345");
    }

    /// <summary>
    /// 回调路由表按动作建立，查表忽略大小写且携带管理员标记
    /// </summary>
    [Fact]
    public void CallbackRoutes_AreKeyedByActionIgnoringCase()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestConfirmCallbackHandler), typeof(TestAdminCallbackHandler));

        Assert.Equal(2, catalog.CallbackRoutes.Count);
        Assert.Equal(typeof(TestConfirmCallbackHandler), catalog.CallbackRoutes["confirm"].HandlerType);
        Assert.Equal(typeof(TestConfirmCallbackHandler), catalog.CallbackRoutes["CONFIRM"].HandlerType);
        Assert.False(catalog.CallbackRoutes["confirm"].AdminOnly);
        Assert.True(catalog.CallbackRoutes["purge"].AdminOnly);
    }

    /// <summary>
    /// 消息 / 回复 / 状态 / 内联 / 深链五条链按接口分别登记
    /// </summary>
    [Fact]
    public void HandlerTypeLists_AreGroupedByInterface()
    {
        var catalog = TelegramTestFactory.CreateCatalog(
            typeof(TestEarlyMessageHandler),
            typeof(TestReplyHandler),
            typeof(TestStateHandler),
            typeof(TestInlineQueryHandler),
            typeof(TestStartPayloadHandler));

        Assert.Equal(new[] { typeof(TestEarlyMessageHandler) }, catalog.MessageHandlerTypes);
        Assert.Equal(new[] { typeof(TestReplyHandler) }, catalog.ReplyHandlerTypes);
        Assert.Equal(new[] { typeof(TestStateHandler) }, catalog.StateHandlerTypes);
        Assert.Equal(new[] { typeof(TestInlineQueryHandler) }, catalog.InlineQueryHandlerTypes);
        Assert.Equal(new[] { typeof(TestStartPayloadHandler) }, catalog.StartPayloadHandlerTypes);
    }

    /// <summary>
    /// 一个类型实现多个处理器接口时会同时登记到多条链
    /// </summary>
    [Fact]
    public void HandlerTypeLists_AllowOneTypeOnMultipleChains()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestMessageAndReplyHandler));

        Assert.Contains(typeof(TestMessageAndReplyHandler), catalog.MessageHandlerTypes);
        Assert.Contains(typeof(TestMessageAndReplyHandler), catalog.ReplyHandlerTypes);
    }

    /// <summary>
    /// 命令菜单排除仅管理员命令
    /// </summary>
    [Fact]
    public void GetPublicCommands_ExcludesAdminOnlyCommands()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestOrderCommandHandler), typeof(TestAdminCommandHandler));

        var commands = catalog.GetPublicCommands();

        Assert.Single(commands);
        Assert.Equal("order", commands[0].Command);
    }

    /// <summary>
    /// 命令菜单里的命令名去掉前导斜杠（Telegram 菜单不接受带斜杠的命令名）
    /// </summary>
    /// <remarks>
    /// 同一个类上的多个 [BotCommand] 由反射读出，顺序不作为契约，因此这里只断言集合内容。
    /// </remarks>
    [Fact]
    public void GetPublicCommands_StripsLeadingSlashFromCommandName()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestMultiCommandHandler));

        var commands = catalog.GetPublicCommands();

        Assert.Equal(2, commands.Count);
        Assert.Contains(commands, x => x.Command == "first" && x.Description == "第一个命令");
        Assert.Contains(commands, x => x.Command == "second" && x.Description == "第二个命令");
        Assert.DoesNotContain(commands, x => x.Command.StartsWith('/'));
    }

    /// <summary>
    /// 描述不足 3 个字符时补上命令名，满足 Telegram 对描述长度的硬性要求
    /// </summary>
    [Fact]
    public void GetPublicCommands_PadsTooShortDescriptionWithCommandName()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestShortDescriptionCommandHandler));

        var commands = catalog.GetPublicCommands();

        Assert.Single(commands);
        Assert.Equal("ok", commands[0].Command);
        Assert.Equal("好 / ok", commands[0].Description);
    }

    /// <summary>
    /// 没有描述时回落到命令名本身
    /// </summary>
    [Fact]
    public void GetPublicCommands_WhenDescriptionEmpty_UsesCommandName()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestNoDescriptionCommandHandler));

        var commands = catalog.GetPublicCommands();

        Assert.Single(commands);
        Assert.Equal("ping", commands[0].Command);
        Assert.Equal("ping", commands[0].Description);
    }

    /// <summary>
    /// 群组菜单优先用别名作描述（无描述时）
    /// </summary>
    [Fact]
    public void GetPublicCommands_WhenPreferAliasDescription_UsesFirstAlias()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestNoDescriptionCommandHandler));

        var commands = catalog.GetPublicCommands(preferAliasDescription: true);

        Assert.Single(commands);
        Assert.Equal("pong", commands[0].Description);
    }

    /// <summary>
    /// 已配置描述时不会被别名顶掉
    /// </summary>
    [Fact]
    public void GetPublicCommands_WhenDescriptionPresent_AliasDoesNotOverrideIt()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestAdminCommandHandler), typeof(TestOrderCommandHandler));

        var commands = catalog.GetPublicCommands(preferAliasDescription: true);

        Assert.Single(commands);
        Assert.Equal("order", commands[0].Command);
        Assert.Equal("下单 / order", commands[0].Description);
    }

    /// <summary>
    /// 命令白名单同样约束菜单展示：主命令或别名任一命中即保留
    /// </summary>
    [Fact]
    public void GetPublicCommands_FiltersByAllowedCommands()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestOrderCommandHandler), typeof(TestNoDescriptionCommandHandler));

        Assert.Single(catalog.GetPublicCommands(["/order"]));
        Assert.Equal("order", catalog.GetPublicCommands(["/order"])[0].Command);

        // 命中别名同样保留主命令
        Assert.Equal("ping", catalog.GetPublicCommands(["/pong"])[0].Command);

        // 白名单项允许省略斜杠、忽略大小写
        Assert.Equal("order", catalog.GetPublicCommands(["ORDER"])[0].Command);
    }

    /// <summary>
    /// 白名单里没有的命令不会出现在菜单中
    /// </summary>
    [Fact]
    public void GetPublicCommands_WhenCommandNotAllowed_IsHidden()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestOrderCommandHandler));

        Assert.Empty(catalog.GetPublicCommands(["/other"]));
    }

    /// <summary>
    /// 白名单为 null 或全为空白项时视为不限制
    /// </summary>
    [Fact]
    public void GetPublicCommands_WhenAllowedCommandsNullOrAllBlank_DoesNotFilter()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestOrderCommandHandler));

        Assert.Single(catalog.GetPublicCommands(null));
        Assert.Single(catalog.GetPublicCommands([]));
        Assert.Single(catalog.GetPublicCommands([string.Empty, "   "]));
    }

    /// <summary>
    /// /help 文本默认看不到仅管理员命令
    /// </summary>
    [Fact]
    public void GetVisibleCommands_ExcludesAdminOnlyByDefault()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestOrderCommandHandler), typeof(TestAdminCommandHandler));

        var descriptors = catalog.GetVisibleCommands();

        Assert.Single(descriptors);
        Assert.Equal("/order", descriptors[0].Command);
        Assert.Equal("下单", descriptors[0].Description);
        Assert.False(descriptors[0].AdminOnly);
        Assert.Equal(new[] { "/o" }, descriptors[0].Aliases);
    }

    /// <summary>
    /// 管理员身份可以看到仅管理员命令
    /// </summary>
    [Fact]
    public void GetVisibleCommands_WhenIncludeAdminOnly_ReturnsAdminCommandsToo()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestOrderCommandHandler), typeof(TestAdminCommandHandler));

        var descriptors = catalog.GetVisibleCommands(includeAdminOnly: true);

        Assert.Equal(2, descriptors.Count);
        Assert.Equal("/order", descriptors[0].Command);
        Assert.Equal("/ban", descriptors[1].Command);
        Assert.True(descriptors[1].AdminOnly);
    }

    /// <summary>
    /// /help 可见列表同样受命令白名单约束
    /// </summary>
    [Fact]
    public void GetVisibleCommands_FiltersByAllowedCommands()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestOrderCommandHandler), typeof(TestNoDescriptionCommandHandler));

        var descriptors = catalog.GetVisibleCommands(["/ping"]);

        Assert.Single(descriptors);
        Assert.Equal("/ping", descriptors[0].Command);
    }

    /// <summary>
    /// 可见列表按登记顺序返回，保证 /help 文本顺序稳定
    /// </summary>
    [Fact]
    public void GetVisibleCommands_KeepsRegistrationOrder()
    {
        var catalog = TelegramTestFactory.CreateCatalog(
            typeof(TestNoDescriptionCommandHandler),
            typeof(TestOrderCommandHandler),
            typeof(TestShortDescriptionCommandHandler));

        var commands = catalog.GetVisibleCommands().Select(x => x.Command).ToArray();

        Assert.Equal(new[] { "/ping", "/order", "/ok" }, commands);
    }

    /// <summary>
    /// 描述符携带的归一化命令集合与命令路由项保持一致
    /// </summary>
    [Fact]
    public void GetVisibleCommands_DescriptorCarriesNormalizedCommands()
    {
        var catalog = TelegramTestFactory.CreateCatalog(typeof(TestOrderCommandHandler));

        var descriptor = catalog.GetVisibleCommands()[0];

        Assert.Equal(2, descriptor.NormalizedCommands.Length);
        Assert.Contains("/order", descriptor.NormalizedCommands);
        Assert.Contains("/o", descriptor.NormalizedCommands);
    }

    /// <summary>
    /// 通过 IOptions 注入的登记列表被逐条消费
    /// </summary>
    [Fact]
    public void Constructor_ConsumesHandlerListFromOptions()
    {
        var options = new TelegramBotHandlerOptions();
        options.Handlers.Add(typeof(TestOrderCommandHandler));
        options.Handlers.Add(typeof(TestConfirmCallbackHandler));
        options.Handlers.Add(typeof(TestEarlyMessageHandler));

        var catalog = new TelegramBotHandlerCatalog(Microsoft.Extensions.Options.Options.Create(options));

        Assert.Equal(2, catalog.CommandRoutes.Count);
        Assert.Single(catalog.CallbackRoutes);
        Assert.Single(catalog.MessageHandlerTypes);
    }
}
