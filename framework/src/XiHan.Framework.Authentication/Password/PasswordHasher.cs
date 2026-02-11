#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PasswordHasher
// Guid:b2c3d4e5-f6a7-8901-bcde-f12345678901
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Authentication.Password;

/// <summary>
/// 密码哈希服务实现
/// </summary>
/// <remarks>
/// 使用 PBKDF2 (Password-Based Key Derivation Function 2) 算法
/// </remarks>
public class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasherOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">密码哈希配置选项</param>
    public PasswordHasher(IOptions<PasswordHasherOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// 哈希密码
    /// </summary>
    /// <param name="password">原始密码</param>
    /// <returns>哈希后的密码</returns>
    /// <exception cref="ArgumentNullException">密码为空时抛出</exception>
    public string HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        // 生成随机盐
        var salt = RandomNumberGenerator.GetBytes(_options.SaltSize);

        // 使用 PBKDF2 生成哈希
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(password),
            salt: salt,
            iterations: _options.Iterations,
            hashAlgorithm: _options.HashAlgorithm,
            outputLength: _options.HashSize
        );

        // 格式: version:iterations:hashAlgorithm:salt:hash
        return $"{_options.Version}:{_options.Iterations}:{_options.HashAlgorithm}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// 验证密码
    /// </summary>
    /// <param name="hashedPassword">已哈希的密码</param>
    /// <param name="providedPassword">提供的密码</param>
    /// <returns>密码是否匹配</returns>
    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(providedPassword))
        {
            return false;
        }

        try
        {
            var parts = hashedPassword.Split(':');
            if (parts.Length != 5)
            {
                return false;
            }

            var version = int.Parse(parts[0]);
            var iterations = int.Parse(parts[1]);
            var hashAlgorithm = ParseHashAlgorithmName(parts[2]);
            var salt = Convert.FromBase64String(parts[3]);
            var hash = Convert.FromBase64String(parts[4]);

            // 使用相同的参数生成哈希
            var testHash = Rfc2898DeriveBytes.Pbkdf2(
                password: Encoding.UTF8.GetBytes(providedPassword),
                salt: salt,
                iterations: iterations,
                hashAlgorithm: hashAlgorithm,
                outputLength: hash.Length
            );

            // 使用恒定时间比较防止时序攻击
            return CryptographicOperations.FixedTimeEquals(hash, testHash);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查密码是否需要重新哈希
    /// </summary>
    /// <param name="hashedPassword">已哈希的密码</param>
    /// <returns>是否需要重新哈希</returns>
    public bool NeedsRehash(string hashedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword))
        {
            return true;
        }

        try
        {
            var parts = hashedPassword.Split(':');
            if (parts.Length != 5)
            {
                return true;
            }

            var version = int.Parse(parts[0]);
            var iterations = int.Parse(parts[1]);
            var hashAlgorithm = ParseHashAlgorithmName(parts[2]);

            // 如果版本、迭代次数或哈希算法不匹配，需要重新哈希
            return version != _options.Version ||
                   iterations != _options.Iterations ||
                   hashAlgorithm != _options.HashAlgorithm;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// 将存储的算法名称字符串解析为 HashAlgorithmName
    /// </summary>
    private static HashAlgorithmName ParseHashAlgorithmName(string name)
    {
        return name?.ToUpperInvariant() switch
        {
            "SHA1" => HashAlgorithmName.SHA1,
            "SHA256" => HashAlgorithmName.SHA256,
            "SHA384" => HashAlgorithmName.SHA384,
            "SHA512" => HashAlgorithmName.SHA512,
            "MD5" => HashAlgorithmName.MD5,
            _ => HashAlgorithmName.SHA256
        };
    }
}
