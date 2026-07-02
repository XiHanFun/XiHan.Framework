#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDingTalkConfigStore
// Guid:9a1b0acb-c328-4196-8782-f621c66d193f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.DingTalk;

/// <summary>
/// 钉钉配置存储
/// </summary>
/// <remarks>
/// 默认实现从 IOptionsMonitor 读取；应用层可注册数据库实现覆盖（TryAdd 语义）。
/// </remarks>
public interface IDingTalkConfigStore
{
    /// <summary>
    /// 获取当前生效配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前生效配置；null 表示未配置（提供者按未配置处理）</returns>
    Task<DingTalkOptions?> GetAsync(CancellationToken cancellationToken = default);
}
