#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OAuthOptions
// Guid:a1b2c3d4-5e6f-7890-abcd-ef1234567802
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authentication.OAuth;

/// <summary>
/// OAuth 全局配置
/// </summary>
public class OAuthOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Authentication:OAuth";

    /// <summary>
    /// 是否启用第三方登录
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 三方登录完成后前端回调地址（携带 token 参数）
    /// </summary>
    public string FrontendCallbackUrl { get; set; } = "http://localhost:5888/auth/oauth-callback";

    /// <summary>
    /// 提供商列表
    /// </summary>
    public List<OAuthProviderConfig> Providers { get; set; } = [];
}
