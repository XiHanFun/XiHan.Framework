#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITelegramConfigStore
// Guid:b336779c-a832-481f-939c-b2437ec6847c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Abstractions;

/// <summary>
/// Telegram 配置存储
/// </summary>
/// <remarks>
/// 默认实现从 IOptionsMonitor 读取；应用层可注册数据库实现覆盖（TryAdd 语义）。
/// </remarks>
public interface ITelegramConfigStore
{
    /// <summary>
    /// 获取当前生效配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前生效配置；null 表示未配置（提供者按未配置处理）</returns>
    Task<TelegramOptions?> GetAsync(CancellationToken cancellationToken = default);
}
