#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TimeoutConstants
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5c0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Constants;

/// <summary>
/// 超时相关常量
/// </summary>
public static class TimeoutConstants
{
    /// <summary>
    /// 默认超时时间（秒）
    /// </summary>
    public const int DefaultTimeoutSeconds = 30;

    /// <summary>
    /// 默认重试次数
    /// </summary>
    public const int DefaultRetryCount = 3;

    /// <summary>
    /// 默认重试间隔（毫秒）
    /// </summary>
    public const int DefaultRetryDelayMs = 1000;

    /// <summary>
    /// 默认会话超时时间（分钟）
    /// </summary>
    public const int DefaultSessionTimeoutMinutes = 30;

    /// <summary>
    /// 默认JWT过期时间（小时）
    /// </summary>
    public const int DefaultJwtExpirationHours = 24;

    /// <summary>
    /// 默认刷新令牌过期时间（天）
    /// </summary>
    public const int DefaultRefreshTokenExpirationDays = 7;

    /// <summary>
    /// 默认验证码过期时间（分钟）
    /// </summary>
    public const int DefaultVerificationCodeExpirationMinutes = 5;
}
