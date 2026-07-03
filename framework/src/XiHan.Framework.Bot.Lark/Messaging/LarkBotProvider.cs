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

using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Helpers;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Lark.Abstractions;
using XiHan.Framework.Bot.Lark.Models;
using XiHan.Framework.Bot.Lark.Options;
using XiHan.Framework.Bot.Lark.Enums;

namespace XiHan.Framework.Bot.Lark.Messaging;

/// <summary>
/// 飞书 Bot 提供者
/// </summary>
public class LarkBotProvider : IBotProvider
{
    private readonly ILarkConfigStore _configStore;

    /// <summary>
    /// 创建提供者
    /// </summary>
    public LarkBotProvider(ILarkConfigStore configStore)
    {
        _configStore = configStore;
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
        var options = await _configStore.GetAsync(context.CancellationToken);
        if (options is null || !options.Enabled)
        {
            return BotResult.BadRequest("Lark provider is not configured or disabled.", Name);
        }

        if (string.IsNullOrWhiteSpace(options.AccessToken))
        {
            return BotResult.BadRequest("Lark access token is required.", Name);
        }

        var bot = new LarkBot(options);
        switch (message.Type)
        {
            case BotMessageType.Card:
                if (BotMessageHelper.TryGetData(message, LarkMessageDataKeys.LarkInterActive, out LarkInterActive? card) && card is not null)
                {
                    return BotResult.From(await bot.InterActiveMessage(card, context.CancellationToken), Name);
                }
                break;

            case BotMessageType.Image:
                if (BotMessageHelper.TryGetData(message, LarkMessageDataKeys.LarkImage, out LarkImage? image) && image is not null)
                {
                    return BotResult.From(await bot.ImageMessage(image, context.CancellationToken), Name);
                }
                break;
        }

        if (BotMessageHelper.TryGetData(message, LarkMessageDataKeys.LarkPost, out LarkPost? post) && post is not null)
        {
            return BotResult.From(await bot.PostMessage(post, context.CancellationToken), Name);
        }

        var text = new LarkText
        {
            Text = message.Content
        };
        return BotResult.From(await bot.TextMessage(text, context.CancellationToken), Name);
    }
}
