// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Core;
using XiHan.Framework.Bot.Options;

namespace XiHan.Framework.Bot.Pipeline;

/// <summary>
/// 环境过滤管道
/// </summary>
public class EnvironmentFilterPipeline : IBotPipeline
{
    private readonly XiHanBotOptions _options;
    private readonly IHostEnvironment? _environment;

    /// <summary>
    /// 创建环境过滤管道
    /// </summary>
    public EnvironmentFilterPipeline(IOptions<XiHanBotOptions> options, IHostEnvironment? environment = null)
    {
        _options = options.Value;
        _environment = environment;
    }

    /// <summary>
    /// 执行管道
    /// </summary>
    public async Task InvokeAsync(BotContext context, Func<Task> next)
    {
        if (!_options.EnableEnvironmentFilter || _options.AllowedEnvironments.Count == 0)
        {
            await next();
            return;
        }

        var environmentName = _environment?.EnvironmentName;
        if (string.IsNullOrWhiteSpace(environmentName))
        {
            await next();
            return;
        }

        var allowed = _options.AllowedEnvironments
            .Any(name => name.Equals(environmentName, StringComparison.OrdinalIgnoreCase));

        if (!allowed)
        {
            context.IsSkipped = true;
            return;
        }

        await next();
    }
}
