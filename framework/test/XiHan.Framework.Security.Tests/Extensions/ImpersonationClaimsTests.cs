// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;
using XiHan.Framework.Security.Claims;
using XiHan.Framework.Security.Extensions;
using XiHan.Framework.Security.Users;

namespace XiHan.Framework.Security.Tests.Extensions;

/// <summary>
/// 模仿者声明读写的测试
/// </summary>
/// <remarks>
/// 覆盖模仿者声明的读写往返：用户标识按 <see cref="long"/> 解析（与 <see cref="ICurrentUser.UserId"/> 同型）、
/// 畸形值解析为空、声明工厂的产出可被读取扩展原样解析回来。
/// </remarks>
public class ImpersonationClaimsTests
{
    /// <summary>
    /// 模仿者用户标识按 long 往返
    /// </summary>
    /// <param name="impersonatorUserId">模仿者用户标识</param>
    [Theory]
    [InlineData(1L)]
    [InlineData(1234567890123456789L)]
    public void FindImpersonatorUserId_LongValue_RoundTrips(long impersonatorUserId)
    {
        var currentUser = new StubCurrentUser(new Claim(XiHanClaimTypes.ImpersonatorUserId, impersonatorUserId.ToString()));

        Assert.Equal(impersonatorUserId, currentUser.FindImpersonatorUserId());
        Assert.True(currentUser.IsImpersonating());
    }

    /// <summary>
    /// 无模仿者声明时判定为非模仿态
    /// </summary>
    [Fact]
    public void IsImpersonating_WithoutClaim_ReturnsFalse()
    {
        var currentUser = new StubCurrentUser();

        Assert.Null(currentUser.FindImpersonatorUserId());
        Assert.False(currentUser.IsImpersonating());
    }

    /// <summary>
    /// 声明值不是合法数字时解析为空，且不抛异常
    /// </summary>
    /// <param name="value">声明值</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    [InlineData("2b1a4d3e-0000-0000-0000-000000000000")]
    public void FindImpersonatorUserId_MalformedValue_ReturnsNull(string value)
    {
        var currentUser = new StubCurrentUser(new Claim(XiHanClaimTypes.ImpersonatorUserId, value));

        Assert.Null(currentUser.FindImpersonatorUserId());
        Assert.False(currentUser.IsImpersonating());
    }

    /// <summary>
    /// 模仿者租户标识与展示名按原值读出
    /// </summary>
    [Fact]
    public void FindImpersonator_TenantAndNames_ReadBackAsWritten()
    {
        var currentUser = new StubCurrentUser(
            new Claim(XiHanClaimTypes.ImpersonatorUserId, "42"),
            new Claim(XiHanClaimTypes.ImpersonatorUserName, "admin"),
            new Claim(XiHanClaimTypes.ImpersonatorTenantId, "7"),
            new Claim(XiHanClaimTypes.ImpersonatorTenantName, "运维租户"));

        Assert.Equal(42L, currentUser.FindImpersonatorUserId());
        Assert.Equal("admin", currentUser.FindImpersonatorUserName());
        Assert.Equal(7L, currentUser.FindImpersonatorTenantId());
        Assert.Equal("运维租户", currentUser.FindImpersonatorTenantName());
    }

    /// <summary>
    /// 声明主体与身份两个重载按 long 解析
    /// </summary>
    [Fact]
    public void FindImpersonatorUserId_PrincipalAndIdentity_ParseAsLong()
    {
        var identity = new ClaimsIdentity([new Claim(XiHanClaimTypes.ImpersonatorUserId, "88")], "test");
        var principal = new ClaimsPrincipal(identity);

        Assert.Equal(88L, principal.FindImpersonatorUserId());
        Assert.Equal(88L, identity.FindImpersonatorUserId());
        Assert.True(principal.IsImpersonating());
    }

    /// <summary>
    /// 声明工厂产出的声明能被读取扩展原样解析回来
    /// </summary>
    [Fact]
    public void BuildImpersonatorClaims_Output_IsReadableByFindExtensions()
    {
        var claims = XiHanClaimsIdentityExtensions.BuildImpersonatorClaims(9527, "ops", 3, "平台");

        var currentUser = new StubCurrentUser([.. claims]);

        Assert.Equal(9527L, currentUser.FindImpersonatorUserId());
        Assert.Equal("ops", currentUser.FindImpersonatorUserName());
        Assert.Equal(3L, currentUser.FindImpersonatorTenantId());
        Assert.Equal("平台", currentUser.FindImpersonatorTenantName());
    }

    /// <summary>
    /// 可选项为空时不产出对应声明，只保留用户标识
    /// </summary>
    [Fact]
    public void BuildImpersonatorClaims_OmitsBlankOptionalValues()
    {
        var claims = XiHanClaimsIdentityExtensions.BuildImpersonatorClaims(9527, "  ", null, null);

        Assert.Single(claims);
        Assert.Equal(XiHanClaimTypes.ImpersonatorUserId, claims[0].Type);
    }

    /// <summary>
    /// 非正的用户标识不产出任何声明
    /// </summary>
    /// <param name="impersonatorUserId">模仿者用户标识</param>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void BuildImpersonatorClaims_NonPositiveUserId_ReturnsEmpty(long impersonatorUserId)
    {
        Assert.Empty(XiHanClaimsIdentityExtensions.BuildImpersonatorClaims(impersonatorUserId, "ops", 3, "平台"));
    }

    /// <summary>
    /// 当前登录主体替身：只回放注入的声明，不依赖 HTTP 上下文
    /// </summary>
    private sealed class StubCurrentUser : ICurrentUser
    {
        private readonly Claim[] _claims;

        public StubCurrentUser(params Claim[] claims)
        {
            _claims = claims;
        }

        public bool IsAuthenticated => true;

        public long? UserId => 1;

        public string? UserName => "target";

        public string? Name => null;

        public string? SurName => null;

        public string? PhoneNumber => null;

        public bool PhoneNumberVerified => false;

        public string? Email => null;

        public bool EmailVerified => false;

        public long? TenantId => null;

        public string[] Roles => [];

        public Claim? FindClaim(string claimType)
        {
            return Array.Find(_claims, claim => claim.Type == claimType);
        }

        public Claim[] FindClaims(string claimType)
        {
            return Array.FindAll(_claims, claim => claim.Type == claimType);
        }

        public Claim[] GetAllClaims()
        {
            return _claims;
        }

        public bool IsInRole(string roleName)
        {
            return false;
        }
    }
}
