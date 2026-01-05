#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JwtOptions
// Guid:a7b8c9d0-e1f2-3456-0123-123456789016
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authentication.Jwt;

/// <summary>
/// JWT 配置选项
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// 密钥
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// 发行者
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// 受众
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// 访问令牌过期时间（分钟）
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// 刷新令牌过期时间（天）
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;

    /// <summary>
    /// 是否验证发行者
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;

    /// <summary>
    /// 是否验证受众
    /// </summary>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// 是否验证生命周期
    /// </summary>
    public bool ValidateLifetime { get; set; } = true;

    /// <summary>
    /// 时钟偏移（分钟）
    /// </summary>
    /// <remarks>
    /// 允许的时间偏差，用于处理服务器时间不同步的问题
    /// </remarks>
    public int ClockSkewMinutes { get; set; } = 5;
}
