#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TelegramBotProvider
// Guid:c3058a1c-0b99-4ccc-8cfa-bd0aa6b86ee3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:49:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Helpers;
using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.Providers.Telegram;

/// <summary>
/// Telegram Bot 提供者
/// </summary>
public class TelegramBotProvider : IBotProvider
{
    private readonly IOptionsMonitor<TelegramOptions> _options;

    /// <summary>
    /// 创建提供者
    /// </summary>
    public TelegramBotProvider(IOptionsMonitor<TelegramOptions> options)
    {
        _options = options;
    }

    /// <summary>
    /// 提供者名称
    /// </summary>
    public string Name => BotProviderNames.Telegram;

    /// <summary>
    /// 发送消息
    /// </summary>
    public async Task<BotResult> SendAsync(BotMessage message, BotContext context)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            return BotResult.BadRequest("Telegram provider is disabled.", Name);
        }

        if (string.IsNullOrWhiteSpace(options.Token))
        {
            return BotResult.BadRequest("Telegram token is required.", Name);
        }

        ChatId chatId;
        try
        {
            chatId = ResolveChatId(message, options);
        }
        catch (Exception ex)
        {
            return BotResult.BadRequest(ex.Message, Name);
        }

        var client = new TelegramBotClient(options.Token);
        var text = BuildText(message);
        var parseMode = ResolveParseMode(message, options);

        var result = parseMode.HasValue
            ? await client.SendMessage(
                chatId,
                text,
                parseMode: parseMode.Value,
                disableNotification: options.DisableNotification,
                cancellationToken: context.CancellationToken)
            : await client.SendMessage(
                chatId,
                text,
                disableNotification: options.DisableNotification,
                cancellationToken: context.CancellationToken);

        return BotResult.Success($"Telegram message sent. Id: {result.Id}", Name);
    }

    private static string BuildText(BotMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Title))
        {
            return message.Content;
        }

        return $"{message.Title}\n{message.Content}";
    }

    private static ChatId ResolveChatId(BotMessage message, TelegramOptions options)
    {
        var chatIdValue = options.ChatId;
        if (BotMessageHelper.TryGetData(message, BotMessageDataKeys.TelegramChatId, out string? overrideChatId)
            && !string.IsNullOrWhiteSpace(overrideChatId))
        {
            chatIdValue = overrideChatId;
        }

        if (string.IsNullOrWhiteSpace(chatIdValue))
        {
            throw new InvalidOperationException("Telegram chat id is required.");
        }

        return long.TryParse(chatIdValue, out var numericId)
            ? new ChatId(numericId)
            : new ChatId(chatIdValue);
    }

    private static ParseMode? ResolveParseMode(BotMessage message, TelegramOptions options)
    {
        if (BotMessageHelper.TryGetData(message, BotMessageDataKeys.TelegramParseMode, out string? parseModeValue)
            && TryParseParseMode(parseModeValue, out var parsed))
        {
            return parsed;
        }

        if (!string.IsNullOrWhiteSpace(options.ParseMode) && TryParseParseMode(options.ParseMode, out var optionParsed))
        {
            return optionParsed;
        }

        if (message.Type == BotMessageType.Markdown)
        {
            return ParseMode.Markdown;
        }

        return null;
    }

    private static bool TryParseParseMode(string? value, out ParseMode parsed)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && Enum.TryParse(value, true, out ParseMode result))
        {
            parsed = result;
            return true;
        }

        parsed = default;
        return false;
    }
}
