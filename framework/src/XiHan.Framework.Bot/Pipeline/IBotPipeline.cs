#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IBotPipeline
// Guid:466dd498-3c02-401c-b973-72fae8b3a0f5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 18:15:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
