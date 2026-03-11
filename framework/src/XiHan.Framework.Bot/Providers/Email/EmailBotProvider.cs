#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EmailBotProvider
// Guid:a72b2f12-7758-4aed-8f84-6b1fb7b961e4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:48:44
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Helpers;
using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.Providers.Email;

/// <summary>
/// 邮件 Bot 提供者
/// </summary>
public class EmailBotProvider : IBotProvider
{
    private readonly IOptionsMonitor<EmailOptions> _options;

    /// <summary>
    /// 创建提供者
    /// </summary>
    public EmailBotProvider(IOptionsMonitor<EmailOptions> options)
    {
        _options = options;
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
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            return BotResult.BadRequest("Email provider is disabled.", Name);
        }

        if (string.IsNullOrWhiteSpace(options.From.SmtpHost) || string.IsNullOrWhiteSpace(options.From.FromMail))
        {
            return BotResult.BadRequest("Email sender configuration is required.", Name);
        }

        var to = ResolveRecipients(message, BotMessageDataKeys.EmailTo, options.To);
        var cc = ResolveRecipients(message, BotMessageDataKeys.EmailCc, options.Cc);
        var bcc = ResolveRecipients(message, BotMessageDataKeys.EmailBcc, options.Bcc);

        if (to.Count == 0 && cc.Count == 0 && bcc.Count == 0)
        {
            return BotResult.BadRequest("Email recipients are required.", Name);
        }

        var isBodyHtml = options.IsBodyHtml;
        if (BotMessageHelper.TryGetData(message, BotMessageDataKeys.EmailIsBodyHtml, out bool bodyHtmlOverride))
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

        var success = await bot.SendMail(toModel);
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
