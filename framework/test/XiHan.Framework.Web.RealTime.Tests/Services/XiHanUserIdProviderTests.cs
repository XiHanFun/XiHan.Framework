// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using XiHan.Framework.Web.RealTime.Services;
using XiHan.Framework.Web.RealTime.Tests.Infrastructure;

namespace XiHan.Framework.Web.RealTime.Tests.Services;

/// <summary>
/// 曦寒用户 ID 提供器测试
/// </summary>
/// <remarks>
/// 该提供器决定了 <c>Clients.User(userId)</c> 能不能命中连接，取值口径一旦漂移，
/// 定向推送会静默失效。用例锁死「NameIdentifier 优先、Name 兜底、都没有则为 null」这条优先级。
/// </remarks>
public class XiHanUserIdProviderTests
{
    /// <summary>
    /// 带 NameIdentifier 声明时取该声明值
    /// </summary>
    [Fact]
    public void GetUserId_WhenNameIdentifierPresent_ReturnsNameIdentifier()
    {
        var provider = new XiHanUserIdProvider();
        var connection = HubConnectionContextFactory.Create(TestPrincipals.WithUserId("user-1"));

        Assert.Equal("user-1", provider.GetUserId(connection));
    }

    /// <summary>
    /// 只有 Name 声明时回退到用户名
    /// </summary>
    [Fact]
    public void GetUserId_WhenOnlyNamePresent_FallsBackToName()
    {
        var provider = new XiHanUserIdProvider();
        var connection = HubConnectionContextFactory.Create(TestPrincipals.WithUserName("张三"));

        Assert.Equal("张三", provider.GetUserId(connection));
    }

    /// <summary>
    /// 两个声明同时存在时 NameIdentifier 优先
    /// </summary>
    [Fact]
    public void GetUserId_WhenBothClaimsPresent_PrefersNameIdentifier()
    {
        var provider = new XiHanUserIdProvider();
        var connection = HubConnectionContextFactory.Create(TestPrincipals.WithUserIdAndName("user-1", "张三"));

        Assert.Equal("user-1", provider.GetUserId(connection));
    }

    /// <summary>
    /// 主体存在但没有任何身份声明时返回 null
    /// </summary>
    [Fact]
    public void GetUserId_WhenNoIdentityClaims_ReturnsNull()
    {
        var provider = new XiHanUserIdProvider();
        var connection = HubConnectionContextFactory.Create(TestPrincipals.Anonymous());

        Assert.Null(provider.GetUserId(connection));
    }

    /// <summary>
    /// 匿名连接（没有用户主体）时返回 null
    /// </summary>
    [Fact]
    public void GetUserId_WhenUserAbsent_ReturnsNull()
    {
        var provider = new XiHanUserIdProvider();
        var connection = HubConnectionContextFactory.Create(user: null);

        Assert.Null(provider.GetUserId(connection));
    }

    /// <summary>
    /// 存在多个 NameIdentifier 声明时取第一个
    /// </summary>
    [Fact]
    public void GetUserId_WithMultipleNameIdentifierClaims_ReturnsFirstOne()
    {
        var provider = new XiHanUserIdProvider();
        var principal = TestPrincipals.FromClaims(
            new Claim(ClaimTypes.NameIdentifier, "first"),
            new Claim(ClaimTypes.NameIdentifier, "second"));
        var connection = HubConnectionContextFactory.Create(principal);

        Assert.Equal("first", provider.GetUserId(connection));
    }

    /// <summary>
    /// 声明值为空字符串时按原值返回，不会被当作缺失而回退
    /// </summary>
    /// <remarks>
    /// 实现用的是 <c>??</c> 而不是 <c>string.IsNullOrEmpty</c>，空串是一个真实存在的声明值，这里把该语义固定下来。
    /// </remarks>
    [Fact]
    public void GetUserId_WhenNameIdentifierIsEmptyString_DoesNotFallBackToName()
    {
        var provider = new XiHanUserIdProvider();
        var principal = TestPrincipals.FromClaims(
            new Claim(ClaimTypes.NameIdentifier, string.Empty),
            new Claim(ClaimTypes.Name, "张三"));
        var connection = HubConnectionContextFactory.Create(principal);

        Assert.Equal(string.Empty, provider.GetUserId(connection));
    }

    /// <summary>
    /// 该提供器可以直接顶替 SignalR 的内置用户标识契约
    /// </summary>
    [Fact]
    public void XiHanUserIdProvider_SatisfiesSignalRUserIdProviderContract()
    {
        var provider = new XiHanUserIdProvider();

        Assert.IsAssignableFrom<IXiHanUserIdProvider>(provider);
        Assert.IsAssignableFrom<IUserIdProvider>(provider);
        Assert.True(typeof(IUserIdProvider).IsAssignableFrom(typeof(IXiHanUserIdProvider)));
    }

    /// <summary>
    /// 取值逻辑可被子类覆写以适配自定义声明
    /// </summary>
    [Fact]
    public void GetUserId_IsVirtual_SoDerivedProviderCanOverrideClaimSource()
    {
        var method = typeof(XiHanUserIdProvider).GetMethod(nameof(XiHanUserIdProvider.GetUserId));

        Assert.NotNull(method);
        Assert.True(method.IsVirtual);
        Assert.False(method.IsFinal);
    }
}
