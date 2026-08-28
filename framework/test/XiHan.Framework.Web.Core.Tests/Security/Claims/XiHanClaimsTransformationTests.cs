// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using XiHan.Framework.Security.Claims;
using XiHan.Framework.Web.Core.Security.Claims;

namespace XiHan.Framework.Web.Core.Tests.Security.Claims;

/// <summary>
/// 曦寒声明转换测试
/// </summary>
/// <remarks>
/// 这是认证管线上唯一一处改写用户身份的地方，属于安全边界，覆盖四类行为：
/// 标准声明重命名后的取值、原始声明与 ValueType/Issuer 的保全、未登记声明不得被动到、
/// 以及重复调用的幂等性——ASP.NET Core 每次 AuthenticateAsync 成功都会再跑一遍转换，
/// 同一请求内被调用多次是常态，不幂等就会出现角色声明成倍增长。
/// </remarks>
public class XiHanClaimsTransformationTests
{
    /// <summary>
    /// 转换器实现的是认证管线约定的声明转换接口
    /// </summary>
    [Fact]
    public void Transformation_ImplementsClaimsTransformationContract()
    {
        Assert.IsAssignableFrom<IClaimsTransformation>(CreateTransformation());
    }

    /// <summary>
    /// 六个标准声明按映射表重命名后可以按框架声明类型取到值
    /// </summary>
    [Fact]
    public async Task TransformAsync_MapsStandardClaimsToFrameworkClaimTypes()
    {
        var principal = CreatePrincipal(
            new Claim("sub", "u-1"),
            new Claim("role", "admin"),
            new Claim("email", "somebody@example.com"),
            new Claim("name", "zhangsan"),
            new Claim("family_name", "Zhang"),
            new Claim("given_name", "San"));

        var result = await CreateTransformation().TransformAsync(principal);

        Assert.Equal("u-1", result.FindFirst(XiHanClaimTypes.UserId)?.Value);
        Assert.Equal("admin", result.FindFirst(XiHanClaimTypes.Role)?.Value);
        Assert.Equal("somebody@example.com", result.FindFirst(XiHanClaimTypes.Email)?.Value);
        Assert.Equal("zhangsan", result.FindFirst(XiHanClaimTypes.UserName)?.Value);
        Assert.Equal("Zhang", result.FindFirst(XiHanClaimTypes.SurName)?.Value);
        Assert.Equal("San", result.FindFirst(XiHanClaimTypes.Name)?.Value);
    }

    /// <summary>
    /// 转换是"追加"不是"改写"，原始的 OIDC 声明必须仍然可读
    /// </summary>
    [Fact]
    public async Task TransformAsync_KeepsOriginalClaims()
    {
        var principal = CreatePrincipal(new Claim("sub", "u-1"), new Claim("role", "admin"));

        var result = await CreateTransformation().TransformAsync(principal);

        Assert.Equal("u-1", result.FindFirst("sub")?.Value);
        Assert.Equal("admin", result.FindFirst("role")?.Value);
    }

    /// <summary>
    /// 返回的是同一个主体实例，认证管线拿到的仍是原对象
    /// </summary>
    [Fact]
    public async Task TransformAsync_ReturnsSamePrincipalInstance()
    {
        var principal = CreatePrincipal(new Claim("sub", "u-1"));

        var result = await CreateTransformation().TransformAsync(principal);

        Assert.Same(principal, result);
    }

    /// <summary>
    /// 重命名时保留声明的值类型与签发者，审计链路不能因为改名丢掉来源信息
    /// </summary>
    [Fact]
    public async Task TransformAsync_PreservesValueTypeAndIssuer()
    {
        var principal = CreatePrincipal(new Claim("sub", "u-1", ClaimValueTypes.String, "https://idp.example.com"));

        var result = await CreateTransformation().TransformAsync(principal);

        var mapped = result.FindFirst(XiHanClaimTypes.UserId);

        Assert.NotNull(mapped);
        Assert.Equal(ClaimValueTypes.String, mapped.ValueType);
        Assert.Equal("https://idp.example.com", mapped.Issuer);
    }

    /// <summary>
    /// 映射表之外的声明一个都不加，声明总数不变
    /// </summary>
    [Fact]
    public async Task TransformAsync_WithUnmappedClaimsOnly_AddsNothing()
    {
        var principal = CreatePrincipal(
            new Claim("tenantid", "t-1"),
            new Claim("client_id", "web"),
            new Claim("preferred_username", "zhangsan"));
        var countBefore = principal.Claims.Count();

        var result = await CreateTransformation().TransformAsync(principal);

        Assert.Equal(countBefore, result.Claims.Count());
        Assert.Equal("t-1", result.FindFirst("tenantid")?.Value);
    }

    /// <summary>
    /// 多值角色声明逐条映射，不会只留下第一条
    /// </summary>
    [Fact]
    public async Task TransformAsync_WithMultipleRoles_MapsEveryOne()
    {
        var principal = CreatePrincipal(new Claim("role", "admin"), new Claim("role", "auditor"));

        var result = await CreateTransformation().TransformAsync(principal);

        var roles = result.FindAll(XiHanClaimTypes.Role)
            .Select(claim => claim.Value)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "admin", "auditor" }, roles);
    }

    /// <summary>
    /// 映射后角色判定能直接生效，这是授权策略依赖的最终效果
    /// </summary>
    [Fact]
    public async Task TransformAsync_AfterMapping_RoleCheckSucceeds()
    {
        var principal = CreatePrincipal(new Claim("role", "admin"));

        Assert.False(principal.IsInRole("admin"));

        var result = await CreateTransformation().TransformAsync(principal);

        Assert.True(result.IsInRole("admin"));
        Assert.False(result.IsInRole("auditor"));
    }

    /// <summary>
    /// 宿主往映射表里追加的自定义映射同样生效
    /// </summary>
    [Fact]
    public async Task TransformAsync_WithCustomMapEntry_HonorsIt()
    {
        var mapOptions = new XiHanClaimsMapOptions();
        mapOptions.Maps["phone_number"] = () => XiHanClaimTypes.PhoneNumber;

        var principal = CreatePrincipal(new Claim("phone_number", "13800000000"));

        var result = await CreateTransformation(mapOptions).TransformAsync(principal);

        Assert.Equal("13800000000", result.FindFirst(XiHanClaimTypes.PhoneNumber)?.Value);
    }

    /// <summary>
    /// 宿主移除映射后对应声明不再被改写
    /// </summary>
    [Fact]
    public async Task TransformAsync_WhenMapEntryRemoved_LeavesClaimUnmapped()
    {
        var mapOptions = new XiHanClaimsMapOptions();
        mapOptions.Maps.Remove("role");

        var principal = CreatePrincipal(new Claim("sub", "u-1"), new Claim("role", "admin"));

        var result = await CreateTransformation(mapOptions).TransformAsync(principal);

        Assert.Equal("u-1", result.FindFirst(XiHanClaimTypes.UserId)?.Value);
        Assert.Null(result.FindFirst(XiHanClaimTypes.Role));
    }

    /// <summary>
    /// 匿名主体（无任何声明）转换后依然没有声明，不得凭空造出身份
    /// </summary>
    [Fact]
    public async Task TransformAsync_WithAnonymousPrincipal_AddsNoClaims()
    {
        var principal = new ClaimsPrincipal();

        var result = await CreateTransformation().TransformAsync(principal);

        Assert.Empty(result.Claims);
    }

    /// <summary>
    /// 同一主体重复转换必须幂等，映射出的声明不能成倍增长
    /// </summary>
    /// <remarks>
    /// ASP.NET Core 的 AuthenticationService 在每次 AuthenticateAsync 成功后都会调用一次声明转换，
    /// 一个请求里中间件、[Authorize] 策略求值、业务代码手动调用会叠加多次，
    /// 传入的还是同一个 ClaimsPrincipal 实例，因此这里是真实会发生的场景而非人造用例。
    /// </remarks>
    [Fact]
    public async Task TransformAsync_CalledTwice_DoesNotDuplicateMappedClaims()
    {
        var principal = CreatePrincipal(new Claim("sub", "u-1"), new Claim("role", "admin"));
        var transformation = CreateTransformation();

        await transformation.TransformAsync(principal);
        await transformation.TransformAsync(principal);

        var userIds = principal.FindAll(XiHanClaimTypes.UserId).ToList();
        var roles = principal.FindAll(XiHanClaimTypes.Role).ToList();

        Assert.Single(userIds);
        Assert.Single(roles);
    }

    /// <summary>
    /// 构造一个使用给定映射表的转换器
    /// </summary>
    /// <param name="mapOptions">映射选项，默认取默认映射表</param>
    /// <returns>转换器实例</returns>
    private static XiHanClaimsTransformation CreateTransformation(XiHanClaimsMapOptions? mapOptions = null)
    {
        return new XiHanClaimsTransformation(
            new OptionsWrapper<XiHanClaimsMapOptions>(mapOptions ?? new XiHanClaimsMapOptions()));
    }

    /// <summary>
    /// 构造一个已认证的主体
    /// </summary>
    /// <param name="claims">初始声明</param>
    /// <returns>主体实例</returns>
    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestScheme"));
    }
}
