// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authentication.OAuth;

/// <summary>
/// 内置第三方登录提供商类型名称
/// </summary>
/// <remarks>
/// 取值填在 <see cref="OAuthProviderConfig.Provider"/>，大小写不敏感；
/// 留空时回退到 <see cref="OAuthProviderConfig.Name"/>。
/// </remarks>
public static class OAuthProviderNames
{
    /// <summary>
    /// Google
    /// </summary>
    public const string Google = "google";

    /// <summary>
    /// GitHub
    /// </summary>
    public const string GitHub = "github";

    /// <summary>
    /// Gitee
    /// </summary>
    public const string Gitee = "gitee";

    /// <summary>
    /// QQ
    /// </summary>
    public const string QQ = "qq";

    /// <summary>
    /// 微信
    /// </summary>
    public const string Weixin = "weixin";

    /// <summary>
    /// 微信，别名
    /// </summary>
    public const string WeChat = "wechat";

    /// <summary>
    /// 企业微信
    /// </summary>
    public const string WorkWeixin = "workweixin";

    /// <summary>
    /// 企业微信，别名
    /// </summary>
    public const string WeCom = "wecom";

    /// <summary>
    /// 飞书
    /// </summary>
    public const string Feishu = "feishu";

    /// <summary>
    /// 飞书，别名
    /// </summary>
    public const string Lark = "lark";

    /// <summary>
    /// 钉钉
    /// </summary>
    public const string DingTalk = "dingtalk";
}
