// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using XiHan.Framework.Security.Password;
using XiHan.Framework.Security.Services;

namespace XiHan.Framework.Security.Tests.Services;

/// <summary>
/// 密码策略服务的测试
/// </summary>
/// <remarks>
/// 覆盖长度、大小写、数字、特殊字符、弱密码黑名单、自定义黑名单、重复与连续字符等规则校验。
/// </remarks>
public class PasswordPolicyServiceTests
{
    /// <summary>
    /// 满足全部策略的密码应通过校验
    /// </summary>
    [Fact]
    public void Validate_ValidPassword_ReturnsValid()
    {
        var service = CreateService();

        var result = service.Validate("XiHan@2024");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal("密码强度良好", result.Message);
        Assert.InRange(result.Score, 1, 100);
    }

    /// <summary>
    /// 长度不足的密码应校验失败
    /// </summary>
    [Fact]
    public void Validate_TooShortPassword_Fails()
    {
        var service = CreateService();

        var result = service.Validate("Ab1!");

        Assert.False(result.IsValid);
        Assert.Contains("密码长度至少需要 8 个字符", result.Errors);
    }

    /// <summary>
    /// 缺少指定复杂度要求的密码应校验失败
    /// </summary>
    /// <param name="password">待校验的密码</param>
    /// <param name="expectedError">预期错误信息</param>
    [Theory]
    [InlineData("lowercase1!", "密码必须包含至少一个大写字母")]
    [InlineData("UPPERCASE1!", "密码必须包含至少一个小写字母")]
    [InlineData("Password!", "密码必须包含至少一个数字")]
    [InlineData("Xihan2024", "密码必须包含至少一个特殊字符")]
    public void Validate_MissingComplexity_Fails(string password, string expectedError)
    {
        var service = CreateService();

        var result = service.Validate(password);

        Assert.False(result.IsValid);
        Assert.Contains(expectedError, result.Errors);
    }

    /// <summary>
    /// 常用弱密码应校验失败
    /// </summary>
    [Fact]
    public void Validate_CommonWeakPassword_Fails()
    {
        var options = new PasswordPolicyOptions
        {
            MinimumLength = 1,
            RequireUppercase = false,
            RequireLowercase = false,
            RequireDigit = false,
            RequireSpecialCharacter = false,
        };
        var service = CreateService(options);

        var result = service.Validate("password");

        Assert.False(result.IsValid);
        Assert.Contains("密码过于常见，请使用更安全的密码", result.Errors);
    }

    /// <summary>
    /// 命中自定义黑名单的密码应校验失败
    /// </summary>
    [Fact]
    public void Validate_CustomBlacklist_Fails()
    {
        var options = new PasswordPolicyOptions { CustomBlacklist = ["qwerty"] };
        var service = CreateService(options);

        var result = service.Validate("Zqwerty!9");

        Assert.False(result.IsValid);
        Assert.Contains("密码包含不允许的词汇", result.Errors);
    }

    /// <summary>
    /// 包含过多重复字符的密码应校验失败
    /// </summary>
    [Fact]
    public void Validate_RepeatingCharacters_Fails()
    {
        var service = CreateService();

        var result = service.Validate("Aaaa!1bc");

        Assert.False(result.IsValid);
        Assert.Contains("密码包含过多重复字符", result.Errors);
    }

    /// <summary>
    /// 包含连续字符序列的密码应校验失败
    /// </summary>
    [Fact]
    public void Validate_SequentialCharacters_Fails()
    {
        var service = CreateService();

        var result = service.Validate("Abc123!");

        Assert.False(result.IsValid);
        Assert.Contains("密码包含连续字符序列", result.Errors);
    }

    /// <summary>
    /// 创建密码策略服务
    /// </summary>
    /// <param name="options">密码策略配置，为空时使用默认配置</param>
    /// <returns>密码策略服务实例</returns>
    private static PasswordPolicyService CreateService(PasswordPolicyOptions? options = null)
    {
        var hasher = new PasswordHasher(Options.Create(new PasswordHasherOptions { Iterations = 1000 }));
        return new PasswordPolicyService(
            Options.Create(options ?? new PasswordPolicyOptions()),
            hasher,
            new DefaultPasswordHistoryStore());
    }
}
