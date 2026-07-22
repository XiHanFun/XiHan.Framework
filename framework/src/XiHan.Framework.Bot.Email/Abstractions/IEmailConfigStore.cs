// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Email.Options;

namespace XiHan.Framework.Bot.Email.Abstractions;

/// <summary>
/// 邮件配置存储
/// </summary>
/// <remarks>
/// 默认实现从 IOptionsMonitor 读取；应用层可注册数据库实现覆盖（TryAdd 语义）。
/// </remarks>
public interface IEmailConfigStore
{
    /// <summary>
    /// 获取当前生效配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前生效配置；null 表示未配置（提供者按未配置处理）</returns>
    Task<EmailOptions?> GetAsync(CancellationToken cancellationToken = default);
}
