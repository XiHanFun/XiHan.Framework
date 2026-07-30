// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Helpers;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Sms.Abstractions;
using XiHan.Framework.Bot.Sms.Options;
using XiHan.Framework.Bot.Sms.Enums;

namespace XiHan.Framework.Bot.Sms.Messaging;

/// <summary>
/// 短信 Bot 提供者
/// </summary>
public class SmsBotProvider : IBotProvider
{
    private readonly ISmsGatewayResolver _gatewayResolver;

    /// <summary>
    /// 创建提供者
    /// </summary>
    public SmsBotProvider(ISmsGatewayResolver gatewayResolver)
    {
        _gatewayResolver = gatewayResolver;
    }

    /// <summary>
    /// 提供者名称
    /// </summary>
    public string Name => BotProviderNames.Sms;

    /// <summary>
    /// 发送消息
    /// </summary>
    public async Task<BotResult> SendAsync(BotMessage message, BotContext context)
    {
        var phones = ResolvePhones(message);
        if (phones.Count == 0)
        {
            return BotResult.BadRequest("Sms recipients are required.", Name);
        }

        BotMessageHelper.TryGetData(message, SmsMessageDataKeys.TemplateCode, out string? templateCode);
        BotMessageHelper.TryGetData(message, SmsMessageDataKeys.TemplateParams, out string? templateParamsJson);

        var gateway = await _gatewayResolver.ResolveAsync(context.CancellationToken);
        if (gateway is null)
        {
            return BotResult.BadRequest("Sms provider is not configured or disabled.", Name);
        }

        try
        {
            var result = await gateway.SendAsync(
                new SmsGatewayRequest(phones, templateCode, templateParamsJson, message.Content),
                context.CancellationToken);
            return result.IsSuccess
                ? BotResult.Success(result.ProviderMessageId, Name)
                : BotResult.Failed(result.ErrorMessage ?? "Sms send failed.", Name);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 模板映射缺失/参数不合法/SDK 异常等一律折叠为失败结果，不打断多提供者调度
            return BotResult.Failed(ex.Message, Name);
        }
    }

    private static List<string> ResolvePhones(BotMessage message)
    {
        if (message.Data.TryGetValue(SmsMessageDataKeys.PhoneNumbers, out var data))
        {
            switch (data)
            {
                case string value when !string.IsNullOrWhiteSpace(value):
                    return [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

                case IEnumerable<string> values:
                    return values.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
            }
        }

        return [];
    }
}
