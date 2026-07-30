// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
}
