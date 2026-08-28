// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using XiHan.Framework.Bot.Telegram.Core;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Core;

/// <summary>
/// <see cref="TelegramBotContext"/> 更新上下文测试
/// </summary>
/// <remarks>
/// 上下文是整条分发管线的取数入口：ChatId / UserId / Text / IsCommand / IsGroup 只要有一个取错，
/// 权限守卫和路由就会整体走偏。这里逐条锁死取值优先级（编辑消息、频道贴文、回调消息的归并顺序）
/// 与命令/回调的切分规则。上下文本身不发任何请求，全程零网络。
/// </remarks>
public class TelegramBotContextTests
{
    /// <summary>
    /// 机器人实例为空时抛参数空异常
    /// </summary>
    [Fact]
    public void Constructor_WhenBotNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TelegramBotContext(null!, new Update()));
    }

    /// <summary>
    /// Update 为空时抛参数空异常
    /// </summary>
    [Fact]
    public void Constructor_WhenUpdateNull_Throws()
    {
        using var bot = TelegramTestFactory.CreateBot();

        Assert.Throws<ArgumentNullException>(() => new TelegramBotContext(bot, null!));
    }

    /// <summary>
    /// 构造后原样暴露机器人实例、Update 与客户端
    /// </summary>
    [Fact]
    public void Constructor_ExposesBotUpdateAndClient()
    {
        using var bot = TelegramTestFactory.CreateBot();
        var update = TelegramTestFactory.CreateMessageUpdate();

        var context = new TelegramBotContext(bot, update);

        Assert.Same(bot, context.Bot);
        Assert.Same(update, context.Update);
        Assert.Same(bot.Client, context.Client);
    }

    /// <summary>
    /// 普通消息、编辑消息、频道贴文、编辑频道贴文都会被归并到 Message
    /// </summary>
    [Fact]
    public void Message_MergesEditedMessageAndChannelPosts()
    {
        using var bot = TelegramTestFactory.CreateBot();

        var edited = TelegramTestFactory.CreateMessage(text: "edited");
        Assert.Same(edited, new TelegramBotContext(bot, new Update { EditedMessage = edited }).Message);

        var channelPost = TelegramTestFactory.CreateMessage(text: "post", chatType: ChatType.Channel);
        Assert.Same(channelPost, new TelegramBotContext(bot, new Update { ChannelPost = channelPost }).Message);

        var editedChannelPost = TelegramTestFactory.CreateMessage(text: "edited-post", chatType: ChatType.Channel);
        Assert.Same(editedChannelPost, new TelegramBotContext(bot, new Update { EditedChannelPost = editedChannelPost }).Message);
    }

    /// <summary>
    /// 同时存在多种消息时按 Message &gt; EditedMessage &gt; ChannelPost &gt; EditedChannelPost 取第一个
    /// </summary>
    [Fact]
    public void Message_PrefersPlainMessageOverEditedMessage()
    {
        using var bot = TelegramTestFactory.CreateBot();
        var plain = TelegramTestFactory.CreateMessage(text: "plain");
        var edited = TelegramTestFactory.CreateMessage(text: "edited");

        var context = new TelegramBotContext(bot, new Update { Message = plain, EditedMessage = edited });

        Assert.Same(plain, context.Message);
        Assert.Equal("plain", context.Text);
    }

    /// <summary>
    /// 回调消息不并入 Message，而是走 Callback
    /// </summary>
    [Fact]
    public void Callback_IsExposedSeparatelyFromMessage()
    {
        using var bot = TelegramTestFactory.CreateBot();
        var update = TelegramTestFactory.CreateCallbackUpdate("confirm:1");

        var context = new TelegramBotContext(bot, update);

        Assert.Null(context.Message);
        Assert.NotNull(context.Callback);
        Assert.True(context.IsCallback);
    }

    /// <summary>
    /// 会话 Id 优先取消息所在会话，其次取回调消息所在会话，都没有时为 0
    /// </summary>
    [Fact]
    public void ChatId_FallsBackFromMessageToCallbackMessageToZero()
    {
        using var bot = TelegramTestFactory.CreateBot();

        Assert.Equal(100L, new TelegramBotContext(bot, TelegramTestFactory.CreateMessageUpdate(chatId: 100)).ChatId);
        Assert.Equal(555L, new TelegramBotContext(bot, TelegramTestFactory.CreateCallbackUpdate("a:1", chatId: 555)).ChatId);
        Assert.Equal(0L, new TelegramBotContext(bot, new Update()).ChatId);
    }

    /// <summary>
    /// 用户 Id 优先取消息发送者，其次取回调发起者，都没有时为 0
    /// </summary>
    [Fact]
    public void UserId_FallsBackFromMessageSenderToCallbackSenderToZero()
    {
        using var bot = TelegramTestFactory.CreateBot();

        Assert.Equal(200L, new TelegramBotContext(bot, TelegramTestFactory.CreateMessageUpdate(userId: 200)).UserId);
        Assert.Equal(321L, new TelegramBotContext(bot, TelegramTestFactory.CreateCallbackUpdate("a:1", userId: 321)).UserId);
        Assert.Equal(0L, new TelegramBotContext(bot, new Update()).UserId);
    }

    /// <summary>
    /// 触发消息 Id 供 Reply 使用，无消息时为 null
    /// </summary>
    [Fact]
    public void TriggerMessageId_ComesFromMessageOrCallbackMessage()
    {
        using var bot = TelegramTestFactory.CreateBot();

        Assert.Equal(11, new TelegramBotContext(bot, TelegramTestFactory.CreateMessageUpdate(messageId: 11)).TriggerMessageId);
        Assert.Equal(11, new TelegramBotContext(bot, TelegramTestFactory.CreateCallbackUpdate("a:1")).TriggerMessageId);
        Assert.Null(new TelegramBotContext(bot, new Update()).TriggerMessageId);
    }

    /// <summary>
    /// 文本优先取覆盖值，其次取消息文本，最后回落到图片说明
    /// </summary>
    [Fact]
    public void Text_PrefersOverrideThenTextThenCaption()
    {
        using var bot = TelegramTestFactory.CreateBot();

        var captionOnly = TelegramTestFactory.CreateMessage(text: null);
        captionOnly.Caption = "图片说明";
        Assert.Equal("图片说明", new TelegramBotContext(bot, new Update { Message = captionOnly }).Text);

        var withText = TelegramTestFactory.CreateMessage(text: "正文");
        withText.Caption = "图片说明";
        Assert.Equal("正文", new TelegramBotContext(bot, new Update { Message = withText }).Text);

        var context = new TelegramBotContext(bot, new Update { Message = withText })
        {
            TextOverride = "覆盖值"
        };
        Assert.Equal("覆盖值", context.Text);
    }

    /// <summary>
    /// 无消息且无覆盖值时文本为 null
    /// </summary>
    [Fact]
    public void Text_WhenNoMessage_IsNull()
    {
        using var bot = TelegramTestFactory.CreateBot();

        Assert.Null(new TelegramBotContext(bot, new Update()).Text);
    }

    /// <summary>
    /// 以斜杠开头的文本判定为命令，允许有前导空白
    /// </summary>
    /// <param name="text">消息文本</param>
    /// <param name="expected">是否命令</param>
    [Theory]
    [InlineData("/start", true)]
    [InlineData("   /start", true)]
    [InlineData("/order 123", true)]
    [InlineData("start", false)]
    [InlineData("你好", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsCommand_RequiresLeadingSlash(string? text, bool expected)
    {
        using var bot = TelegramTestFactory.CreateBot();
        var context = new TelegramBotContext(bot, TelegramTestFactory.CreateMessageUpdate(text: text));

        Assert.Equal(expected, context.IsCommand);
    }

    /// <summary>
    /// 回调更新永远不判定为命令，即使覆盖文本以斜杠开头
    /// </summary>
    [Fact]
    public void IsCommand_WhenCallback_IsAlwaysFalse()
    {
        using var bot = TelegramTestFactory.CreateBot();
        var context = new TelegramBotContext(bot, TelegramTestFactory.CreateCallbackUpdate("confirm:1"))
        {
            TextOverride = "/order"
        };

        Assert.True(context.IsCallback);
        Assert.False(context.IsCommand);
    }

    /// <summary>
    /// ReplyToMessage 非空时判定为回复消息
    /// </summary>
    [Fact]
    public void IsReply_RequiresReplyToMessage()
    {
        using var bot = TelegramTestFactory.CreateBot();

        var plain = TelegramTestFactory.CreateMessage(text: "普通消息");
        Assert.False(new TelegramBotContext(bot, new Update { Message = plain }).IsReply);

        var reply = TelegramTestFactory.CreateMessage(text: "回复内容");
        reply.ReplyToMessage = TelegramTestFactory.CreateMessage(text: "被回复的消息", messageId: 5);
        Assert.True(new TelegramBotContext(bot, new Update { Message = reply }).IsReply);
    }

    /// <summary>
    /// 群与超级群判定为群聊，私聊与频道不算
    /// </summary>
    /// <param name="chatType">会话类型</param>
    /// <param name="expected">是否群聊</param>
    [Theory]
    [InlineData(ChatType.Group, true)]
    [InlineData(ChatType.Supergroup, true)]
    [InlineData(ChatType.Private, false)]
    [InlineData(ChatType.Channel, false)]
    public void IsGroup_CoversGroupAndSupergroup(ChatType chatType, bool expected)
    {
        using var bot = TelegramTestFactory.CreateBot();
        var context = new TelegramBotContext(bot, TelegramTestFactory.CreateMessageUpdate(chatType: chatType));

        Assert.Equal(expected, context.IsGroup);
    }

    /// <summary>
    /// 频道贴文与频道消息上的回调都判定为频道
    /// </summary>
    [Fact]
    public void IsChannel_CoversChannelPostAndChannelCallback()
    {
        using var bot = TelegramTestFactory.CreateBot();

        var post = new TelegramBotContext(bot, TelegramTestFactory.CreateMessageUpdate(chatType: ChatType.Channel));
        Assert.True(post.IsChannel);
        Assert.False(post.IsGroup);

        var callback = new TelegramBotContext(bot, TelegramTestFactory.CreateCallbackUpdate("a:1", chatType: ChatType.Channel));
        Assert.True(callback.IsChannel);
    }

    /// <summary>
    /// 群聊里的按钮回调同样判定为群聊，守卫才不会被回调绕过
    /// </summary>
    [Fact]
    public void IsGroup_CoversCallbackFromGroupChat()
    {
        using var bot = TelegramTestFactory.CreateBot();
        var context = new TelegramBotContext(bot, TelegramTestFactory.CreateCallbackUpdate("a:1", chatType: ChatType.Supergroup));

        Assert.True(context.IsGroup);
    }

    /// <summary>
    /// 管理员判定直接委托给机器人配置
    /// </summary>
    [Fact]
    public void IsAdmin_DelegatesToBotConfig()
    {
        using var bot = TelegramTestFactory.CreateBot(TelegramTestFactory.CreateConfig(adminUsers: [200L]));

        Assert.True(new TelegramBotContext(bot, TelegramTestFactory.CreateMessageUpdate(userId: 200)).IsAdmin);
        Assert.False(new TelegramBotContext(bot, TelegramTestFactory.CreateMessageUpdate(userId: 201)).IsAdmin);
    }

    /// <summary>
    /// 命令 token 保留 @bot 后缀，参数按空白切分并剔除空项
    /// </summary>
    [Fact]
    public void GetCommandTokenAndArgs_SplitByWhitespace()
    {
        using var bot = TelegramTestFactory.CreateBot();
        var context = new TelegramBotContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "  /order@my_bot   A-1    2  "));

        Assert.Equal("/order@my_bot", context.GetCommandToken());
        Assert.Equal(new[] { "A-1", "2" }, context.GetCommandArgs());
    }

    /// <summary>
    /// 无参数命令返回空参数数组
    /// </summary>
    [Fact]
    public void GetCommandArgs_WhenNoArgs_ReturnsEmpty()
    {
        using var bot = TelegramTestFactory.CreateBot();
        var context = new TelegramBotContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "/start"));

        Assert.Equal("/start", context.GetCommandToken());
        Assert.Empty(context.GetCommandArgs());
    }

    /// <summary>
    /// 非命令消息取不到命令 token，参数为空
    /// </summary>
    [Fact]
    public void GetCommandToken_WhenNotCommand_ReturnsNull()
    {
        using var bot = TelegramTestFactory.CreateBot();
        var context = new TelegramBotContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "你好"));

        Assert.Null(context.GetCommandToken());
        Assert.Empty(context.GetCommandArgs());
    }

    /// <summary>
    /// 回调 Action 取第一个冒号之前的部分并去空白；无冒号时整串即 Action
    /// </summary>
    /// <param name="data">回调数据</param>
    /// <param name="expected">回调动作</param>
    [Theory]
    [InlineData("confirm:123", "confirm")]
    [InlineData("confirm:123:456", "confirm")]
    [InlineData("refresh", "refresh")]
    [InlineData(" confirm :123", "confirm")]
    [InlineData(":123", "")]
    public void GetCallbackAction_TakesPrefixBeforeFirstSeparator(string data, string expected)
    {
        using var bot = TelegramTestFactory.CreateBot();
        var context = new TelegramBotContext(bot, TelegramTestFactory.CreateCallbackUpdate(data));

        Assert.Equal(expected, context.GetCallbackAction());
    }

    /// <summary>
    /// 无回调数据时取不到 Action
    /// </summary>
    /// <param name="data">回调数据</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetCallbackAction_WhenDataBlank_ReturnsNull(string? data)
    {
        using var bot = TelegramTestFactory.CreateBot();
        var context = new TelegramBotContext(bot, TelegramTestFactory.CreateCallbackUpdate(data));

        Assert.Null(context.GetCallbackAction());
    }

    /// <summary>
    /// 非回调更新取不到 Action
    /// </summary>
    [Fact]
    public void GetCallbackAction_WhenNotCallback_ReturnsNull()
    {
        using var bot = TelegramTestFactory.CreateBot();
        var context = new TelegramBotContext(bot, TelegramTestFactory.CreateMessageUpdate(text: "confirm:1"));

        Assert.Null(context.GetCallbackAction());
    }

    /// <summary>
    /// 语言代码归一化为「小写语言-大写地区」，未识别时回退 zh-CN
    /// </summary>
    /// <param name="languageCode">Telegram 客户端语言代码</param>
    /// <param name="expected">归一化结果</param>
    [Theory]
    [InlineData("zh-cn", "zh-CN")]
    [InlineData("zh_CN", "zh-CN")]
    [InlineData("EN-us", "en-US")]
    [InlineData("EN", "en")]
    [InlineData("pt-br-x", "pt-BR")]
    [InlineData(null, "zh-CN")]
    [InlineData("", "zh-CN")]
    [InlineData("   ", "zh-CN")]
    public void LanguageCode_IsNormalizedWithZhCnFallback(string? languageCode, string expected)
    {
        using var bot = TelegramTestFactory.CreateBot();
        var update = new Update { Message = TelegramTestFactory.CreateMessage(languageCode: languageCode) };

        Assert.Equal(expected, new TelegramBotContext(bot, update).LanguageCode);
    }

    /// <summary>
    /// 回调更新的语言代码取自回调发起者
    /// </summary>
    [Fact]
    public void LanguageCode_ForCallback_ComesFromCallbackSender()
    {
        using var bot = TelegramTestFactory.CreateBot();
        var update = TelegramTestFactory.CreateCallbackUpdate("confirm:1");
        update.CallbackQuery!.From.LanguageCode = "ja";

        Assert.Equal("ja", new TelegramBotContext(bot, update).LanguageCode);
    }

    /// <summary>
    /// 默认未设置回调应答，也未被处理器自行应答
    /// </summary>
    [Fact]
    public void CallbackAnswer_DefaultsAreUnsetAndUnanswered()
    {
        using var bot = TelegramTestFactory.CreateBot();
        var context = new TelegramBotContext(bot, TelegramTestFactory.CreateCallbackUpdate("confirm:1"));

        Assert.Null(context.CallbackAnswerText);
        Assert.False(context.CallbackAnswerShowAlert);
        Assert.False(context.CallbackAnswered);
    }

    /// <summary>
    /// 设置回调应答文本与弹窗标记后可读回
    /// </summary>
    [Fact]
    public void SetCallbackAnswer_StoresTextAndAlertFlag()
    {
        using var bot = TelegramTestFactory.CreateBot();
        var context = new TelegramBotContext(bot, TelegramTestFactory.CreateCallbackUpdate("confirm:1"));

        context.SetCallbackAnswer("已确认", showAlert: true);

        Assert.Equal("已确认", context.CallbackAnswerText);
        Assert.True(context.CallbackAnswerShowAlert);
        Assert.False(context.CallbackAnswered);
    }

    /// <summary>
    /// 传 null 表示仅结束客户端 loading，弹窗标记默认关闭
    /// </summary>
    [Fact]
    public void SetCallbackAnswer_WithNullText_KeepsAlertDisabledByDefault()
    {
        using var bot = TelegramTestFactory.CreateBot();
        var context = new TelegramBotContext(bot, TelegramTestFactory.CreateCallbackUpdate("confirm:1"));

        context.SetCallbackAnswer(null);

        Assert.Null(context.CallbackAnswerText);
        Assert.False(context.CallbackAnswerShowAlert);
    }

    /// <summary>
    /// 标记已自行应答后路由器不再补答
    /// </summary>
    [Fact]
    public void MarkCallbackAnswered_SetsAnsweredFlag()
    {
        using var bot = TelegramTestFactory.CreateBot();
        var context = new TelegramBotContext(bot, TelegramTestFactory.CreateCallbackUpdate("confirm:1"));

        context.MarkCallbackAnswered();

        Assert.True(context.CallbackAnswered);
    }

    /// <summary>
    /// 兜底回复开关由分发器实时写入，默认关闭
    /// </summary>
    [Fact]
    public void EnableFallbackReply_DefaultsToFalseAndIsWritable()
    {
        using var bot = TelegramTestFactory.CreateBot();
        var context = new TelegramBotContext(bot, TelegramTestFactory.CreateMessageUpdate());

        Assert.False(context.EnableFallbackReply);

        context.EnableFallbackReply = true;

        Assert.True(context.EnableFallbackReply);
    }
}
