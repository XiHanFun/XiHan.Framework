#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TelegramBotContext
// Guid:e5798cda-1c13-4f02-a300-1027473cdc27
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using XiHan.Framework.Bot.Telegram.Platform.MultiBot;
using XiHan.Framework.Bot.Telegram.Platform.Options;

namespace XiHan.Framework.Bot.Telegram.Platform.Core;

/// <summary>
/// 统一封装一次 Telegram Update 的运行上下文
/// </summary>
public sealed class TelegramBotContext
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="bot">当前机器人实例</param>
    /// <param name="update">Telegram 原始 Update</param>
    public TelegramBotContext(BotInstance bot, Update update)
    {
        Bot = bot ?? throw new ArgumentNullException(nameof(bot));
        Update = update ?? throw new ArgumentNullException(nameof(update));
    }

    /// <summary>
    /// 当前处理该消息的机器人实例
    /// </summary>
    public BotInstance Bot { get; }

    /// <summary>
    /// Telegram 原始 Update
    /// </summary>
    public Update Update { get; }

    /// <summary>
    /// 文本路由覆盖值（由前置逻辑注入，优先于消息原文参与路由）
    /// </summary>
    public string? TextOverride { get; set; }

    /// <summary>
    /// 当前 Bot 客户端
    /// </summary>
    public ITelegramBotClient Client => Bot.Client;

    /// <summary>
    /// 当前 Update 对应的消息（含编辑消息与频道贴文；回调消息不归并到这里）
    /// </summary>
    public Message? Message =>
        Update.Message ??
        Update.EditedMessage ??
        Update.ChannelPost ??
        Update.EditedChannelPost;

    /// <summary>
    /// 当前 Update 的回调对象
    /// </summary>
    public CallbackQuery? Callback => Update.CallbackQuery;

    /// <summary>
    /// 当前会话 ChatId
    /// </summary>
    public long ChatId =>
        Message?.Chat.Id ??
        Callback?.Message?.Chat.Id ??
        0;

    /// <summary>
    /// 当前操作用户 Id
    /// </summary>
    public long UserId =>
        Message?.From?.Id ??
        Callback?.From.Id ??
        0;

    /// <summary>
    /// 触发本次处理的消息 Id（用于 Reply）
    /// </summary>
    public int? TriggerMessageId =>
        Message?.MessageId ??
        Callback?.Message?.MessageId;

    /// <summary>
    /// 原始文本（优先覆盖值，其次消息文本，再次图片说明）
    /// </summary>
    public string? Text => TextOverride ?? Message?.Text ?? Message?.Caption;

    /// <summary>
    /// 当前是否为按钮回调
    /// </summary>
    public bool IsCallback => Callback is not null;

    /// <summary>
    /// 当前是否为命令消息
    /// </summary>
    public bool IsCommand =>
        !IsCallback &&
        !string.IsNullOrWhiteSpace(Text) &&
        Text!.TrimStart().StartsWith('/');

    /// <summary>
    /// 当前是否为回复消息（ReplyToMessage 不为空）
    /// </summary>
    public bool IsReply => Message?.ReplyToMessage is not null;

    /// <summary>
    /// 是否为群聊（含超级群）
    /// </summary>
    public bool IsGroup =>
        Message?.Chat.Type is ChatType.Group or ChatType.Supergroup ||
        Callback?.Message?.Chat.Type is ChatType.Group or ChatType.Supergroup;

    /// <summary>
    /// 是否为频道（频道贴文与频道消息上的回调；与群组共用同一白名单守卫）
    /// </summary>
    public bool IsChannel =>
        Message?.Chat.Type is ChatType.Channel ||
        Callback?.Message?.Chat.Type is ChatType.Channel;

    /// <summary>
    /// 当前用户是否为该机器人管理员（来自机器人配置）
    /// </summary>
    public bool IsAdmin => Bot.IsAdmin(UserId);

    /// <summary>
    /// 是否启用兜底回复（分发时从设置存储实时解析）
    /// </summary>
    public bool EnableFallbackReply { get; set; }

    /// <summary>
    /// 用户语言代码（Telegram 客户端语言，未识别时回退 zh-CN）
    /// </summary>
    public string LanguageCode => NormalizeLanguageCode(Message?.From?.LanguageCode ?? Callback?.From.LanguageCode) ?? "zh-CN";

    /// <summary>
    /// 回调应答文本（由处理器通过 <see cref="SetCallbackAnswer"/> 设置，路由器统一应答）
    /// </summary>
    public string? CallbackAnswerText { get; private set; }

    /// <summary>
    /// 回调应答是否以弹窗显示
    /// </summary>
    public bool CallbackAnswerShowAlert { get; private set; }

    /// <summary>
    /// 回调是否已由处理器自行应答（为 true 时路由器不再补答）
    /// </summary>
    public bool CallbackAnswered { get; private set; }

    /// <summary>
    /// 设置本次按钮回调的应答文本（路由器在处理结束后统一调用 AnswerCallbackQuery）
    /// </summary>
    /// <param name="text">应答文本（null 表示仅结束客户端 loading）</param>
    /// <param name="showAlert">是否以弹窗形式显示</param>
    public void SetCallbackAnswer(string? text, bool showAlert = false)
    {
        CallbackAnswerText = text;
        CallbackAnswerShowAlert = showAlert;
    }

    /// <summary>
    /// 标记本次按钮回调已由处理器自行应答（路由器不再补答）
    /// </summary>
    public void MarkCallbackAnswered()
    {
        CallbackAnswered = true;
    }

    /// <summary>
    /// 提取命令 token（保留 @bot 后缀，如 /order@my_bot）
    /// </summary>
    /// <returns>命令 token；非命令时为 null</returns>
    public string? GetCommandToken()
    {
        if (!IsCommand || string.IsNullOrWhiteSpace(Text))
        {
            return null;
        }

        var parts = Text.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length > 0 ? parts[0] : null;
    }

    /// <summary>
    /// 提取命令参数
    /// </summary>
    /// <returns>参数数组</returns>
    public string[] GetCommandArgs()
    {
        if (!IsCommand || string.IsNullOrWhiteSpace(Text))
        {
            return [];
        }

        var parts = Text.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return [.. parts.Skip(1)];
    }

    /// <summary>
    /// 提取回调 Action（callback data 中第一个冒号之前的部分）
    /// </summary>
    /// <returns>回调 Action；无回调数据时为 null</returns>
    public string? GetCallbackAction()
    {
        var data = Callback?.Data;
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        var index = data.IndexOf(TelegramBotPlatformConsts.CallbackDataSeparator);
        return (index >= 0 ? data[..index] : data).Trim();
    }

    private static string? NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return null;
        }

        var value = languageCode.Trim().Replace('_', '-');
        var parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => null,
            1 => parts[0].ToLowerInvariant(),
            _ => $"{parts[0].ToLowerInvariant()}-{parts[1].ToUpperInvariant()}"
        };
    }
}
