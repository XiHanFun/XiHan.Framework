// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authentication.Otp;

/// <summary>
/// OTP 服务接口
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// 生成 TOTP 密钥
    /// </summary>
    /// <returns>密钥</returns>
    string GenerateTotpSecret();

    /// <summary>
    /// 生成 TOTP 二维码 URI
    /// </summary>
    /// <param name="secret">密钥</param>
    /// <param name="issuer">发行者</param>
    /// <param name="account">账户名</param>
    /// <returns>二维码 URI</returns>
    string GenerateTotpUri(string secret, string issuer, string account);

    /// <summary>
    /// 生成 TOTP 代码
    /// </summary>
    /// <param name="secret">密钥</param>
    /// <returns>OTP 代码</returns>
    string GenerateTotpCode(string secret);

    /// <summary>
    /// 验证 TOTP 代码
    /// </summary>
    /// <param name="secret">密钥</param>
    /// <param name="code">OTP 代码</param>
    /// <returns>是否验证通过</returns>
    bool VerifyTotpCode(string secret, string code);

    /// <summary>
    /// 生成 HOTP 代码
    /// </summary>
    /// <param name="secret">密钥</param>
    /// <param name="counter">计数器</param>
    /// <returns>OTP 代码</returns>
    string GenerateHotpCode(string secret, long counter);

    /// <summary>
    /// 验证 HOTP 代码
    /// </summary>
    /// <param name="secret">密钥</param>
    /// <param name="code">OTP 代码</param>
    /// <param name="counter">计数器</param>
    /// <returns>是否验证通过</returns>
    bool VerifyHotpCode(string secret, string code, long counter);

    /// <summary>
    /// 生成备用恢复码
    /// </summary>
    /// <param name="count">生成数量</param>
    /// <returns>恢复码列表</returns>
    List<string> GenerateRecoveryCodes(int count = 10);
}
