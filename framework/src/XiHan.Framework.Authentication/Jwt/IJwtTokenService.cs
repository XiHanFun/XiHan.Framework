// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;

namespace XiHan.Framework.Authentication.Jwt;

/// <summary>
/// JWT Token 服务接口
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// 生成访问令牌
    /// </summary>
    /// <param name="claims">声明集合</param>
    /// <returns>JWT Token 结果</returns>
    JwtTokenResult GenerateAccessToken(List<Claim> claims);

    /// <summary>
    /// 生成刷新令牌
    /// </summary>
    /// <returns>刷新令牌</returns>
    string GenerateRefreshToken();

    /// <summary>
    /// 验证令牌
    /// </summary>
    /// <param name="token">JWT Token</param>
    /// <returns>声明主体</returns>
    ClaimsPrincipal? ValidateToken(string token);

    /// <summary>
    /// 从令牌中提取声明
    /// </summary>
    /// <param name="token">JWT Token</param>
    /// <returns>声明集合</returns>
    List<Claim>? GetClaimsFromToken(string token);

    /// <summary>
    /// 检查令牌是否过期
    /// </summary>
    /// <param name="token">JWT Token</param>
    /// <returns>是否过期</returns>
    bool IsTokenExpired(string token);

    /// <summary>
    /// 刷新访问令牌
    /// </summary>
    /// <param name="accessToken">原访问令牌</param>
    /// <param name="refreshToken">刷新令牌</param>
    /// <returns>新的 JWT Token 结果</returns>
    JwtTokenResult? RefreshAccessToken(string accessToken, string refreshToken);
}
