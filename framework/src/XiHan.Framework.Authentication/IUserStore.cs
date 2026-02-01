#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IUserStore
// Guid:d6e7f8a9-b0c1-2345-9012-123456789025
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/09 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authentication;

/// <summary>
/// 用户存储接口
/// </summary>
/// <remarks>
/// 用户需要实现此接口来提供用户数据的存储和检索功能
/// </remarks>
public interface IUserStore
{
    /// <summary>
    /// 根据用户名获取用户
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户信息</returns>
    Task<UserInfo?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据用户ID获取用户
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户信息</returns>
    Task<UserInfo?> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新用户信息
    /// </summary>
    /// <param name="user">用户信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateUserAsync(UserInfo user, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新用户密码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="passwordHash">密码哈希</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdatePasswordAsync(string userId, string passwordHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取登录失败次数
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>失败次数</returns>
    Task<int> GetFailedLoginAttemptsAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// 记录登录失败
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task IncrementFailedLoginAttemptsAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重置登录失败次数
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ResetFailedLoginAttemptsAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置账户锁定时间
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="lockoutEnd">锁定结束时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SetLockoutEndAsync(string username, DateTime? lockoutEnd, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取账户锁定结束时间
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>锁定结束时间</returns>
    Task<DateTime?> GetLockoutEndAsync(string username, CancellationToken cancellationToken = default);
}
