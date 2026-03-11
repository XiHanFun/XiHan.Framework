#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotDispatcher
// Guid:3911bfc0-5afd-45ea-a5fe-da07e931c3d4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:44:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Consts;
using XiHan.Framework.Bot.Helpers;
using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Options;
using XiHan.Framework.Bot.Pipeline;
using XiHan.Framework.Bot.Strategy;

namespace XiHan.Framework.Bot.Core;

/// <summary>
/// Bot 调度器
/// </summary>
public class BotDispatcher
{
    private readonly BotProviderManager _providerManager;
    private readonly IReadOnlyList<IBotPipeline> _pipelines;
    private readonly IReadOnlyList<IBotStrategy> _strategies;
    private readonly XiHanBotOptions _options;
    private readonly ILogger<BotDispatcher> _logger;

    /// <summary>
    /// 创建调度器
    /// </summary>
    public BotDispatcher(
        BotProviderManager providerManager,
        IEnumerable<IBotPipeline> pipelines,
        IEnumerable<IBotStrategy> strategies,
        IOptions<XiHanBotOptions> options,
        ILogger<BotDispatcher> logger)
    {
        _providerManager = providerManager;
        _pipelines = pipelines.ToArray();
        _strategies = strategies.ToArray();
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 调度发送消息
    /// </summary>
    public async Task DispatchAsync(BotMessage message, IReadOnlyList<string>? channels = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        var channelList = channels?
            .Where(channel => !string.IsNullOrWhiteSpace(channel))
            .Select(channel => channel.Trim())
            .ToArray() ?? [];

        var context = new BotContext(message, channelList, cancellationToken)
        {
            StrategyName = ResolveStrategyName(message)
        };

        var providers = _providerManager.ResolveProviders(channelList);
        if (providers.Count == 0)
        {
            var error = "No bot provider configured.";
            _logger.LogWarning("{Error}", error);
            if (_options.ThrowWhenNoProvider)
            {
                throw new InvalidOperationException(error);
            }

            return;
        }

        context.SetProviders(providers);
        var strategy = ResolveStrategy(context.StrategyName);

        Func<Task> finalStep = () => strategy.ExecuteAsync(context, context.Providers);
        await InvokePipelinesAsync(context, finalStep);
    }

    private string? ResolveStrategyName(BotMessage message)
    {
        if (BotMessageHelper.TryGetData(message, BotMessageDataKeys.Strategy, out string? strategyName)
            && !string.IsNullOrWhiteSpace(strategyName))
        {
            return strategyName;
        }

        return null;
    }

    private IBotStrategy ResolveStrategy(string? strategyName)
    {
        var name = string.IsNullOrWhiteSpace(strategyName) ? _options.DefaultStrategy : strategyName!;
        var strategy = _strategies.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? _strategies.FirstOrDefault(item => item.Name.Equals(BotStrategyNames.Broadcast, StringComparison.OrdinalIgnoreCase));

        if (strategy is null)
        {
            throw new InvalidOperationException($"Bot strategy '{name}' is not registered.");
        }

        return strategy;
    }

    private Task InvokePipelinesAsync(BotContext context, Func<Task> finalStep)
    {
        var pipeline = _pipelines.Reverse()
            .Aggregate(finalStep, (next, current) => () => current.InvokeAsync(context, next));

        return pipeline();
    }
}
