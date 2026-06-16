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
    /// 统一头像 Claim 类型：各 provider 注册时把各自头像 JSON 字段映射到此 Claim，
    /// 回调端只读这一个，不依赖各家 handler 默认 Claim 命名（版本无关、provider 无关）。
    /// </summary>
    public const string AvatarClaimType = "urn:xihan:avatar";

    /// <summary>
    /// 是否启用第三方登录
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 三方登录完成后前端回调地址（携带 token 参数）
    /// </summary>
    public string FrontendCallbackUrl { get; set; } = string.Empty;

    /// <summary>
    /// 提供商列表
    /// </summary>
    public List<OAuthProviderConfig> Providers { get; set; } = [];
}
