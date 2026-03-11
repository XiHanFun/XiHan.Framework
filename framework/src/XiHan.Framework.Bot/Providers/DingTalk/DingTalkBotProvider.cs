#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DingTalkBotProvider
// Guid:02693b94-e451-46bc-b1f6-64c867bc0b23
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:48:01
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Helpers;
using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.Providers.DingTalk;

/// <summary>
/// 钉钉 Bot 提供者
/// </summary>
public class DingTalkBotProvider : IBotProvider
{
    private readonly IOptionsMonitor<DingTalkOptions> _options;

    /// <summary>
    /// 创建提供者
    /// </summary>
    public DingTalkBotProvider(IOptionsMonitor<DingTalkOptions> options)
    {
        _options = options;
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
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            return BotResult.BadRequest("DingTalk provider is disabled.", Name);
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
                return BotResult.From(await bot.MarkdownMessage(markdown, BuildAt(message)), Name);

            case BotMessageType.Link:
                if (BotMessageHelper.TryGetData(message, BotMessageDataKeys.DingTalkLink, out DingTalkLink? link) && link is not null)
                {
                    return BotResult.From(await bot.LinkMessage(link), Name);
                }
                break;

            case BotMessageType.Card:
                if (BotMessageHelper.TryGetData(message, BotMessageDataKeys.DingTalkActionCard, out DingTalkActionCard? card) && card is not null)
                {
                    return BotResult.From(await bot.ActionCardMessage(card), Name);
                }
                if (BotMessageHelper.TryGetData(message, BotMessageDataKeys.DingTalkFeedCard, out DingTalkFeedCard? feedCard) && feedCard is not null)
                {
                    return BotResult.From(await bot.FeedCardMessage(feedCard), Name);
                }
                break;
        }

        var text = new DingTalkText
        {
            Content = message.Content
        };
        return BotResult.From(await bot.TextMessage(text, BuildAt(message)), Name);
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
