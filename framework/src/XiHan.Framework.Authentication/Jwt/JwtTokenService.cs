#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JwtTokenService
// Guid:f6a7b8c9-d0e1-2345-f012-123456789015
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace XiHan.Framework.Authentication.Jwt;

/// <summary>
/// JWT Token 服务实现
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly TokenValidationParameters _tokenValidationParameters;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">JWT 配置选项</param>
    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        _tokenValidationParameters = CreateTokenValidationParameters();
    }

    /// <summary>
    /// 生成访问令牌
    /// </summary>
    /// <param name="claims">声明集合</param>
    /// <returns>JWT Token 结果</returns>
    public JwtTokenResult GenerateAccessToken(List<Claim> claims)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_options.SecretKey);

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenExpirationMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return new JwtTokenResult
        {
            AccessToken = tokenString,
            RefreshToken = GenerateRefreshToken(),
            ExpiresIn = (int)TimeSpan.FromMinutes(_options.AccessTokenExpirationMinutes).TotalSeconds,
            TokenType = "Bearer",
            IssuedAt = now,
            ExpiresAt = expires
        };
    }

    /// <summary>
    /// 生成刷新令牌
    /// </summary>
    /// <returns>刷新令牌</returns>
    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// 验证令牌
    /// </summary>
    /// <param name="token">JWT Token</param>
    /// <returns>声明主体</returns>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, _tokenValidationParameters, out var validatedToken);

            // 确保令牌使用了正确的安全算法
            if (validatedToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 从令牌中提取声明
    /// </summary>
    /// <param name="token">JWT Token</param>
    /// <returns>声明集合</returns>
    public List<Claim>? GetClaimsFromToken(string token)
    {
        var principal = ValidateToken(token);
        return principal?.Claims.ToList();
    }

    /// <summary>
    /// 检查令牌是否过期
    /// </summary>
    /// <param name="token">JWT Token</param>
    /// <returns>是否过期</returns>
    public bool IsTokenExpired(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return true;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            return jwtToken.ValidTo < DateTime.UtcNow;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// 刷新访问令牌
    /// </summary>
    /// <param name="accessToken">原访问令牌</param>
    /// <param name="refreshToken">刷新令牌</param>
    /// <returns>新的 JWT Token 结果</returns>
    public JwtTokenResult? RefreshAccessToken(string accessToken, string refreshToken)
    {
        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
        {
            return null;
        }

        try
        {
            // 即使令牌已过期，也需要提取声明
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = _tokenValidationParameters.Clone();
            validationParameters.ValidateLifetime = false; // 允许过期令牌

            var principal = tokenHandler.ValidateToken(accessToken, validationParameters, out _);

            // 这里应该验证 refreshToken 是否存在于数据库中
            // 实际应用中需要实现 IRefreshTokenStore 接口
            // if (!await _refreshTokenStore.ValidateRefreshToken(refreshToken)) return null;

            // 生成新的访问令牌
            var newToken = GenerateAccessToken([.. principal.Claims]);

            return newToken;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 创建令牌验证参数
    /// </summary>
    /// <returns>令牌验证参数</returns>
    private TokenValidationParameters CreateTokenValidationParameters()
    {
        var key = Encoding.UTF8.GetBytes(_options.SecretKey);

        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = _options.ValidateIssuer,
            ValidIssuer = _options.Issuer,
            ValidateAudience = _options.ValidateAudience,
            ValidAudience = _options.Audience,
            ValidateLifetime = _options.ValidateLifetime,
            ClockSkew = TimeSpan.FromMinutes(_options.ClockSkewMinutes)
        };
    }
}
