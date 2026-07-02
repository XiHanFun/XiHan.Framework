#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITelegramBotConfigStore
// Guid:e67512f8-0806-4874-b722-0f8363559b48
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Telegram.Platform.Options;

namespace XiHan.Framework.Bot.Telegram.Platform.Abstractions;

/// <summary>
/// Telegram 机器人配置存储（机器人列表）
/// </summary>
/// <remarks>
/// 默认实现从 IOptionsMonitor 读取（配置文件仅兜底）；生产环境由应用层注册数据库实现覆盖（TryAdd 语义）。
/// 管理器按刷新周期轮询该存储，配置改动无需重启应用即可生效。
/// </remarks>
public interface ITelegramBotConfigStore
{
    /// <summary>
    /// 获取当前生效的机器人配置列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>机器人配置列表</returns>
    Task<IReadOnlyList<TelegramBotConfig>> GetBotConfigsAsync(CancellationToken cancellationToken = default);
}
