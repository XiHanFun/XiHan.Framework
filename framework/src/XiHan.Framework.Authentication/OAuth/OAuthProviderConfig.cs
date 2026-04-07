#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OAuthProviderConfig
// Guid:a1b2c3d4-5e6f-7890-abcd-ef1234567801
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authentication.OAuth;

/// <summary>
/// OAuth 提供商配置
/// </summary>
public class OAuthProviderConfig
{
    /// <summary>
    /// 提供商名称（如 google、github、qq），作为 AuthenticationScheme 名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Client ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client Secret
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// 额外的 Scope（各提供商默认 Scope 之外的补充）
    /// </summary>
    public string[] Scopes { get; set; } = [];

    /// <summary>
    /// 回调路径（默认使用 /signin-{name}）
    /// </summary>
    public string? CallbackPath { get; set; }
}
