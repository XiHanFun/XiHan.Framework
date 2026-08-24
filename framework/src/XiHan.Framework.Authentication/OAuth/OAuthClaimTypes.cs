// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authentication.OAuth;

/// <summary>
/// 各提供商的私有声明类型
/// </summary>
/// <remarks>
/// 与既有生态保持一致的 <c>urn:{provider}:{field}</c> 命名。
/// 头像另有一个跨提供商统一的 <see cref="OAuthOptions.AvatarClaimType"/>，回调端只读那一个。
/// </remarks>
public static class OAuthClaimTypes
{
    /// <summary>
    /// Google 声明类型
    /// </summary>
    public static class Google
    {
        /// <summary>个人主页</summary>
        public const string Profile = "urn:google:profile";
    }

    /// <summary>
    /// GitHub 声明类型
    /// </summary>
    public static class GitHub
    {
        /// <summary>昵称</summary>
        public const string Name = "urn:github:name";

        /// <summary>接口地址</summary>
        public const string Url = "urn:github:url";
    }

    /// <summary>
    /// Gitee 声明类型
    /// </summary>
    public static class Gitee
    {
        /// <summary>昵称</summary>
        public const string Name = "urn:gitee:name";

        /// <summary>接口地址</summary>
        public const string Url = "urn:gitee:url";
    }

    /// <summary>
    /// QQ 声明类型
    /// </summary>
    public static class QQ
    {
        /// <summary>统一用户标识</summary>
        public const string UnionId = "urn:qq:unionid";

        /// <summary>QQ 空间头像，30×30</summary>
        public const string PictureUrl = "urn:qq:picture";

        /// <summary>QQ 空间头像，50×50</summary>
        public const string PictureMediumUrl = "urn:qq:picture_medium";

        /// <summary>QQ 空间头像，100×100</summary>
        public const string PictureFullUrl = "urn:qq:picture_full";

        /// <summary>QQ 头像，40×40</summary>
        public const string AvatarUrl = "urn:qq:avatar";

        /// <summary>QQ 头像，100×100</summary>
        public const string AvatarFullUrl = "urn:qq:avatar_full";
    }

    /// <summary>
    /// 微信声明类型
    /// </summary>
    public static class Weixin
    {
        /// <summary>应用内用户标识</summary>
        public const string OpenId = "urn:weixin:openid";

        /// <summary>开放平台统一用户标识</summary>
        public const string UnionId = "urn:weixin:unionid";

        /// <summary>头像</summary>
        public const string HeadImageUrl = "urn:weixin:headimgurl";

        /// <summary>省份</summary>
        public const string Province = "urn:weixin:province";

        /// <summary>城市</summary>
        public const string City = "urn:weixin:city";
    }

    /// <summary>
    /// 企业微信声明类型
    /// </summary>
    public static class WorkWeixin
    {
        /// <summary>非企业成员的应用内用户标识</summary>
        public const string OpenId = "urn:workweixin:openid";

        /// <summary>头像</summary>
        public const string Avatar = "urn:workweixin:avatar";

        /// <summary>手机号</summary>
        public const string Mobile = "urn:workweixin:mobile";
    }

    /// <summary>
    /// 飞书声明类型
    /// </summary>
    public static class Feishu
    {
        /// <summary>应用内用户标识</summary>
        public const string OpenId = "urn:feishu:openid";

        /// <summary>开发者后台统一用户标识</summary>
        public const string UnionId = "urn:feishu:unionid";

        /// <summary>组织内成员标识</summary>
        public const string UserId = "urn:feishu:userid";

        /// <summary>头像</summary>
        public const string Avatar = "urn:feishu:avatar";

        /// <summary>手机号</summary>
        public const string Mobile = "urn:feishu:mobile";
    }

    /// <summary>
    /// 钉钉声明类型
    /// </summary>
    public static class DingTalk
    {
        /// <summary>应用内用户标识</summary>
        public const string OpenId = "urn:dingtalk:openid";

        /// <summary>企业范围内的统一用户标识</summary>
        public const string UnionId = "urn:dingtalk:unionid";

        /// <summary>用户在授权页选定的组织，仅在权限范围含 corpid 时下发</summary>
        public const string CorpId = "urn:dingtalk:corpid";

        /// <summary>头像</summary>
        public const string Avatar = "urn:dingtalk:avatar";

        /// <summary>手机号</summary>
        public const string Mobile = "urn:dingtalk:mobile";
    }
}
