#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IRefreshTokenStore
// Guid:3b2a4f91-3d9e-4bc8-9a8f-1b61a7a5f2d1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 14:10:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authentication.Jwt;

/// <summary>
/// 刷新令牌存储接口
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>
    /// 保存刷新令牌
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <param name="subject">主体标识（通常为用户ID）</param>
    /// <param name="expiresAt">过期时间（UTC）</param>
    void Save(string refreshToken, string? subject, DateTime expiresAt);

    /// <summary>
    /// 校验刷新令牌
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <param name="subject">可选主体标识，用于绑定校验</param>
    /// <returns>是否有效</returns>
    bool Validate(string refreshToken, string? subject = null);

    /// <summary>
    /// 移除刷新令牌
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    void Remove(string refreshToken);
}
