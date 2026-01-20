#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OtpHelper
// Guid:86738b17-6280-4f60-a9cd-016a7a691396
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/8 14:28:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;
using System.Text;
using XiHan.Framework.Utils.Security.Cryptography;

namespace XiHan.Framework.Utils.Security;

/// <summary>
/// OtpHelper
/// </summary>
/// <remarks>
/// TOTP 和 HOTP 都是一次性密码（OTP）算法，常用于 2FA / MFA（双因素认证），比如 Google Authenticator、Microsoft Authenticator、Authy 等。
/// HOTP（HMAC-Based One-Time Password），基于 计数器（Counter） 的一次性密码
/// TOTP（Time-Based One-Time Password），基于 时间片（Time Slice） 的一次性密码
/// </remarks>
public static class OtpHelper
{
    /// <summary>
    /// 生成随机的密钥(适用于 TOTP 和 HOTP)
    /// </summary>
    /// <param name="length">密钥字节长度，默认为 32 字节。生成的 Base64 字符串长度约为 (length * 4 / 3)</param>
    /// <returns>Base64 编码的随机密钥字符串</returns>
    public static string GenerateSecretKey(int length = 32)
    {
        var secretKey = new byte[length];

        // 使用新的随机数生成方法
        RandomNumberGenerator.Fill(secretKey);
        return Convert.ToBase64String(secretKey);
    }

    /// <summary>
    /// 生成 TOTP
    /// </summary>
    /// <param name="secretKey">Base64 编码的密钥字符串，用于生成 HMAC-SHA256 哈希</param>
    /// <param name="digits">生成的 OTP 位数，默认为 6 位。通常使用 6 位或 8 位</param>
    /// <param name="step">时间步长（秒），默认为 30 秒。表示每 30 秒生成一个新的 OTP</param>
    /// <returns>生成的基于时间的一次性密码</returns>
    /// <exception cref="ArgumentNullException">当 secretKey 为空时抛出</exception>
    public static string GenerateTotp(string secretKey, int digits = 6, int step = 30)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new ArgumentNullException(nameof(secretKey));
        }

        var counter = GetCurrentCounter(step);
        return GenerateOtp(secretKey, counter, digits);
    }

    /// <summary>
    /// 验证 TOTP
    /// </summary>
    /// <param name="secretKey">Base64 编码的密钥字符串，必须与生成时使用的密钥一致</param>
    /// <param name="otp">待验证的一次性密码</param>
    /// <param name="digits">OTP 位数，默认为 6 位，必须与生成时的位数一致</param>
    /// <param name="step">时间步长（秒），默认为 30 秒，必须与生成时的步长一致</param>
    /// <param name="allowedSkew">允许的时间偏移量，默认为 1。表示允许前后各 1 个时间窗口的 OTP，用于应对时钟偏差</param>
    /// <returns>验证通过返回 true，否则返回 false</returns>
    public static bool VerifyTotp(string secretKey, string otp, int digits = 6, int step = 30, int allowedSkew = 1)
    {
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(otp))
        {
            return false;
        }

        var currentCounter = GetCurrentCounter(step);

        // 允许一定的时间偏移量
        for (var i = -allowedSkew; i <= allowedSkew; i++)
        {
            var counter = currentCounter + i;
            if (GenerateOtp(secretKey, counter, digits) == otp)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 生成 HOTP
    /// </summary>
    /// <param name="secretKey">Base64 编码的密钥字符串，用于生成 HMAC-SHA256 哈希</param>
    /// <param name="counter">计数器值，每次生成 OTP 后应该递增。通常从 0 开始</param>
    /// <param name="digits">生成的 OTP 位数，默认为 6 位。通常使用 6 位或 8 位</param>
    /// <returns>生成的基于计数器的一次性密码</returns>
    /// <exception cref="ArgumentNullException">当 secretKey 为空时抛出</exception>
    public static string GenerateHotp(string secretKey, long counter, int digits = 6)
    {
        return string.IsNullOrWhiteSpace(secretKey)
            ? throw new ArgumentNullException(nameof(secretKey))
            : GenerateOtp(secretKey, counter, digits);
    }

    /// <summary>
    /// 验证 HOTP
    /// </summary>
    /// <param name="secretKey">Base64 编码的密钥字符串，必须与生成时使用的密钥一致</param>
    /// <param name="otp">待验证的一次性密码</param>
    /// <param name="counter">计数器值，必须与生成时使用的计数器值一致</param>
    /// <param name="digits">OTP 位数，默认为 6 位，必须与生成时的位数一致</param>
    /// <returns>验证通过返回 true，否则返回 false</returns>
    public static bool VerifyHotp(string secretKey, string otp, long counter, int digits = 6)
    {
        return !string.IsNullOrWhiteSpace(secretKey) && !string.IsNullOrWhiteSpace(otp) && GenerateOtp(secretKey, counter, digits) == otp;
    }

    /// <summary>
    /// 获取当前时间的计数器(用于 TOTP)
    /// </summary>
    /// <param name="step">时间步长（秒），用于将当前 Unix 时间戳转换为计数器值</param>
    /// <returns>当前时间对应的计数器值</returns>
    private static long GetCurrentCounter(int step)
    {
        var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return unixTime / step;
    }

    /// <summary>
    /// 核心 OTP 生成逻辑
    /// </summary>
    /// <param name="secretKey">Base64 编码的密钥字符串</param>
    /// <param name="counter">计数器值，用于 HOTP 时为实际计数器，用于 TOTP 时为时间戳除以步长</param>
    /// <param name="digits">生成的 OTP 位数</param>
    /// <returns>生成的一次性密码，使用 0 左填充到指定位数</returns>
    private static string GenerateOtp(string secretKey, long counter, int digits)
    {
        var counterBytes = BitConverter.GetBytes(counter);

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counterBytes);
        }

        var hash = HmacHelper.HmacSha256(secretKey, Encoding.UTF8.GetString(counterBytes));

        // 动态截取
        var offset = hash[^1] & 0x0F;
        var binaryCode = ((hash[offset] & 0x7F) << 24) |
            (hash[offset + 1] << 16) |
            (hash[offset + 2] << 8) |
            hash[offset + 3];

        // 取模以生成固定位数的 OTP
        var otp = binaryCode % (int)Math.Pow(10, digits);

        // 用 0 填充左侧，确保返回值的位数正确
        return otp.ToString().PadLeft(digits, '0');
    }
}
