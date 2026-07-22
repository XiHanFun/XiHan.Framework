// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Options;

namespace XiHan.Framework.Bot.Pipeline;

/// <summary>
/// 日志管道
/// </summary>
public class LoggingPipeline : IBotPipeline
{
    private readonly XiHanBotOptions _options;
    private readonly ILogger<LoggingPipeline> _logger;

    /// <summary>
    /// 创建日志管道
    /// </summary>
    public LoggingPipeline(IOptions<XiHanBotOptions> options, ILogger<LoggingPipeline> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 执行管道
    /// </summary>
    public async Task InvokeAsync(BotContext context, Func<Task> next)
    {
        if (!_options.EnableLoggingPipeline)
        {
            await next();
            return;
        }

        var providerNames = context.Providers.Select(provider => provider.Name);
        _logger.LogInformation(
            "Bot dispatch started. Type: {Type}, Providers: {Providers}",
            context.Message.Type,
            string.Join(", ", providerNames));

        try
        {
            await next();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bot dispatch exception.");
            throw;
        }
        finally
        {
            if (context.IsSkipped)
            {
                _logger.LogInformation("Bot dispatch skipped by environment filter.");
            }

            if (context.Results.Count == 0)
            {
                _logger.LogWarning("Bot dispatch finished with no results.");
            }

            foreach (var result in context.Results)
            {
                if (result.IsSuccess)
                {
                    _logger.LogInformation(
                        "Bot dispatch success. Provider: {Provider}, Message: {Message}",
                        result.Provider,
                        result.Message);
                }
                else
                {
                    _logger.LogWarning(
                        "Bot dispatch failed. Provider: {Provider}, Message: {Message}",
                        result.Provider,
                        result.Message);
                }
            }
        }
    }
}
