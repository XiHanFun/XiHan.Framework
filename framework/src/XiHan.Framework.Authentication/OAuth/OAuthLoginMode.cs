// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authentication.OAuth;

/// <summary>
/// 第三方登录方式
/// </summary>
/// <remarks>
/// 只影响授权页地址与申请的权限范围，换取令牌与拉取用户信息的接口不随之变化。
/// 仅微信、企业微信、钉钉区分两种方式，其余提供商忽略此项。
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
