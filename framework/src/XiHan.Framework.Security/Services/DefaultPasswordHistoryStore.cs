#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultPasswordHistoryStore
// Guid:f3e4d5c6-b7a8-9012-3456-123456789034
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;

namespace XiHan.Framework.Security.Services;

/// <summary>
/// 默认密码历史记录存储（内存实现）
/// </summary>
/// <remarks>
/// 生产环境中应替换为数据库持久化实现
/// </remarks>
public class DefaultPasswordHistoryStore : IPasswordHistoryStore
{
    private static readonly ConcurrentDictionary<long, ConcurrentQueue<string>> PasswordHistories = new();

    /// <summary>
    /// 获取用户最近的密码哈希列表
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="count">获取数量</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>密码哈希列表</returns>
    public Task<IReadOnlyList<string>> GetRecentPasswordHashesAsync(long userId, int count, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (PasswordHistories.TryGetValue(userId, out var queue))
        {
            var recent = queue.TakeLast(Math.Min(count, queue.Count)).ToList();
            return Task.FromResult<IReadOnlyList<string>>(recent);
        }

        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    /// <summary>
    /// 记录密码历史
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="passwordHash">密码哈希</param>
    /// <param name="maxHistoryCount">最大历史记录数</param>
    public static void RecordPassword(long userId, string passwordHash, int maxHistoryCount = 10)
    {
        var queue = PasswordHistories.GetOrAdd(userId, _ => new ConcurrentQueue<string>());
        queue.Enqueue(passwordHash);

        // 保持历史记录数量在限制之内
        while (queue.Count > maxHistoryCount)
        {
            queue.TryDequeue(out _);
        }
    }
}
