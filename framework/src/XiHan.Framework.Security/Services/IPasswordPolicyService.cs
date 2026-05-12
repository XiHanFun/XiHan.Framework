#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IPasswordPolicyService
// Guid:d5e6f7a8-b9c0-1234-def0-123456789031
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/05/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
    /// <param name="newPasswordHash">新密码哈希</param>
    /// <param name="userId">用户标识</param>
    /// <param name="historyCount">历史记录数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>是否重复使用旧密码</returns>
    Task<bool> IsPasswordReusedAsync(string newPasswordHash, long userId, int historyCount, CancellationToken ct = default);
}
