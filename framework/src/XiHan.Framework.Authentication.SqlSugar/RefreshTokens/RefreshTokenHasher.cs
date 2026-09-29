// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;

namespace XiHan.Framework.Authentication.SqlSugar.RefreshTokens;

/// <summary>
/// 刷新令牌哈希
/// </summary>
public static class RefreshTokenHasher
{
    /// <summary>
    /// 计算刷新令牌的 SHA-256 哈希
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <returns>64 位大写十六进制字符串</returns>
    public static string Hash(string refreshToken)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }
}
