// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Security.Password;

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
