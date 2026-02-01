#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OtpService
// Guid:d0e1f2a3-b4c5-6789-3456-123456789019
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using XiHan.Framework.Utils.Security;

namespace XiHan.Framework.Authentication.Otp;

/// <summary>
/// OTP 服务实现
/// </summary>
public class OtpService : IOtpService
{
    private readonly OtpOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">OTP 配置选项</param>
    public OtpService(IOptions<OtpOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// 生成 TOTP 密钥
    /// </summary>
    /// <returns>密钥</returns>
    public string GenerateTotpSecret()
    {
        return OtpHelper.GenerateSecretKey(_options.SecretKeyLength);
    }

    /// <summary>
    /// 生成 TOTP 二维码 URI
    /// </summary>
    /// <param name="secret">密钥</param>
    /// <param name="issuer">发行者</param>
    /// <param name="account">账户名</param>
    /// <returns>二维码 URI</returns>
    public string GenerateTotpUri(string secret, string issuer, string account)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(account);

        // 格式: otpauth://totp/{issuer}:{account}?secret={secret}&issuer={issuer}&digits={digits}&period={period}
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(account);
        var encodedSecret = Uri.EscapeDataString(secret);

        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={encodedSecret}&issuer={encodedIssuer}&digits={_options.Digits}&period={_options.TimeStep}";
    }

    /// <summary>
    /// 生成 TOTP 代码
    /// </summary>
    /// <param name="secret">密钥</param>
    /// <returns>OTP 代码</returns>
    public string GenerateTotpCode(string secret)
    {
        return OtpHelper.GenerateTotp(secret, _options.Digits, _options.TimeStep);
    }

    /// <summary>
    /// 验证 TOTP 代码
    /// </summary>
    /// <param name="secret">密钥</param>
    /// <param name="code">OTP 代码</param>
    /// <returns>是否验证通过</returns>
    public bool VerifyTotpCode(string secret, string code)
    {
        return OtpHelper.VerifyTotp(secret, code, _options.Digits, _options.TimeStep, _options.AllowedSkew);
    }

    /// <summary>
    /// 生成 HOTP 代码
    /// </summary>
    /// <param name="secret">密钥</param>
    /// <param name="counter">计数器</param>
    /// <returns>OTP 代码</returns>
    public string GenerateHotpCode(string secret, long counter)
    {
        return OtpHelper.GenerateHotp(secret, counter, _options.Digits);
    }

    /// <summary>
    /// 验证 HOTP 代码
    /// </summary>
    /// <param name="secret">密钥</param>
    /// <param name="code">OTP 代码</param>
    /// <param name="counter">计数器</param>
    /// <returns>是否验证通过</returns>
    public bool VerifyHotpCode(string secret, string code, long counter)
    {
        return OtpHelper.VerifyHotp(secret, code, counter, _options.Digits);
    }

    /// <summary>
    /// 生成备用恢复码
    /// </summary>
    /// <param name="count">生成数量</param>
    /// <returns>恢复码列表</returns>
    public List<string> GenerateRecoveryCodes(int count = 10)
    {
        var recoveryCodes = new List<string>();

        for (var i = 0; i < count; i++)
        {
            var code = GenerateRecoveryCode();
            recoveryCodes.Add(code);
        }

        return recoveryCodes;
    }

    /// <summary>
    /// 生成单个恢复码
    /// </summary>
    /// <returns>恢复码</returns>
    private static string GenerateRecoveryCode()
    {
        var bytes = new byte[8];
        RandomNumberGenerator.Fill(bytes);
        var code = Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .ToUpperInvariant();

        // 格式化为 XXXX-XXXX 的形式
        return $"{code[..4]}-{code[4..8]}";
    }
}
