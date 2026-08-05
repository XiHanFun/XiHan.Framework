// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authentication.Oidc;

/// <summary>
/// OpenID Connect 协议常量
/// </summary>
public static class OidcConstants
{
    /// <summary>
    /// 标准端点路径
    /// </summary>
    public static class Endpoints
    {
        /// <summary>
        /// 发现文档
        /// </summary>
        public const string Discovery = "/.well-known/openid-configuration";

        /// <summary>
        /// 签名公钥集
        /// </summary>
        public const string Jwks = "/.well-known/jwks.json";

        /// <summary>
        /// 授权
        /// </summary>
        public const string Authorize = "/connect/authorize";

        /// <summary>
        /// 令牌
        /// </summary>
        public const string Token = "/connect/token";

        /// <summary>
        /// 用户信息
        /// </summary>
        public const string UserInfo = "/connect/userinfo";

        /// <summary>
        /// 令牌撤销
        /// </summary>
        public const string Revocation = "/connect/revoke";
    }

    /// <summary>
    /// 标准 scope
    /// </summary>
    public static class Scopes
    {
        /// <summary>
        /// 启用 OIDC 的必要 scope：请求中不含它则只是普通 OAuth2，不签发 id_token
        /// </summary>
        public const string OpenId = "openid";

        /// <summary>
        /// 基本资料
        /// </summary>
        public const string Profile = "profile";

        /// <summary>
        /// 邮箱
        /// </summary>
        public const string Email = "email";

        /// <summary>
        /// 手机号
        /// </summary>
        public const string Phone = "phone";

        /// <summary>
        /// 请求刷新令牌
        /// </summary>
        public const string OfflineAccess = "offline_access";
    }

    /// <summary>
    /// 标准声明名
    /// </summary>
    public static class Claims
    {
        /// <summary>主体标识</summary>
        public const string Subject = "sub";

        /// <summary>随机串，由客户端提供并原样回填，用于防重放</summary>
        public const string Nonce = "nonce";

        /// <summary>本次认证发生时刻</summary>
        public const string AuthTime = "auth_time";

        /// <summary>访问令牌摘要</summary>
        public const string AccessTokenHash = "at_hash";

        /// <summary>用户名</summary>
        public const string PreferredUserName = "preferred_username";

        /// <summary>显示名</summary>
        public const string Name = "name";

        /// <summary>头像</summary>
        public const string Picture = "picture";

        /// <summary>邮箱</summary>
        public const string Email = "email";

        /// <summary>邮箱是否已验证</summary>
        public const string EmailVerified = "email_verified";

        /// <summary>手机号</summary>
        public const string PhoneNumber = "phone_number";

        /// <summary>手机号是否已验证</summary>
        public const string PhoneNumberVerified = "phone_number_verified";

        /// <summary>资料更新时刻</summary>
        public const string UpdatedAt = "updated_at";
    }
}
