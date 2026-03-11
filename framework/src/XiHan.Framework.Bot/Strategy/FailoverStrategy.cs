#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FailoverStrategy
// Guid:808b59bc-5385-4c71-bbb0-7bfef8ca1694
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:45:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Providers;

namespace XiHan.Framework.Bot.Strategy;

/// <summary>
/// 主备策略
/// </summary>
public class FailoverStrategy : IBotStrategy
{
    private readonly ILogger<FailoverStrategy> _logger;

    /// <summary>
    /// 创建主备策略
    /// </summary>
    public FailoverStrategy(ILogger<FailoverStrategy> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 策略名称
    /// </summary>
    public string Name => BotStrategyNames.Failover;

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

            if (result.IsSuccess)
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
