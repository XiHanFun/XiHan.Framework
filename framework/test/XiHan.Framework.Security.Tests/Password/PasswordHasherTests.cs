// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using XiHan.Framework.Security.Password;

namespace XiHan.Framework.Security.Tests.Password;

/// <summary>
/// 密码哈希服务的测试
/// </summary>
/// <remarks>
/// 覆盖 PBKDF2 哈希与校验往返、错误密码、随机盐、空值/畸形哈希以及重新哈希判定与自定义选项。
/// </remarks>
public class PasswordHasherTests
{
    /// <summary>
    /// 哈希后使用相同密码校验应通过
    /// </summary>
    /// <param name="password">原始密码</param>
    [Theory]
    [InlineData("P@ssw0rd")]
    [InlineData("密码 P@ss 123")]
    [InlineData("!@#$%^&*()_+")]
    public void HashPassword_VerifyPassword_RoundTrips(string password)
    {
        var hasher = CreateHasher();

        var hash = hasher.HashPassword(password);

        Assert.True(hasher.VerifyPassword(hash, password));
    }

    /// <summary>
    /// 使用错误密码校验应失败
    /// </summary>
    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var hasher = CreateHasher();
        var hash = hasher.HashPassword("correct-password");

        Assert.False(hasher.VerifyPassword(hash, "wrong-password"));
    }

    /// <summary>
    /// 相同密码两次哈希应产生不同结果（随机盐）
    /// </summary>
    [Fact]
    public void HashPassword_SamePassword_Twice_ProducesDifferentHashes()
    {
        var hasher = CreateHasher();

        var first = hasher.HashPassword("same-password");
        var second = hasher.HashPassword("same-password");

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// 传入空密码哈希时应抛出异常
    /// </summary>
    [Fact]
    public void HashPassword_NullPassword_Throws()
    {
        var hasher = CreateHasher();

        Assert.Throws<ArgumentNullException>(() => hasher.HashPassword(null!));
    }

    /// <summary>
    /// 空值或畸形哈希在校验时应返回失败
    /// </summary>
    /// <param name="hashedPassword">已哈希的密码</param>
    /// <param name="providedPassword">提供的密码</param>
    [Theory]
    [InlineData("", "anything")]
    [InlineData("any-hash", "")]
    [InlineData("not-a-hash", "secret")]
    [InlineData("1:2:3:4", "secret")]
    public void VerifyPassword_EmptyOrMalformedHash_ReturnsFalse(string hashedPassword, string providedPassword)
    {
        var hasher = CreateHasher();

        Assert.False(hasher.VerifyPassword(hashedPassword, providedPassword));
    }

    /// <summary>
    /// 使用当前选项生成的哈希无需重新哈希
    /// </summary>
    [Fact]
    public void NeedsRehash_WithCurrentOptions_ReturnsFalse()
    {
        var hasher = CreateHasher();
        var hash = hasher.HashPassword("secret");

        Assert.False(hasher.NeedsRehash(hash));
    }

    /// <summary>
    /// 迭代次数变更后需要重新哈希
    /// </summary>
    [Fact]
    public void NeedsRehash_WithDifferentIterations_ReturnsTrue()
    {
        var hash = CreateHasher(new PasswordHasherOptions { Iterations = 1000 }).HashPassword("secret");
        var hasher = CreateHasher(new PasswordHasherOptions { Iterations = 2000 });

        Assert.True(hasher.NeedsRehash(hash));
    }

    /// <summary>
    /// 版本号变更后需要重新哈希
    /// </summary>
    [Fact]
    public void NeedsRehash_WithDifferentVersion_ReturnsTrue()
    {
        var hash = CreateHasher(new PasswordHasherOptions { Version = 1, Iterations = 1000 }).HashPassword("secret");
        var hasher = CreateHasher(new PasswordHasherOptions { Version = 2, Iterations = 1000 });

        Assert.True(hasher.NeedsRehash(hash));
    }

    /// <summary>
    /// 空值或畸形哈希在判定重新哈希时应返回需要
    /// </summary>
    /// <param name="hash">待判定的哈希</param>
    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("1:2:3:4")]
    public void NeedsRehash_MalformedHash_ReturnsTrue(string hash)
    {
        var hasher = CreateHasher();

        Assert.True(hasher.NeedsRehash(hash));
    }

    /// <summary>
    /// 自定义选项应体现在哈希格式（版本、迭代、算法、盐与哈希长度）
    /// </summary>
    [Fact]
    public void CustomOptions_ProduceExpectedHashFormat()
    {
        var options = new PasswordHasherOptions
        {
            Version = 7,
            Iterations = 1234,
            SaltSize = 16,
            HashSize = 24,
            HashAlgorithm = HashAlgorithmName.SHA512,
        };
        var hasher = CreateHasher(options);

        var hash = hasher.HashPassword("secret");
        var parts = hash.Split(':');

        Assert.Equal(5, parts.Length);
        Assert.Equal("7", parts[0]);
        Assert.Equal("1234", parts[1]);
        Assert.Equal("SHA512", parts[2]);
        Assert.Equal(16, Convert.FromBase64String(parts[3]).Length);
        Assert.Equal(24, Convert.FromBase64String(parts[4]).Length);
    }

    /// <summary>
    /// 创建密码哈希服务
    /// </summary>
    /// <param name="options">密码哈希配置，为空时使用低迭代次数的默认配置</param>
    /// <returns>密码哈希服务实例</returns>
    private static PasswordHasher CreateHasher(PasswordHasherOptions? options = null)
    {
        return new PasswordHasher(Options.Create(options ?? new PasswordHasherOptions { Iterations = 1000 }));
    }
}
