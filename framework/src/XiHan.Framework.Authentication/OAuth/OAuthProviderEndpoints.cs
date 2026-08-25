// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authentication.OAuth;

/// <summary>
/// 各提供商的接口地址
/// </summary>
/// <remarks>
/// 集中登记便于核对实际调用了谁家的哪个接口；区分登录方式的提供商，扫码与账号授权各列一条授权页地址。
/// </remarks>
public static class OAuthProviderEndpoints
{
    /// <summary>
    /// 微信系授权页要求携带的锚点片段
    /// </summary>
    public const string WeixinRedirectFragment = "#wechat_redirect";

    /// <summary>
    /// Google 接口地址
    /// </summary>
    public static class Google
    {
        /// <summary>授权页</summary>
        public const string Authorization = "https://accounts.google.com/o/oauth2/v2/auth";

        /// <summary>令牌接口</summary>
        public const string Token = "https://oauth2.googleapis.com/token";

        /// <summary>用户信息接口</summary>
        public const string UserInformation = "https://www.googleapis.com/oauth2/v3/userinfo";
    }

    /// <summary>
    /// GitHub 接口地址
    /// </summary>
    public static class GitHub
    {
        /// <summary>授权页</summary>
        public const string Authorization = "https://github.com/login/oauth/authorize";

        /// <summary>令牌接口</summary>
        public const string Token = "https://github.com/login/oauth/access_token";

        /// <summary>用户信息接口</summary>
        public const string UserInformation = "https://api.github.com/user";

        /// <summary>邮箱列表接口</summary>
        public const string UserEmails = "https://api.github.com/user/emails";
    }

    /// <summary>
    /// Gitee 接口地址
    /// </summary>
    public static class Gitee
    {
        /// <summary>授权页</summary>
        public const string Authorization = "https://gitee.com/oauth/authorize";

        /// <summary>令牌接口</summary>
        public const string Token = "https://gitee.com/oauth/token";

        /// <summary>用户信息接口</summary>
        public const string UserInformation = "https://gitee.com/api/v5/user";

        /// <summary>邮箱列表接口</summary>
        public const string UserEmails = "https://gitee.com/api/v5/emails";
    }

    /// <summary>
    /// QQ 接口地址
    /// </summary>
    public static class QQ
    {
        /// <summary>授权页</summary>
        public const string Authorization = "https://graph.qq.com/oauth2.0/authorize";

        /// <summary>令牌接口</summary>
        public const string Token = "https://graph.qq.com/oauth2.0/token";

        /// <summary>用户标识接口</summary>
        public const string UserIdentification = "https://graph.qq.com/oauth2.0/me";

        /// <summary>用户信息接口</summary>
        public const string UserInformation = "https://graph.qq.com/user/get_user_info";
    }

    /// <summary>
    /// 微信接口地址
    /// </summary>
    public static class Weixin
    {
        /// <summary>扫码登录授权页，开放平台网站应用</summary>
        public const string QrCodeAuthorization = "https://open.weixin.qq.com/connect/qrconnect";

        /// <summary>账号授权页，公众号网页授权</summary>
        public const string AccountAuthorization = "https://open.weixin.qq.com/connect/oauth2/authorize";

        /// <summary>令牌接口</summary>
        public const string Token = "https://api.weixin.qq.com/sns/oauth2/access_token";

        /// <summary>用户信息接口</summary>
        public const string UserInformation = "https://api.weixin.qq.com/sns/userinfo";

        /// <summary>扫码登录申请的权限范围</summary>
        public const string QrCodeScope = "snsapi_login";

        /// <summary>账号授权申请的权限范围</summary>
        public const string AccountScope = "snsapi_userinfo";
    }

    /// <summary>
    /// 企业微信接口地址
    /// </summary>
    public static class WorkWeixin
    {
        /// <summary>扫码登录授权页</summary>
        public const string QrCodeAuthorization = "https://login.work.weixin.qq.com/wwlogin/sso/login";

        /// <summary>账号授权页，应用内网页授权</summary>
        public const string AccountAuthorization = "https://open.weixin.qq.com/connect/oauth2/authorize";

        /// <summary>企业凭证接口</summary>
        public const string Token = "https://qyapi.weixin.qq.com/cgi-bin/gettoken";

        /// <summary>成员标识接口</summary>
        public const string UserIdentification = "https://qyapi.weixin.qq.com/cgi-bin/auth/getuserinfo";

        /// <summary>成员敏感信息接口</summary>
        public const string UserDetail = "https://qyapi.weixin.qq.com/cgi-bin/auth/getuserdetail";

        /// <summary>通讯录成员读取接口</summary>
        public const string Member = "https://qyapi.weixin.qq.com/cgi-bin/user/get";

        /// <summary>账号授权申请的权限范围</summary>
        public const string AccountScope = "snsapi_privateinfo";
    }

    /// <summary>
    /// 飞书接口地址
    /// </summary>
    public static class Feishu
    {
        /// <summary>扫码登录授权页，供网页二维码 SDK 内嵌</summary>
        public const string QrCodeAuthorization = "https://passport.feishu.cn/suite/passport/oauth/authorize";

        /// <summary>扫码登录令牌接口</summary>
        public const string QrCodeToken = "https://passport.feishu.cn/suite/passport/oauth/token";

        /// <summary>扫码登录用户信息接口</summary>
        public const string QrCodeUserInformation = "https://passport.feishu.cn/suite/passport/oauth/userinfo";

        /// <summary>账号授权页</summary>
        public const string AccountAuthorization = "https://accounts.feishu.cn/open-apis/authen/v1/authorize";

        /// <summary>账号授权令牌接口</summary>
        public const string AccountToken = "https://open.feishu.cn/open-apis/authen/v2/oauth/token";

        /// <summary>账号授权用户信息接口</summary>
        public const string AccountUserInformation = "https://open.feishu.cn/open-apis/authen/v1/user_info";
    }

    /// <summary>
    /// 钉钉接口地址
    /// </summary>
    public static class DingTalk
    {
        /// <summary>扫码登录授权页</summary>
        public const string QrCodeAuthorization = "https://login.dingtalk.com/oauth2/challenge.htm";

        /// <summary>账号授权页</summary>
        public const string AccountAuthorization = "https://login.dingtalk.com/oauth2/auth";

        /// <summary>用户令牌接口</summary>
        public const string Token = "https://api.dingtalk.com/v1.0/oauth2/userAccessToken";

        /// <summary>用户信息接口</summary>
        public const string UserInformation = "https://api.dingtalk.com/v1.0/contact/users/me";

        /// <summary>用户信息接口读取令牌的请求头名称</summary>
        public const string AccessTokenHeaderName = "x-acs-dingtalk-access-token";

        /// <summary>默认权限范围</summary>
        public const string Scope = "openid";
    }
}
