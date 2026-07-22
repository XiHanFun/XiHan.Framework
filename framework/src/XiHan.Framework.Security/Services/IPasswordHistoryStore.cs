// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Security.Services;

/// <summary>
/// 密码历史记录存储接口
/// </summary>
public interface IPasswordHistoryStore
{
    /// <summary>
    /// 获取用户最近的密码哈希列表
    /// </summary>
    /// <param name="userId">用户标识</param>
    /// <param name="count">获取数量</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>密码哈希列表</returns>
    Task<IReadOnlyList<string>> GetRecentPasswordHashesAsync(long userId, int count, CancellationToken ct = default);
}
