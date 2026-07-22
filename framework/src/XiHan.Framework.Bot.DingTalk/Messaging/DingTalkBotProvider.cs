// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Helpers;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.DingTalk.Abstractions;
using XiHan.Framework.Bot.DingTalk.Models;
using XiHan.Framework.Bot.DingTalk.Options;
using XiHan.Framework.Bot.DingTalk.Enums;

namespace XiHan.Framework.Bot.DingTalk.Messaging;

/// <summary>
/// 钉钉 Bot 提供者
/// </summary>
public class DingTalkBotProvider : IBotProvider
{
    private readonly IDingTalkConfigStore _configStore;

    /// <summary>
    /// 创建提供者
    /// </summary>
    public DingTalkBotProvider(IDingTalkConfigStore configStore)
    {
        _configStore = configStore;
    }

    /// <summary>
    /// 提供者名称
    /// </summary>
    public string Name => BotProviderNames.DingTalk;

    /// <summary>
    /// 发送消息
    /// </summary>
    public async Task<BotResult> SendAsync(BotMessage message, BotContext context)
    {
        var options = await _configStore.GetAsync(context.CancellationToken);
        if (options is null || !options.Enabled)
        {
            return BotResult.BadRequest("DingTalk provider is not configured or disabled.", Name);
        }

        if (string.IsNullOrWhiteSpace(options.AccessToken))
        {
            return BotResult.BadRequest("DingTalk access token is required.", Name);
        }

        var bot = new DingTalkBot(options);
        switch (message.Type)
        {
            case BotMessageType.Markdown:
                var markdown = new DingTalkMarkdown
                {
                    Title = message.Title ?? "Notification",
                    Text = message.Content
                };
                return BotResult.From(await bot.MarkdownMessage(markdown, BuildAt(message), context.CancellationToken), Name);

            case BotMessageType.Link:
                if (BotMessageHelper.TryGetData(message, DingTalkMessageDataKeys.DingTalkLink, out DingTalkLink? link) && link is not null)
                {
                    return BotResult.From(await bot.LinkMessage(link, context.CancellationToken), Name);
                }
                break;

            case BotMessageType.Card:
                if (BotMessageHelper.TryGetData(message, DingTalkMessageDataKeys.DingTalkActionCard, out DingTalkActionCard? card) && card is not null)
                {
                    return BotResult.From(await bot.ActionCardMessage(card, context.CancellationToken), Name);
                }
                if (BotMessageHelper.TryGetData(message, DingTalkMessageDataKeys.DingTalkFeedCard, out DingTalkFeedCard? feedCard) && feedCard is not null)
                {
                    return BotResult.From(await bot.FeedCardMessage(feedCard, context.CancellationToken), Name);
                }
                break;
        }

        var text = new DingTalkText
        {
            Content = message.Content
        };
        return BotResult.From(await bot.TextMessage(text, BuildAt(message), context.CancellationToken), Name);
    }

    private static DingTalkAt? BuildAt(BotMessage message)
    {
        if (message.Mentions.Count == 0)
        {
            return null;
        }

        var at = new DingTalkAt();
        var mobiles = message.Mentions
            .Where(item => !string.Equals(item, "@all", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (mobiles.Count > 0)
        {
            at.AtMobiles = mobiles;
        }

        at.IsAtAll = message.Mentions.Any(item => string.Equals(item, "@all", StringComparison.OrdinalIgnoreCase));
        return at;
    }
}
