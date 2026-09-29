// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;
using XiHan.Framework.Authentication.Jwt;
using XiHan.Framework.Authentication.SqlSugar.Entities;
using XiHan.Framework.Authentication.SqlSugar.Options;
using XiHan.Framework.Authentication.SqlSugar.RefreshTokens;
using XiHan.Framework.Authentication.SqlSugar.Tests.Fakes;

namespace XiHan.Framework.Authentication.SqlSugar.Tests;

/// <summary>
/// 刷新令牌存储与 JwtTokenService 的轮换集成测试
/// </summary>
public class RefreshTokenRotationTests
{
    /// <summary>
    /// 刷新后旧令牌失效
    /// </summary>
    [Fact]
    public void 刷新后旧令牌失效()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore();
        var jwtTokenService = CreateJwtTokenService(store);
        var issued = jwtTokenService.GenerateAccessToken(CreateClaims("1001"));

        var refreshed = jwtTokenService.RefreshAccessToken(issued.AccessToken, issued.RefreshToken);

        Assert.NotNull(refreshed);
        Assert.NotEqual(issued.RefreshToken, refreshed.RefreshToken);
        Assert.Null(jwtTokenService.RefreshAccessToken(issued.AccessToken, issued.RefreshToken));
    }

    /// <summary>
    /// 重复使用旧令牌使新令牌失效
    /// </summary>
    [Fact]
    public void 重复使用旧令牌使新令牌失效()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore();
        var jwtTokenService = CreateJwtTokenService(store);
        var issued = jwtTokenService.GenerateAccessToken(CreateClaims("1001"));
        var refreshed = jwtTokenService.RefreshAccessToken(issued.AccessToken, issued.RefreshToken);
        Assert.NotNull(refreshed);

        var replay = jwtTokenService.RefreshAccessToken(issued.AccessToken, issued.RefreshToken);

        Assert.Null(replay);
        Assert.False(store.Validate(refreshed.RefreshToken, "1001"));
    }

    /// <summary>
    /// 宽限期内重复使用不影响新令牌
    /// </summary>
    [Fact]
    public void 宽限期内重复使用不影响新令牌()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore(new XiHanAuthenticationSqlSugarOptions
        {
            RefreshTokenReuseGracePeriod = TimeSpan.FromMinutes(1)
        });
        var jwtTokenService = CreateJwtTokenService(store);
        var issued = jwtTokenService.GenerateAccessToken(CreateClaims("1001"));
        var refreshed = jwtTokenService.RefreshAccessToken(issued.AccessToken, issued.RefreshToken);
        Assert.NotNull(refreshed);

        var replay = jwtTokenService.RefreshAccessToken(issued.AccessToken, issued.RefreshToken);

        Assert.Null(replay);
        Assert.True(store.Validate(refreshed.RefreshToken, "1001"));
    }

    /// <summary>
    /// 并发刷新同一令牌只有一方成功
    /// </summary>
    [Fact]
    public void 并发刷新同一令牌只有一方成功()
    {
        using var context = NewContext();
        var racing = new RacingRefreshTokenStore(context.CreateRefreshTokenStore());
        var jwtTokenService = CreateJwtTokenService(racing);
        var issued = jwtTokenService.GenerateAccessToken(CreateClaims("1001"));
        JwtTokenResult? competitor = null;
        racing.BeforeNextSave = () => competitor = jwtTokenService.RefreshAccessToken(issued.AccessToken, issued.RefreshToken);

        var first = jwtTokenService.RefreshAccessToken(issued.AccessToken, issued.RefreshToken);

        Assert.NotNull(competitor);
        Assert.Null(first);
        Assert.True(racing.Validate(competitor.RefreshToken, "1001"));
    }

    /// <summary>
    /// 数据库中没有令牌明文
    /// </summary>
    [Fact]
    public void 数据库中没有令牌明文()
    {
        using var context = NewContext();
        var jwtTokenService = CreateJwtTokenService(context.CreateRefreshTokenStore());

        var issued = jwtTokenService.GenerateAccessToken(CreateClaims("1001"));

        var rows = context.Client.Queryable<SysAuthRefreshToken>().ToList();
        Assert.DoesNotContain(rows, row => row.TokenHash == issued.RefreshToken || row.Subject == issued.RefreshToken);
        Assert.Contains(rows, row => row.TokenHash == RefreshTokenHasher.Hash(issued.RefreshToken) && row.Subject == "1001");
    }

    /// <summary>
    /// 主体不符的访问令牌不能刷新
    /// </summary>
    [Fact]
    public void 主体不符的访问令牌不能刷新()
    {
        using var context = NewContext();
        var store = context.CreateRefreshTokenStore();
        var jwtTokenService = CreateJwtTokenService(store);
        var alice = jwtTokenService.GenerateAccessToken(CreateClaims("1001"));
        var bob = jwtTokenService.GenerateAccessToken(CreateClaims("2002"));

        var result = jwtTokenService.RefreshAccessToken(bob.AccessToken, alice.RefreshToken);

        Assert.Null(result);
        Assert.True(store.Validate(alice.RefreshToken, "1001"));
    }

    private static AuthenticationTestContext NewContext()
    {
        return new AuthenticationTestContext(typeof(SysAuthRefreshToken));
    }

    private static JwtTokenService CreateJwtTokenService(IRefreshTokenStore store)
    {
        return new JwtTokenService(
            Microsoft.Extensions.Options.Options.Create(new JwtOptions
            {
                SecretKey = "xihan-authentication-sqlsugar-tests-secret-key-0123456789",
                Issuer = "xihan-tests",
                Audience = "xihan-tests"
            }),
            store);
    }

    private static List<Claim> CreateClaims(string userId)
    {
        return
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, $"user-{userId}")
        ];
    }
}
