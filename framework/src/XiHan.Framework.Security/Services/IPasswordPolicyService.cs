// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Security.Password;

namespace XiHan.Framework.Security.Services;

/// <summary>
/// 密码策略服务接口
/// </summary>
public interface IPasswordPolicyService
{
    /// <summary>
    /// 验证密码是否符合策略
    /// </summary>
    /// <param name="password">待验证的密码</param>
    /// <returns>密码验证结果</returns>
    PasswordValidationResult Validate(string password);

    /// <summary>
    /// 检查新密码是否与历史密码重复
    /// </summary>
    /// <remarks>
    /// 入参为新密码<b>明文</b>：历史存储的是 PBKDF2 加盐哈希，同一明文每次哈希结果不同，
    /// 必须用 <c>VerifyPassword(历史哈希, 明文)</c> 逐条比对，不能直接比较哈希字符串。
    /// </remarks>
    /// <param name="newPassword">新密码明文</param>
    /// <param name="userId">用户标识</param>
    /// <param name="historyCount">历史记录数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>是否重复使用旧密码</returns>
    Task<bool> IsPasswordReusedAsync(string newPassword, long userId, int historyCount, CancellationToken ct = default);
}
