// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authentication.OAuth;

/// <summary>
/// 第三方登录方式
/// </summary>
/// <remarks>
/// 仅微信、企业微信、飞书、钉钉区分两种方式，其余提供商忽略此项。
/// 对微信、企业微信、钉钉只影响授权页地址与申请的权限范围；
/// 飞书是例外，两种方式的授权、令牌、用户信息三个接口成套不同，切换时一起替换。
/// </remarks>
public enum OAuthLoginMode
{
    /// <summary>
    /// 扫码登录，跳转提供商二维码页由用户以移动端扫码确认
    /// </summary>
    QrCode = 0,

    /// <summary>
    /// 账号授权，跳转提供商登录页由用户以账号登录并授权
    /// </summary>
    Account = 1
}
