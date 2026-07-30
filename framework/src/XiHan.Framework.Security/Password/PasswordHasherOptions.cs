// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;

namespace XiHan.Framework.Security.Password;

/// <summary>
/// 密码哈希配置选项
/// </summary>
public class PasswordHasherOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Authentication:PasswordHasher";

    /// <summary>
    /// 版本号，用于标识哈希算法版本
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 迭代次数，越高越安全但越慢
    /// </summary>
    /// <remarks>
    /// OWASP 推荐 PBKDF2-SHA256 至少 600,000 次迭代
    /// </remarks>
    public int Iterations { get; set; } = 600000;

    /// <summary>
    /// 盐的大小（字节）
    /// </summary>
    public int SaltSize { get; set; } = 32;

    /// <summary>
    /// 哈希的大小（字节）
    /// </summary>
    public int HashSize { get; set; } = 32;

    /// <summary>
    /// 哈希算法
    /// </summary>
    public HashAlgorithmName HashAlgorithm { get; set; } = HashAlgorithmName.SHA256;
}
