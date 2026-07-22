// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Core;

namespace XiHan.Framework.Bot.Pipeline;

/// <summary>
/// Bot 管道抽象
/// </summary>
public interface IBotPipeline
{
    /// <summary>
    /// 执行管道
    /// </summary>
    Task InvokeAsync(BotContext context, Func<Task> next);
}
