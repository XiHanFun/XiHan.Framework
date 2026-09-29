// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authentication.Jwt;
using XiHan.Framework.Authentication.Otp;
using XiHan.Framework.Authentication.Users;
using XiHan.Framework.Security.Password;

namespace XiHan.Framework.Authentication.SqlSugar.Tests;

/// <summary>
/// 认证服务在 SqlSugar 用户存储上的端到端流程测试
/// </summary>
public class AuthenticationFlowTests
{
    private const string OldPassword = "Rk4@Wz8&Hs3%";
    private const string NewPassword = "Tq7#Lm2!Vx9$";
    private const string WrongPassword = "Wrong#Pass9x";

    private static readonly PasswordHasher Hasher = CreateHasher(1000);

    /// <summary>
    /// 修改密码后旧密码失效新密码生效
    /// </summary>
    [Fact]
    public async Task 修改密码后旧密码失效新密码生效()
    {
        using var context = new AuthenticationTestContext();
        var userId = await SeedUserAsync(context);

        var changed = await CreateService(context.CreateUserStore()).ChangePasswordAsync(userId, OldPassword, NewPassword);

        Assert.True(changed);
        var withOld = await CreateService(context.CreateUserStore()).AuthenticateAsync("alice", OldPassword);
        var withNew = await CreateService(context.CreateUserStore()).AuthenticateAsync("alice", NewPassword);
        Assert.False(withOld.Succeeded);
        Assert.True(withNew.Succeeded);
    }

    /// <summary>
    /// 启用双因素后恢复码被保存
    /// </summary>
    [Fact]
    public async Task 启用双因素后恢复码被保存()
    {
        using var context = new AuthenticationTestContext();
        var userId = await SeedUserAsync(context);

        var setup = await CreateService(context.CreateUserStore()).EnableTwoFactorAuthenticationAsync(userId);

        var stored = await context.CreateUserStore().GetUserByIdAsync(userId);
        Assert.NotNull(stored);
        Assert.True(stored.TwoFactorEnabled);
        Assert.Equal(setup.Secret, stored.TwoFactorSecret);
        Assert.NotEmpty(stored.RecoveryCodes);
        Assert.Equal(setup.RecoveryCodes.Count, stored.RecoveryCodes.Count);
        Assert.DoesNotContain(setup.RecoveryCodes[0], stored.RecoveryCodes);
    }

    /// <summary>
    /// 恢复码使用一次后失效
    /// </summary>
    [Fact]
    public async Task 恢复码使用一次后失效()
    {
        using var context = new AuthenticationTestContext();
        var userId = await SeedUserAsync(context);
        var setup = await CreateService(context.CreateUserStore()).EnableTwoFactorAuthenticationAsync(userId);
        var code = setup.RecoveryCodes[0];

        var first = await CreateService(context.CreateUserStore()).VerifyRecoveryCodeAsync(userId, code);
        var second = await CreateService(context.CreateUserStore()).VerifyRecoveryCodeAsync(userId, code);

        Assert.True(first);
        Assert.False(second);
    }

    /// <summary>
    /// 登录成功后失败次数清零
    /// </summary>
    [Fact]
    public async Task 登录成功后失败次数清零()
    {
        using var context = new AuthenticationTestContext();
        await SeedUserAsync(context);
        await CreateService(context.CreateUserStore()).AuthenticateAsync("alice", WrongPassword);
        await CreateService(context.CreateUserStore()).AuthenticateAsync("alice", WrongPassword);
        Assert.Equal(2, await context.CreateUserStore().GetFailedLoginAttemptsAsync("alice"));

        var result = await CreateService(context.CreateUserStore()).AuthenticateAsync("alice", OldPassword);

        Assert.True(result.Succeeded);
        var stored = await context.CreateUserStore().GetUserByUsernameAsync("alice");
        Assert.NotNull(stored);
        Assert.Equal(0, stored.FailedLoginAttempts);
        Assert.NotNull(stored.LastLoginTime);
    }

    /// <summary>
    /// 连续失败达到阈值后账户锁定
    /// </summary>
    [Fact]
    public async Task 连续失败达到阈值后账户锁定()
    {
        using var context = new AuthenticationTestContext();
        await SeedUserAsync(context);
        var policy = new PasswordPolicyOptions { MaxFailedAccessAttempts = 3 };

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await CreateService(context.CreateUserStore(), policy: policy).AuthenticateAsync("alice", WrongPassword);
        }

        var stored = await context.CreateUserStore().GetUserByUsernameAsync("alice");
        Assert.NotNull(stored);
        Assert.True(stored.IsLocked);
        Assert.NotNull(stored.LockoutEnd);

        var result = await CreateService(context.CreateUserStore(), policy: policy).AuthenticateAsync("alice", OldPassword);
        Assert.False(result.Succeeded);
        Assert.True(result.IsLockedOut);
    }

    /// <summary>
    /// 登录时重新哈希的密码被保存
    /// </summary>
    [Fact]
    public async Task 登录时重新哈希的密码被保存()
    {
        using var context = new AuthenticationTestContext();
        await SeedUserAsync(context);

        var result = await CreateService(context.CreateUserStore(), CreateHasher(2000)).AuthenticateAsync("alice", OldPassword);

        Assert.True(result.Succeeded);
        var stored = await context.CreateUserStore().GetUserByUsernameAsync("alice");
        Assert.NotNull(stored);
        Assert.StartsWith("1:2000:", stored.PasswordHash);
    }

    private static DefaultAuthenticationService CreateService(
        IUserStore store,
        PasswordHasher? hasher = null,
        PasswordPolicyOptions? policy = null)
    {
        var jwtTokenService = new JwtTokenService(
            Microsoft.Extensions.Options.Options.Create(new JwtOptions
            {
                SecretKey = "xihan-authentication-sqlsugar-tests-secret-key-0123456789",
                Issuer = "xihan-tests",
                Audience = "xihan-tests"
            }),
            new DefaultRefreshTokenStore());

        return new DefaultAuthenticationService(
            store,
            hasher ?? Hasher,
            jwtTokenService,
            new OtpService(Microsoft.Extensions.Options.Options.Create(new OtpOptions())),
            Microsoft.Extensions.Options.Options.Create(policy ?? new PasswordPolicyOptions()));
    }

    private static PasswordHasher CreateHasher(int iterations)
    {
        return new PasswordHasher(Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions
        {
            Iterations = iterations
        }));
    }

    private static async Task<string> SeedUserAsync(AuthenticationTestContext context)
    {
        return await context.CreateUserStore().AddUserAsync(new UserInfo
        {
            Username = "alice",
            PasswordHash = Hasher.HashPassword(OldPassword),
            IsActive = true
        });
    }
}
