// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using XiHan.Framework.Authorization.AspNetCore;

namespace XiHan.Framework.Authorization.Tests.AspNetCore;

/// <summary>
/// 混合权限策略提供器测试
/// </summary>
/// <remarks>
/// 提供器是策略名字符串协议的解码端，与授权特性的编码端构成一次完整往返。
/// 这里的重点是往返保真（含分号、等号、空格、中文等会撑破协议的字符）、
/// 无法解析时必须原样退回默认提供器，以及“p 与 a 全为空”这种应当判定为非本协议的边界。
/// </remarks>
public class HybridPermissionPolicyProviderTests
{
    /// <summary>
    /// 权限特性生成的策略名能被解码回原始的权限编码与策略编码
    /// </summary>
    /// <param name="permissionCode">权限编码</param>
    /// <param name="abacPolicyCode">ABAC 策略编码</param>
    [Theory]
    [InlineData("Sys.User.Create", "same_tenant")]
    [InlineData("Sys:User:Create", "self_only")]
    [InlineData("a;b=c", "x=y;z")]
    [InlineData("has space", "policy with space")]
    [InlineData("权限.创建", "subject.tenant_id == resource.tenant_id")]
    [InlineData("p%3D", "a%3B")]
    public async Task GetPolicyAsync_RoundTripsPermissionAuthorizeAttribute(string permissionCode, string abacPolicyCode)
    {
        var attribute = new PermissionAuthorizeAttribute(permissionCode, abacPolicyCode);

        var requirement = await ResolveRequirementAsync(attribute.Policy!);

        Assert.Equal(permissionCode, requirement.PermissionCode);
        Assert.Equal(abacPolicyCode, requirement.AbacPolicyCode);
    }

    /// <summary>
    /// ABAC 特性生成的策略名解码后权限编码为空、策略编码保真
    /// </summary>
    [Fact]
    public async Task GetPolicyAsync_RoundTripsAbacAuthorizeAttribute()
    {
        var attribute = new AbacAuthorizeAttribute("subject.user_id == resource.owner_user_id");

        var requirement = await ResolveRequirementAsync(attribute.Policy!);

        Assert.Equal(string.Empty, requirement.PermissionCode);
        Assert.Equal("subject.user_id == resource.owner_user_id", requirement.AbacPolicyCode);
    }

    /// <summary>
    /// 只配权限、不配 ABAC 时解码出的策略编码为空串
    /// </summary>
    [Fact]
    public async Task GetPolicyAsync_WithPermissionOnly_LeavesAbacEmpty()
    {
        var attribute = new PermissionAuthorizeAttribute("Sys.User.Create");

        var requirement = await ResolveRequirementAsync(attribute.Policy!);

        Assert.Equal("Sys.User.Create", requirement.PermissionCode);
        Assert.Equal(string.Empty, requirement.AbacPolicyCode);
    }

    /// <summary>
    /// 构建出的策略只包含一条混合权限要求
    /// </summary>
    [Fact]
    public async Task GetPolicyAsync_BuildsPolicyWithSingleRequirement()
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync("xihan.hybrid:p=read;a=allow");

        Assert.NotNull(policy);
        Assert.Single(policy!.Requirements);
        Assert.IsType<HybridPermissionRequirement>(Assert.Single(policy.Requirements));
    }

    /// <summary>
    /// 策略名前缀的大小写不敏感
    /// </summary>
    [Fact]
    public async Task GetPolicyAsync_PrefixIsCaseInsensitive()
    {
        var requirement = await ResolveRequirementAsync("XIHAN.HYBRID:p=read;a=");

        Assert.Equal("read", requirement.PermissionCode);
    }

    /// <summary>
    /// 段的顺序不影响解析，未知段被忽略
    /// </summary>
    [Fact]
    public async Task GetPolicyAsync_IgnoresUnknownSegmentsAndOrder()
    {
        var requirement = await ResolveRequirementAsync("xihan.hybrid:x=1;a=allow;p=read");

        Assert.Equal("read", requirement.PermissionCode);
        Assert.Equal("allow", requirement.AbacPolicyCode);
    }

    /// <summary>
    /// 不是本协议的策略名交回默认提供器，未注册则返回空
    /// </summary>
    [Fact]
    public async Task GetPolicyAsync_WithForeignName_FallsBackAndReturnsNull()
    {
        var provider = CreateProvider();

        Assert.Null(await provider.GetPolicyAsync("some-other-policy"));
    }

    /// <summary>
    /// 带前缀但两段都为空时不认作本协议，回退到默认提供器
    /// </summary>
    /// <param name="policyName">策略名称</param>
    [Theory]
    [InlineData("xihan.hybrid:")]
    [InlineData("xihan.hybrid:p=;a=")]
    [InlineData("xihan.hybrid:   ")]
    public async Task GetPolicyAsync_WithEmptyPayload_FallsBack(string policyName)
    {
        var provider = CreateProvider();

        Assert.Null(await provider.GetPolicyAsync(policyName));
    }

    /// <summary>
    /// 默认提供器里已注册的策略仍然可以取到
    /// </summary>
    [Fact]
    public async Task GetPolicyAsync_WithRegisteredName_DelegatesToFallback()
    {
        var options = new AuthorizationOptions();
        options.AddPolicy("admin-only", builder => builder.RequireRole("admin"));
        var provider = new HybridPermissionPolicyProvider(Options.Create(options));

        var policy = await provider.GetPolicyAsync("admin-only");

        Assert.NotNull(policy);
        Assert.NotEmpty(policy!.Requirements);
    }

    /// <summary>
    /// 默认策略委托给默认提供器，必须有要求项（默认是“必须已认证”）
    /// </summary>
    [Fact]
    public async Task GetDefaultPolicyAsync_DelegatesToFallback()
    {
        var provider = CreateProvider();

        var policy = await provider.GetDefaultPolicyAsync();

        Assert.NotNull(policy);
        Assert.NotEmpty(policy.Requirements);
    }

    /// <summary>
    /// 未配置兜底策略时返回空，而不是自造一个
    /// </summary>
    [Fact]
    public async Task GetFallbackPolicyAsync_WithoutConfiguration_ReturnsNull()
    {
        var provider = CreateProvider();

        Assert.Null(await provider.GetFallbackPolicyAsync());
    }

    private static HybridPermissionPolicyProvider CreateProvider()
    {
        return new HybridPermissionPolicyProvider(Options.Create(new AuthorizationOptions()));
    }

    private static async Task<HybridPermissionRequirement> ResolveRequirementAsync(string policyName)
    {
        var policy = await CreateProvider().GetPolicyAsync(policyName);

        Assert.NotNull(policy);
        return Assert.Single(policy!.Requirements.OfType<HybridPermissionRequirement>());
    }
}
