#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OtpHelper
// Guid:86738b17-6280-4f60-a9cd-016a7a691396
// Author:afand
// Email:me@zhaifanhua.com
// CreateTime:2024/12/8 14:28:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Cryptography;
using System.Text;
using XiHan.Framework.Utils.Security.Cryptography;

namespace XiHan.Framework.Utils.Verifications;

/// <summary>
/// OtpHelper
/// </summary>
public static class OtpHelper
{
    /// <summary>
    /// 生成随机的密钥（适用于 TOTP 和 HOTP）
    /// </summary>
    /// <param name="length"></param>
    /// <returns></returns>
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
    /// <param name="secretKey"></param>
    /// <param name="digits"></param>
    /// <param name="step"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
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
    /// <param name="secretKey"></param>
    /// <param name="otp"></param>
    /// <param name="digits"></param>
    /// <param name="step"> </param>
    /// <param name="allowedSkew">允许的时间偏移量</param>
    /// <returns></returns>
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
    /// <param name="secretKey"></param>
    /// <param name="counter"></param>
    /// <param name="digits"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static string GenerateHotp(string secretKey, long counter, int digits = 6)
    {
        return string.IsNullOrWhiteSpace(secretKey)
            ? throw new ArgumentNullException(nameof(secretKey))
            : GenerateOtp(secretKey, counter, digits);
    }

    /// <summary>
    /// 验证 HOTP
    /// </summary>
    /// <param name="secretKey"></param>
    /// <param name="otp"></param>
    /// <param name="counter"></param>
    /// <param name="digits"></param>
    /// <returns></returns>
    public static bool VerifyHotp(string secretKey, string otp, long counter, int digits = 6)
    {
        return !string.IsNullOrWhiteSpace(secretKey) && !string.IsNullOrWhiteSpace(otp) && GenerateOtp(secretKey, counter, digits) == otp;
    }

    /// <summary>
    /// 获取当前时间的计数器（用于 TOTP）
    /// </summary>
    /// <param name="step"></param>
    /// <returns></returns>
    private static long GetCurrentCounter(int step)
    {
        var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return unixTime / step;
    }

    /// <summary>
    /// 核心 OTP 生成逻辑
    /// </summary>
    /// <param name="secretKey"></param>
    /// <param name="counter"></param>
    /// <param name="digits"></param>
    /// <returns></returns>
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
