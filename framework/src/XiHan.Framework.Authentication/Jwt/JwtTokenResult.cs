// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authentication.Jwt;

/// <summary>
/// JWT Token 结果
/// </summary>
public class JwtTokenResult
{
    /// <summary>
    /// 访问令牌
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 刷新令牌
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// 令牌类型
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// 过期时间（秒）
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// 发行时间
    /// </summary>
    public DateTime IssuedAt { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 刷新令牌过期时间
    /// </summary>
    /// <remarks>
    /// 会话级生命周期应以它为准，而不是 <see cref="ExpiresAt"/>（后者是访问令牌的短过期，
    /// 拿它当会话生死线会导致访问令牌一到期整个会话即判死、刷新永不可能成功）。
    /// </remarks>
    public DateTime RefreshTokenExpiresAt { get; set; }
}
