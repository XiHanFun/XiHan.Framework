#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RetryPipeline
// Guid:c865c407-d61d-48fd-8dd7-2bc8ccc49f4a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:45:28
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Options;

namespace XiHan.Framework.Bot.Pipeline;

/// <summary>
/// 重试管道
/// </summary>
public class RetryPipeline : IBotPipeline
{
    private readonly XiHanBotOptions _options;
    private readonly ILogger<RetryPipeline> _logger;

    /// <summary>
    /// 创建重试管道
    /// </summary>
    public RetryPipeline(IOptions<XiHanBotOptions> options, ILogger<RetryPipeline> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 执行管道
    /// </summary>
    public async Task InvokeAsync(BotContext context, Func<Task> next)
    {
        if (!_options.EnableRetryPipeline || _options.RetryCount <= 1)
        {
            await next();
            return;
        }

        var originalProviders = context.Providers.ToArray();
        for (var attempt = 1; attempt <= _options.RetryCount; attempt++)
        {
            context.ClearResults();
            context.LastException = null;

            try
            {
                await next();
            }
            catch (Exception ex)
            {
                context.LastException = ex;
            }

            if (context.IsSuccess)
            {
                return;
            }

            if (attempt >= _options.RetryCount)
            {
                if (context.LastException is not null)
                {
                    throw context.LastException;
                }

                return;
            }

            _logger.LogWarning(
                "Bot dispatch retry. Attempt: {Attempt}, Total: {Total}",
                attempt + 1,
                _options.RetryCount);

            var failedProviders = context.Results
                .Where(result => !result.Result.IsSuccess)
                .Select(result => result.ProviderName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (failedProviders.Count > 0)
            {
                context.SetProviders(originalProviders.Where(provider => failedProviders.Contains(provider.Name)));
            }
            else
            {
                context.SetProviders(originalProviders);
            }

            if (_options.RetryDelay > TimeSpan.Zero)
            {
                await Task.Delay(_options.RetryDelay, context.CancellationToken);
            }
        }
    }
}
