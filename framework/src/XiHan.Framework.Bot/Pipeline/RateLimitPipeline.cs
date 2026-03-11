#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RateLimitPipeline
// Guid:61503ed0-6812-4bcb-baa7-de1b998c9653
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:45:28
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Options;

namespace XiHan.Framework.Bot.Pipeline;

/// <summary>
/// 限流管道
/// </summary>
public class RateLimitPipeline : IBotPipeline
{
    private readonly XiHanBotOptions _options;
    private readonly Queue<DateTime> _timestamps = new();
    private readonly object _lock = new();

    /// <summary>
    /// 创建限流管道
    /// </summary>
    public RateLimitPipeline(IOptions<XiHanBotOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// 执行管道
    /// </summary>
    public async Task InvokeAsync(BotContext context, Func<Task> next)
    {
        if (!_options.EnableRateLimitPipeline || _options.RateLimitPerSecond <= 0)
        {
            await next();
            return;
        }

        await WaitAsync(context.CancellationToken);
        await next();
    }

    private async Task WaitAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TimeSpan delay;

            lock (_lock)
            {
                var now = DateTime.UtcNow;
                while (_timestamps.Count > 0 && now - _timestamps.Peek() >= TimeSpan.FromSeconds(1))
                {
                    _timestamps.Dequeue();
                }

                if (_timestamps.Count < _options.RateLimitPerSecond)
                {
                    _timestamps.Enqueue(now);
                    return;
                }

                var earliest = _timestamps.Peek();
                delay = TimeSpan.FromSeconds(1) - (now - earliest);
            }

            if (delay <= TimeSpan.Zero)
            {
                continue;
            }

            await Task.Delay(delay, cancellationToken);
        }
    }
}
