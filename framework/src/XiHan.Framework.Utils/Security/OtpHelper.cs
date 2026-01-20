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
using XiHan.Framework.Utils.Converters;
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
    /// <param name="length">密钥字节长度，默认为 20 字节（160 位）。符合 RFC 4226 推荐</param>
    /// <param name="useBase32">是否使用 Base32 编码，默认为 true（兼容 Google Authenticator）。false 则使用 Base64</param>
    /// <returns>Base32 或 Base64 编码的随机密钥字符串</returns>
    public static string GenerateSecretKey(int length = 20, bool useBase32 = true)
    {
        var secretKey = new byte[length];

        // 使用新的随机数生成方法
        RandomNumberGenerator.Fill(secretKey);
        return useBase32 ? Base32.Encode(secretKey) : Convert.ToBase64String(secretKey);
    }

    /// <summary>
    /// 生成 TOTP
    /// </summary>
    /// <param name="secretKey">密钥字符串，默认为 Base32 编码（兼容 Google Authenticator）</param>
    /// <param name="digits">生成的 OTP 位数，默认为 6 位。通常使用 6 位或 8 位</param>
    /// <param name="step">时间步长（秒），默认为 30 秒。表示每 30 秒生成一个新的 OTP</param>
    /// <param name="useBase32">密钥是否为 Base32 编码，默认为 true（兼容 Google Authenticator）。false 则为 Base64</param>
    /// <returns>生成的基于时间的一次性密码</returns>
    /// <exception cref="ArgumentNullException">当 secretKey 为空时抛出</exception>
    public static string GenerateTotp(string secretKey, int digits = 6, int step = 30, bool useBase32 = true)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new ArgumentNullException(nameof(secretKey));
        }

        var counter = GetCurrentCounter(step);
        return GenerateOtp(secretKey, counter, digits, useBase32);
    }

    /// <summary>
    /// 验证 TOTP
    /// </summary>
    /// <param name="secretKey">密钥字符串，默认为 Base32 编码，必须与生成时使用的密钥一致</param>
    /// <param name="otp">待验证的一次性密码</param>
    /// <param name="digits">OTP 位数，默认为 6 位，必须与生成时的位数一致</param>
    /// <param name="step">时间步长（秒），默认为 30 秒，必须与生成时的步长一致</param>
    /// <param name="allowedSkew">允许的时间偏移量，默认为 1。表示允许前后各 1 个时间窗口的 OTP，用于应对时钟偏差</param>
    /// <param name="useBase32">密钥是否为 Base32 编码，默认为 true（兼容 Google Authenticator）。false 则为 Base64</param>
    /// <returns>验证通过返回 true，否则返回 false</returns>
    public static bool VerifyTotp(string secretKey, string otp, int digits = 6, int step = 30, int allowedSkew = 1, bool useBase32 = true)
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
            if (GenerateOtp(secretKey, counter, digits, useBase32) == otp)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 生成 HOTP
    /// </summary>
    /// <param name="secretKey">密钥字符串，默认为 Base32 编码</param>
    /// <param name="counter">计数器值，每次生成 OTP 后应该递增。通常从 0 开始</param>
    /// <param name="digits">生成的 OTP 位数，默认为 6 位。通常使用 6 位或 8 位</param>
    /// <param name="useBase32">密钥是否为 Base32 编码，默认为 true。false 则为 Base64</param>
    /// <returns>生成的基于计数器的一次性密码</returns>
    /// <exception cref="ArgumentNullException">当 secretKey 为空时抛出</exception>
    public static string GenerateHotp(string secretKey, long counter, int digits = 6, bool useBase32 = true)
    {
        return string.IsNullOrWhiteSpace(secretKey)
            ? throw new ArgumentNullException(nameof(secretKey))
            : GenerateOtp(secretKey, counter, digits, useBase32);
    }

    /// <summary>
    /// 验证 HOTP
    /// </summary>
    /// <param name="secretKey">密钥字符串，默认为 Base32 编码，必须与生成时使用的密钥一致</param>
    /// <param name="otp">待验证的一次性密码</param>
    /// <param name="counter">计数器值，必须与生成时使用的计数器值一致</param>
    /// <param name="digits">OTP 位数，默认为 6 位，必须与生成时的位数一致</param>
    /// <param name="useBase32">密钥是否为 Base32 编码，默认为 true。false 则为 Base64</param>
    /// <returns>验证通过返回 true，否则返回 false</returns>
    public static bool VerifyHotp(string secretKey, string otp, long counter, int digits = 6, bool useBase32 = true)
    {
        return !string.IsNullOrWhiteSpace(secretKey) && !string.IsNullOrWhiteSpace(otp) && GenerateOtp(secretKey, counter, digits, useBase32) == otp;
    }

    #region 辅助功能

    /// <summary>
    /// 获取当前 Unix 时间戳（秒）
    /// </summary>
    /// <returns>当前 UTC 时间的 Unix 时间戳（秒）</returns>
    public static long GetCurrentUnixTimeSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    /// <summary>
    /// 获取指定时间的 Unix 时间戳（秒）
    /// </summary>
    /// <param name="dateTime">指定的时间</param>
    /// <returns>指定时间的 Unix 时间戳（秒）</returns>
    public static long GetUnixTimeSeconds(DateTimeOffset dateTime)
    {
        return dateTime.ToUnixTimeSeconds();
    }

    /// <summary>
    /// 获取当前 TOTP 剩余有效秒数
    /// </summary>
    /// <param name="step">时间步长（秒），默认为 30 秒</param>
    /// <returns>当前 OTP 剩余有效秒数</returns>
    public static int GetRemainingSeconds(int step = 30)
    {
        var unixTime = GetCurrentUnixTimeSeconds();
        return step - (int)(unixTime % step);
    }

    /// <summary>
    /// 获取当前时间窗口索引（计数器值）
    /// </summary>
    /// <param name="step">时间步长（秒），默认为 30 秒</param>
    /// <returns>当前时间窗口的计数器值</returns>
    public static long GetCurrentTimeWindow(int step = 30)
    {
        return GetCurrentCounter(step);
    }

    /// <summary>
    /// 获取指定时间的时间窗口索引（计数器值）
    /// </summary>
    /// <param name="dateTime">指定的时间</param>
    /// <param name="step">时间步长（秒），默认为 30 秒</param>
    /// <returns>指定时间的时间窗口计数器值</returns>
    public static long GetTimeWindow(DateTimeOffset dateTime, int step = 30)
    {
        return dateTime.ToUnixTimeSeconds() / step;
    }

    /// <summary>
    /// 获取当前时间窗口的开始时间
    /// </summary>
    /// <param name="step">时间步长（秒），默认为 30 秒</param>
    /// <returns>当前时间窗口的开始时间</returns>
    public static DateTimeOffset GetCurrentWindowStartTime(int step = 30)
    {
        var counter = GetCurrentCounter(step);
        return DateTimeOffset.FromUnixTimeSeconds(counter * step);
    }

    /// <summary>
    /// 获取当前时间窗口的结束时间
    /// </summary>
    /// <param name="step">时间步长（秒），默认为 30 秒</param>
    /// <returns>当前时间窗口的结束时间</returns>
    public static DateTimeOffset GetCurrentWindowEndTime(int step = 30)
    {
        var counter = GetCurrentCounter(step);
        return DateTimeOffset.FromUnixTimeSeconds((counter + 1) * step);
    }

    /// <summary>
    /// 生成 TOTP URI（用于生成二维码，兼容 Google Authenticator）
    /// </summary>
    /// <param name="secretKey">Base32 编码的密钥字符串</param>
    /// <param name="accountName">账户名称（通常是用户邮箱或用户名）</param>
    /// <param name="issuer">发行者名称（应用或服务名称）</param>
    /// <param name="digits">OTP 位数，默认为 6 位</param>
    /// <param name="step">时间步长（秒），默认为 30 秒</param>
    /// <returns>otpauth:// 格式的 URI 字符串</returns>
    /// <exception cref="ArgumentNullException">当必需参数为空时抛出</exception>
    public static string GenerateTotpUri(string secretKey, string accountName, string issuer, int digits = 6, int step = 30)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new ArgumentNullException(nameof(secretKey));
        }

        if (string.IsNullOrWhiteSpace(accountName))
        {
            throw new ArgumentNullException(nameof(accountName));
        }

        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new ArgumentNullException(nameof(issuer));
        }

        // otpauth://totp/issuer:account?secret=SECRET&issuer=ISSUER&digits=6&period=30
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(accountName);
        var label = $"{encodedIssuer}:{encodedAccount}";

        return $"otpauth://totp/{label}?secret={secretKey}&issuer={encodedIssuer}&digits={digits}&period={step}&algorithm=SHA1";
    }

    /// <summary>
    /// 生成 HOTP URI（用于生成二维码）
    /// </summary>
    /// <param name="secretKey">Base32 编码的密钥字符串</param>
    /// <param name="accountName">账户名称（通常是用户邮箱或用户名）</param>
    /// <param name="issuer">发行者名称（应用或服务名称）</param>
    /// <param name="counter">初始计数器值，默认为 0</param>
    /// <param name="digits">OTP 位数，默认为 6 位</param>
    /// <returns>otpauth:// 格式的 URI 字符串</returns>
    /// <exception cref="ArgumentNullException">当必需参数为空时抛出</exception>
    public static string GenerateHotpUri(string secretKey, string accountName, string issuer, long counter = 0, int digits = 6)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new ArgumentNullException(nameof(secretKey));
        }

        if (string.IsNullOrWhiteSpace(accountName))
        {
            throw new ArgumentNullException(nameof(accountName));
        }

        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new ArgumentNullException(nameof(issuer));
        }

        // otpauth://hotp/issuer:account?secret=SECRET&issuer=ISSUER&digits=6&counter=0
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(accountName);
        var label = $"{encodedIssuer}:{encodedAccount}";

        return $"otpauth://hotp/{label}?secret={secretKey}&issuer={encodedIssuer}&digits={digits}&counter={counter}&algorithm=SHA1";
    }

    /// <summary>
    /// 批量生成多个时间窗口的 TOTP（用于显示或调试）
    /// </summary>
    /// <param name="secretKey">密钥字符串，默认为 Base32 编码</param>
    /// <param name="count">生成的数量，默认为 5（当前窗口及前后各 2 个）</param>
    /// <param name="digits">OTP 位数，默认为 6 位</param>
    /// <param name="step">时间步长（秒），默认为 30 秒</param>
    /// <param name="useBase32">密钥是否为 Base32 编码，默认为 true</param>
    /// <returns>时间窗口和对应 OTP 的字典</returns>
    public static Dictionary<long, string> GenerateMultipleTotps(string secretKey, int count = 5, int digits = 6, int step = 30, bool useBase32 = true)
    {
        var currentCounter = GetCurrentCounter(step);
        var result = new Dictionary<long, string>();
        var offset = count / 2;

        for (var i = -offset; i <= offset; i++)
        {
            var counter = currentCounter + i;
            var otp = GenerateOtp(secretKey, counter, digits, useBase32);
            result[counter] = otp;
        }

        return result;
    }

    /// <summary>
    /// 获取 TOTP 的完整信息（包含时间窗口信息）
    /// </summary>
    /// <param name="secretKey">密钥字符串，默认为 Base32 编码</param>
    /// <param name="digits">OTP 位数，默认为 6 位</param>
    /// <param name="step">时间步长（秒），默认为 30 秒</param>
    /// <param name="useBase32">密钥是否为 Base32 编码，默认为 true</param>
    /// <returns>包含 OTP 和时间窗口信息的元组</returns>
    public static (string Otp, long TimeWindow, int RemainingSeconds, DateTimeOffset WindowStart, DateTimeOffset WindowEnd) GetTotpWithInfo(
        string secretKey, int digits = 6, int step = 30, bool useBase32 = true)
    {
        var otp = GenerateTotp(secretKey, digits, step, useBase32);
        var timeWindow = GetCurrentTimeWindow(step);
        var remainingSeconds = GetRemainingSeconds(step);
        var windowStart = GetCurrentWindowStartTime(step);
        var windowEnd = GetCurrentWindowEndTime(step);

        return (otp, timeWindow, remainingSeconds, windowStart, windowEnd);
    }

    #endregion

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
    /// 核心 OTP 生成逻辑（符合 RFC 4226 和 RFC 6238 标准）
    /// </summary>
    /// <param name="secretKey">密钥字符串（Base32 或 Base64 编码）</param>
    /// <param name="counter">计数器值，用于生成 OTP</param>
    /// <param name="digits">生成的 OTP 位数，默认为 6 位。通常使用 6 位或 8 位</param>
    /// <param name="useBase32">密钥是否为 Base32 编码，默认为 true。false 则为 Base64</param>
    /// <returns>生成的基于计数器的一次性密码</returns>
    private static string GenerateOtp(string secretKey, long counter, int digits, bool useBase32 = true)
    {
        // 解码密钥
        var keyBytes = useBase32 ? Base32.Decode(secretKey) : Convert.FromBase64String(secretKey);

        // 将计数器转换为大端字节序（Big-Endian）
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counterBytes);
        }

        // 使用 HMAC-SHA1 计算哈希（符合 RFC 4226/6238 标准）
        var hash = HmacHelper.HmacSha1(keyBytes, counterBytes);

        // 动态截取（Dynamic Truncation）- RFC 4226 第 5.3 节
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
