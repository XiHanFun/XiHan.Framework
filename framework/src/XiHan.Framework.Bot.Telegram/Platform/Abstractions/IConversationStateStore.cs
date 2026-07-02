#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IConversationStateStore
// Guid:9d830361-9b96-4e58-82e4-64a0079468be
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Bot.Telegram.Platform.Abstractions;

/// <summary>
/// 会话状态存储（键 = botName:chatId:userId）
/// </summary>
/// <remarks>
/// 默认实现为进程内 TTL 字典；应用层可注册分布式缓存实现覆盖（TryAdd 语义）。
/// </remarks>
public interface IConversationStateStore
{
    /// <summary>
    /// 获取指定会话的当前状态
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">会话 Id</param>
    /// <param name="userId">用户 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话状态；null 表示无活跃状态</returns>
    Task<ConversationState?> GetAsync(string botName, long chatId, long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置指定会话的状态（覆盖已有状态）
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">会话 Id</param>
    /// <param name="userId">用户 Id</param>
    /// <param name="state">会话状态</param>
    /// <param name="ttl">存活时长</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetAsync(string botName, long chatId, long userId, ConversationState state, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清除指定会话的状态
    /// </summary>
    /// <param name="botName">机器人名称</param>
    /// <param name="chatId">会话 Id</param>
    /// <param name="userId">用户 Id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RemoveAsync(string botName, long chatId, long userId, CancellationToken cancellationToken = default);
}
