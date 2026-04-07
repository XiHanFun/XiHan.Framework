#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExternalLoginInfo
// Guid:a1b2c3d4-5e6f-7890-abcd-ef1234567803
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authentication.OAuth;

/// <summary>
/// 第三方登录用户信息
/// </summary>
public class ExternalLoginInfo
{
    /// <summary>
    /// 提供商名称（google、github、qq 等）
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// 提供商用户唯一标识
    /// </summary>
    public string ProviderKey { get; set; } = string.Empty;

    /// <summary>
    /// 提供商返回的显示名称
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 邮箱
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 头像 URL
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Access Token（提供商颁发的，用于调用提供商 API）
    /// </summary>
    public string? AccessToken { get; set; }
}
