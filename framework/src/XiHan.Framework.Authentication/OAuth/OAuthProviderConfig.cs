// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    /// 提供商类型，取值见 <see cref="OAuthProviderNames"/>，留空时取 <see cref="Name"/>
    /// </summary>
    /// <remarks>
    /// 同一提供商要同时提供账号授权与扫码登录时，两条配置用不同的 <see cref="Name"/>
    /// 但填相同的 <see cref="Provider"/>，各自成为独立的 AuthenticationScheme。
    /// </remarks>
    public string? Provider { get; set; }

    /// <summary>
    /// 显示名称
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 登录方式，仅微信、企业微信、飞书、钉钉区分，其余提供商忽略
    /// </summary>
    /// <remarks>飞书两种方式的授权、令牌、用户信息三个接口成套不同，只改 <see cref="AuthorizationEndpoint"/> 换不过去。</remarks>
    public OAuthLoginMode Mode { get; set; } = OAuthLoginMode.QrCode;

    /// <summary>
    /// Client ID
    /// </summary>
    /// <remarks>微信填 AppId，企业微信填 CorpId，钉钉填 AppKey，飞书填 AppId。</remarks>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client Secret
    /// </summary>
    /// <remarks>微信填 AppSecret，企业微信填自建应用 Secret，钉钉填 AppSecret，飞书填 AppSecret。</remarks>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// 企业微信自建应用 AgentId
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// 企业微信是否额外读取通讯录成员资料以取回姓名
    /// </summary>
    /// <remarks>授权链路本身不返回姓名；读取失败时姓名为空，不影响登录。</remarks>
    public bool LoadMemberProfile { get; set; }

    /// <summary>
    /// 钉钉企业 CorpId，随授权请求一并带出
    /// </summary>
    /// <remarks>
    /// 要拿到用户在授权页选定的组织，须在 <see cref="Scopes"/> 里显式加上 <c>corpid</c>：
    /// 钉钉只在权限范围含 <c>corpid</c> 时才会在令牌响应里返回组织标识，
    /// 框架据此写出 <see cref="OAuthClaimTypes.DingTalk.CorpId"/> 声明。只填本项不申请该权限范围拿不到组织。
    /// </remarks>
    public string? CorpId { get; set; }

    /// <summary>
    /// 申请的权限范围，在提供商默认值之外追加
    /// </summary>
    /// <remarks>
    /// 微信与企业微信是例外，按登录方式推导出的权限范围与配置值互斥，配置非空时整体替换。
    /// </remarks>
    public string[] Scopes { get; set; } = [];

    /// <summary>
    /// 覆盖授权页地址，留空时由提供商类型与 <see cref="Mode"/> 推导
    /// </summary>
    public string? AuthorizationEndpoint { get; set; }

    /// <summary>
    /// 追加到授权地址上的额外参数
    /// </summary>
    /// <remarks>
    /// 提供商特有的可选参数走这里，如 Google 的 <c>access_type</c>、钉钉的 <c>org_type</c>。
    /// 不要用它覆盖 <c>client_id</c>、<c>redirect_uri</c>、<c>response_type</c>、<c>scope</c>、<c>state</c>
    /// 这些由处理器自己拼装的参数，也不要覆盖启用 PKCE 时的 <c>code_challenge</c> 与 <c>code_challenge_method</c>：
    /// 走通用授权地址构造的提供商会在发起挑战时抛出重复键异常。
    /// </remarks>
    public Dictionary<string, string> AuthorizationParameters { get; set; } = [];

    /// <summary>
    /// 回调路径（默认使用 /signin-{name}）
    /// </summary>
    public string? CallbackPath { get; set; }

    /// <summary>
    /// 解析提供商类型，留空时回退到 <see cref="Name"/>
    /// </summary>
    /// <returns>小写的提供商类型名</returns>
    public string ResolveProviderType()
    {
        var type = string.IsNullOrWhiteSpace(Provider) ? Name : Provider;
        return type.Trim().ToLowerInvariant();
    }
}
