#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LarkBotProvider
// Guid:b5ac29c5-2872-4fc8-b5ed-2e782bddfcac
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:48:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Helpers;
using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.Providers.Lark;

/// <summary>
/// 飞书 Bot 提供者
/// </summary>
public class LarkBotProvider : IBotProvider
{
    private readonly IOptionsMonitor<LarkOptions> _options;

    /// <summary>
    /// 创建提供者
    /// </summary>
    public LarkBotProvider(IOptionsMonitor<LarkOptions> options)
    {
        _options = options;
    }

    /// <summary>
    /// 提供者名称
    /// </summary>
    public string Name => BotProviderNames.Lark;

    /// <summary>
    /// 发送消息
    /// </summary>
    public async Task<BotResult> SendAsync(BotMessage message, BotContext context)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            return BotResult.BadRequest("Lark provider is disabled.", Name);
        }

        if (string.IsNullOrWhiteSpace(options.AccessToken))
        {
            return BotResult.BadRequest("Lark access token is required.", Name);
        }

        var bot = new LarkBot(options);
        switch (message.Type)
        {
            case BotMessageType.Card:
                if (BotMessageHelper.TryGetData(message, BotMessageDataKeys.LarkInterActive, out LarkInterActive? card) && card is not null)
                {
                    return BotResult.From(await bot.InterActiveMessage(card), Name);
                }
                break;

            case BotMessageType.Image:
                if (BotMessageHelper.TryGetData(message, BotMessageDataKeys.LarkImage, out LarkImage? image) && image is not null)
                {
                    return BotResult.From(await bot.ImageMessage(image), Name);
                }
                break;
        }

        if (BotMessageHelper.TryGetData(message, BotMessageDataKeys.LarkPost, out LarkPost? post) && post is not null)
        {
            return BotResult.From(await bot.PostMessage(post), Name);
        }

        var text = new LarkText
        {
            Text = message.Content
        };
        return BotResult.From(await bot.TextMessage(text), Name);
    }
}
