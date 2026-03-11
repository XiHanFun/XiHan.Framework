#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:WeComBotProvider
// Guid:89180cdf-3ce9-43f4-9027-c6cd1c1cdfa8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:48:26
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Helpers;
using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.Providers.WeCom;

/// <summary>
/// 企业微信 Bot 提供者
/// </summary>
public class WeComBotProvider : IBotProvider
{
    private readonly IOptionsMonitor<WeComOptions> _options;

    /// <summary>
    /// 创建提供者
    /// </summary>
    public WeComBotProvider(IOptionsMonitor<WeComOptions> options)
    {
        _options = options;
    }

    /// <summary>
    /// 提供者名称
    /// </summary>
    public string Name => BotProviderNames.WeCom;

    /// <summary>
    /// 发送消息
    /// </summary>
    public async Task<BotResult> SendAsync(BotMessage message, BotContext context)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            return BotResult.BadRequest("WeCom provider is disabled.", Name);
        }

        if (string.IsNullOrWhiteSpace(options.Key))
        {
            return BotResult.BadRequest("WeCom key is required.", Name);
        }

        var bot = new WeComBot(options);
        switch (message.Type)
        {
            case BotMessageType.Markdown:
                var markdown = new WeComMarkdown
                {
                    Content = message.Content
                };
                return BotResult.From(await bot.MarkdownMessage(markdown), Name);

            case BotMessageType.Image:
                if (BotMessageHelper.TryGetData(message, BotMessageDataKeys.WeComImage, out WeComImage? image) && image is not null)
                {
                    return BotResult.From(await bot.ImageMessage(image), Name);
                }
                break;

            case BotMessageType.File:
                if (BotMessageHelper.TryGetData(message, BotMessageDataKeys.WeComFile, out WeComFile? file) && file is not null)
                {
                    return BotResult.From(await bot.FileMessage(file), Name);
                }
                break;

            case BotMessageType.Card:
                if (BotMessageHelper.TryGetData(message, BotMessageDataKeys.WeComTemplateCardTextNotice, out WeComTemplateCardTextNotice? textNotice) && textNotice is not null)
                {
                    return BotResult.From(await bot.TextNoticeMessage(textNotice), Name);
                }
                if (BotMessageHelper.TryGetData(message, BotMessageDataKeys.WeComTemplateCardNewsNotice, out WeComTemplateCardNewsNotice? newsNotice) && newsNotice is not null)
                {
                    return BotResult.From(await bot.NewsNoticeMessage(newsNotice), Name);
                }
                break;

            case BotMessageType.Link:
                if (BotMessageHelper.TryGetData(message, BotMessageDataKeys.WeComNews, out WeComNews? news) && news is not null)
                {
                    return BotResult.From(await bot.NewsMessage(news), Name);
                }
                break;
        }

        var text = new WeComText
        {
            Content = message.Content
        };
        ApplyMentions(text, message);
        return BotResult.From(await bot.TextMessage(text), Name);
    }

    private static void ApplyMentions(WeComText text, BotMessage message)
    {
        if (message.Mentions.Count == 0)
        {
            return;
        }

        text.Mentions = message.Mentions;
        text.MentionedMobiles = message.Mentions;
    }
}
