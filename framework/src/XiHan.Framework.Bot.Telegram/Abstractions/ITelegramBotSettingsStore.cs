#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITelegramBotSettingsStore
// Guid:735e4dda-485e-4671-b087-903a6544db1a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Abstractions;

/// <summary>
/// Telegram 机器人平台全局设置存储
/// </summary>
/// <remarks>
/// 默认实现从 IOptionsMonitor 读取（配置文件仅兜底）；生产环境由应用层注册数据库实现覆盖（TryAdd 语义）。
/// </remarks>
public interface ITelegramBotSettingsStore
{
    /// <summary>
    /// 获取当前生效的平台全局设置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>平台全局设置</returns>
    Task<TelegramBotSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
}
