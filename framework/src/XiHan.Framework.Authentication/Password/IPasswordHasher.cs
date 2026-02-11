#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IPasswordHasher
// Guid:a1b2c3d4-e5f6-7890-abcd-ef1234567890
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authentication.Password;

/// <summary>
/// 密码哈希接口
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// 哈希密码
    /// </summary>
    /// <param name="password">原始密码</param>
    /// <returns>哈希后的密码</returns>
    string HashPassword(string password);

    /// <summary>
    /// 验证密码
    /// </summary>
    /// <param name="hashedPassword">已哈希的密码</param>
    /// <param name="providedPassword">提供的密码</param>
    /// <returns>密码是否匹配</returns>
    bool VerifyPassword(string hashedPassword, string providedPassword);

    /// <summary>
    /// 检查密码是否需要重新哈希
    /// </summary>
    /// <param name="hashedPassword">已哈希的密码</param>
    /// <returns>是否需要重新哈希</returns>
    bool NeedsRehash(string hashedPassword);
}
