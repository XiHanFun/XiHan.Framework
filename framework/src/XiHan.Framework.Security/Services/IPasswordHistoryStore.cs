#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IPasswordHistoryStore
// Guid:e8f9a0b1-c2d3-4567-e8f9-123456789030
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
