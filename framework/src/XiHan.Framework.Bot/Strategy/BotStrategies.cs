// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Bot.Providers;

namespace XiHan.Framework.Bot.Strategy;

/// <summary>
/// 广播策略
/// </summary>
public class BroadcastStrategy : IBotStrategy
{
    private readonly XiHanBotOptions _options;
    private readonly ILogger<BroadcastStrategy> _logger;

    /// <summary>
    /// 创建广播策略
    /// </summary>
    public BroadcastStrategy(IOptions<XiHanBotOptions> options, ILogger<BroadcastStrategy> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 策略名称
    /// </summary>
    public string Name => BotStrategyNames.Broadcast;

    /// <summary>
    /// 执行策略
    /// </summary>
    public async Task ExecuteAsync(BotContext context, IReadOnlyList<IBotProvider> providers)
    {
        foreach (var provider in providers)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var result = await SafeSendAsync(provider, context);
            context.AddResult(provider.Name, result);

            if (!result.IsSuccess && !_options.ContinueOnError)
            {
                break;
            }
        }
    }

    private async Task<BotResult> SafeSendAsync(IBotProvider provider, BotContext context)
    {
        try
        {
            return await provider.SendAsync(context.Message, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bot provider exception. Provider: {Provider}", provider.Name);
            return BotResult.Failed(ex.Message, provider.Name);
        }
    }
}
