// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authentication.Otp;

/// <summary>
/// 双因素认证设置结果
/// </summary>
public class TwoFactorSetupResult
{
    /// <summary>
    /// TOTP 密钥
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// 二维码 URI
    /// </summary>
    public string QrCodeUri { get; set; } = string.Empty;

    /// <summary>
    /// 备用恢复码
    /// </summary>
    public List<string> RecoveryCodes { get; set; } = [];

    /// <summary>
    /// 手动输入密钥（用于无法扫描二维码的情况）
    /// </summary>
    public string ManualEntryKey { get; set; } = string.Empty;
}
