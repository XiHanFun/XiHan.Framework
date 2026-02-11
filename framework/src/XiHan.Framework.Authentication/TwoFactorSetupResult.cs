#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TwoFactorSetupResult
// Guid:c5d6e7f8-a9b0-1234-8901-123456789024
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authentication;

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
