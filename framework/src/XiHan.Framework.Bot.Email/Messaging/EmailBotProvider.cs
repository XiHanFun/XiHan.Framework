// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Helpers;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Email.Abstractions;
using XiHan.Framework.Bot.Email.Models;
using XiHan.Framework.Bot.Email.Options;

namespace XiHan.Framework.Bot.Email.Messaging;

/// <summary>
/// 邮件 Bot 提供者
/// </summary>
public class EmailBotProvider : IBotProvider
{
    private readonly IEmailConfigStore _configStore;

    /// <summary>
    /// 创建提供者
    /// </summary>
    public EmailBotProvider(IEmailConfigStore configStore)
    {
        _configStore = configStore;
    }

    /// <summary>
    /// 提供者名称
    /// </summary>
    public string Name => BotProviderNames.Email;

    /// <summary>
    /// 发送消息
    /// </summary>
    public async Task<BotResult> SendAsync(BotMessage message, BotContext context)
    {
        var options = await _configStore.GetAsync(context.CancellationToken);
        if (options is null || !options.Enabled)
        {
            return BotResult.BadRequest("Email provider is not configured or disabled.", Name);
        }

        if (string.IsNullOrWhiteSpace(options.From.SmtpHost) || string.IsNullOrWhiteSpace(options.From.FromMail))
        {
            return BotResult.BadRequest("Email sender configuration is required.", Name);
        }

        var to = ResolveRecipients(message, EmailMessageDataKeys.EmailTo, options.To);
        var cc = ResolveRecipients(message, EmailMessageDataKeys.EmailCc, options.Cc);
        var bcc = ResolveRecipients(message, EmailMessageDataKeys.EmailBcc, options.Bcc);

        if (to.Count == 0 && cc.Count == 0 && bcc.Count == 0)
        {
            return BotResult.BadRequest("Email recipients are required.", Name);
        }

        var isBodyHtml = options.IsBodyHtml;
        if (BotMessageHelper.TryGetData(message, EmailMessageDataKeys.EmailIsBodyHtml, out bool bodyHtmlOverride))
        {
            isBodyHtml = bodyHtmlOverride;
        }

        var bot = new EmailBot(options.From);
        var toModel = new EmailToModel
        {
            Subject = message.Title ?? "Notification",
            Body = message.Content,
            IsBodyHtml = isBodyHtml,
            ToMail = to,
            CcMail = cc,
            BccMail = bcc
        };

        var success = await bot.SendMail(toModel, context.CancellationToken);
        return success
            ? BotResult.Success("Email sent.", Name)
            : BotResult.Failed("Email send failed.", Name);
    }

    private static List<string> ResolveRecipients(BotMessage message, string key, List<string> fallback)
    {
        if (message.Data.TryGetValue(key, out var data))
        {
            switch (data)
            {
                case string value when !string.IsNullOrWhiteSpace(value):
                    return [value];

                case IEnumerable<string> values:
                    return values.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
            }
        }

        return fallback;
    }
}
