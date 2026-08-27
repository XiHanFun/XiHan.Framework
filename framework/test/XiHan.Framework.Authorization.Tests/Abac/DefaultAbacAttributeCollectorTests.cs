// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using XiHan.Framework.Authorization.Abac;

namespace XiHan.Framework.Authorization.Tests.Abac;

/// <summary>
/// 默认 ABAC 属性收集器测试
/// </summary>
/// <remarks>
/// 收集器产出的键名是策略表达式唯一能引用的东西，属于字符串协议：键一律经过“去空白 + 冒号转下划线 + 转小写”规整，
/// 且先写入者优先（同名不覆盖）。这里把主体、资源、环境三段的键名与取值来源逐条锁死。
/// </remarks>
public class DefaultAbacAttributeCollectorTests
{
    /// <summary>
    /// 主体属性从声明里解出用户、租户、角色与认证状态
    /// </summary>
    [Fact]
    public async Task CollectAsync_FromClaims_FillsSubjectAttributes()
    {
        var principal = CreatePrincipal(
            new Claim(ClaimTypes.NameIdentifier, "u1"),
            new Claim("tenantid", "t9"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim("role", "ops"),
            new Claim("dept", "rd"));

        var set = await new DefaultAbacAttributeCollector().CollectAsync(
            principal, null, "Sys.User.Read", "allow", TestContext.Current.CancellationToken);

        Assert.Equal("u1", set.SubjectAttributes["user_id"]);
        Assert.Equal("t9", set.SubjectAttributes["tenant_id"]);
        Assert.True(set.SubjectAttributes["is_authenticated"] is true);
        Assert.Equal("rd", set.SubjectAttributes["claim.dept"]);

        var roles = Assert.IsType<string[]>(set.SubjectAttributes["roles"]);
        Assert.Equal(2, roles.Length);
        Assert.Contains("admin", roles);
        Assert.Contains("ops", roles);
    }

    /// <summary>
    /// 角色去重忽略大小写，避免同一角色被算成两个
    /// </summary>
    [Fact]
    public async Task CollectAsync_WithDuplicateRoles_DeduplicatesIgnoringCase()
    {
        var principal = CreatePrincipal(
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("role", "admin"));

        var set = await new DefaultAbacAttributeCollector().CollectAsync(
            principal, null, "p", "allow", TestContext.Current.CancellationToken);

        Assert.Equal("Admin", Assert.Single(Assert.IsType<string[]>(set.SubjectAttributes["roles"])));
    }

    /// <summary>
    /// 用户标识按 nameidentifier、sub、userid、user_id 的顺序回退解析
    /// </summary>
    /// <param name="claimType">承载用户标识的声明类型</param>
    [Theory]
    [InlineData("sub")]
    [InlineData("userid")]
    [InlineData("user_id")]
    public async Task CollectAsync_ResolvesUserIdFromAlternativeClaimTypes(string claimType)
    {
        var principal = CreatePrincipal(new Claim(claimType, "u1"));

        var set = await new DefaultAbacAttributeCollector().CollectAsync(
            principal, null, "p", "allow", TestContext.Current.CancellationToken);

        Assert.Equal("u1", set.SubjectAttributes["user_id"]);
    }

    /// <summary>
    /// 匿名主体也要产出属性，用户与租户是空串、认证状态为假
    /// </summary>
    [Fact]
    public async Task CollectAsync_WithAnonymousPrincipal_UsesEmptyIdentityAttributes()
    {
        var set = await new DefaultAbacAttributeCollector().CollectAsync(
            new ClaimsPrincipal(), null, "p", "allow", TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, set.SubjectAttributes["user_id"]);
        Assert.Equal(string.Empty, set.SubjectAttributes["tenant_id"]);
        Assert.True(set.SubjectAttributes["is_authenticated"] is false);
        Assert.Empty(Assert.IsType<string[]>(set.SubjectAttributes["roles"]));
    }

    /// <summary>
    /// 资源为空时不产出任何资源属性
    /// </summary>
    [Fact]
    public async Task CollectAsync_WithNullResource_LeavesResourceAttributesEmpty()
    {
        var set = await new DefaultAbacAttributeCollector().CollectAsync(
            CreatePrincipal(), null, "p", "allow", TestContext.Current.CancellationToken);

        Assert.Empty(set.ResourceAttributes);
    }

    /// <summary>
    /// 普通对象资源按公共属性展开，键名统一小写
    /// </summary>
    [Fact]
    public async Task CollectAsync_WithPocoResource_ExpandsPublicProperties()
    {
        var resource = new OrderResource { OwnerUserId = "u1", TenantId = "t9", Amount = 12 };

        var set = await new DefaultAbacAttributeCollector().CollectAsync(
            CreatePrincipal(), resource, "p", "allow", TestContext.Current.CancellationToken);

        Assert.Equal("OrderResource", set.ResourceAttributes["resource_type"]);
        Assert.Equal("u1", set.ResourceAttributes["owneruserid"]);
        Assert.Equal("t9", set.ResourceAttributes["tenantid"]);
        Assert.Equal(12, set.ResourceAttributes["amount"]);
    }

    /// <summary>
    /// 字符串资源只产出类型与值，不做属性反射
    /// </summary>
    [Fact]
    public async Task CollectAsync_WithStringResource_UsesResourceValue()
    {
        var set = await new DefaultAbacAttributeCollector().CollectAsync(
            CreatePrincipal(), "order-1", "p", "allow", TestContext.Current.CancellationToken);

        Assert.Equal("String", set.ResourceAttributes["resource_type"]);
        Assert.Equal("order-1", set.ResourceAttributes["resource_value"]);
        Assert.Equal(2, set.ResourceAttributes.Count);
    }

    /// <summary>
    /// 基元类型资源同样只产出类型与值
    /// </summary>
    [Fact]
    public async Task CollectAsync_WithPrimitiveResource_UsesResourceValue()
    {
        var set = await new DefaultAbacAttributeCollector().CollectAsync(
            CreatePrincipal(), 42, "p", "allow", TestContext.Current.CancellationToken);

        Assert.Equal("Int32", set.ResourceAttributes["resource_type"]);
        Assert.Equal(42, set.ResourceAttributes["resource_value"]);
    }

    /// <summary>
    /// 环境属性始终带上权限编码、策略编码与时间信息
    /// </summary>
    [Fact]
    public async Task CollectAsync_AlwaysFillsEnvironmentBasics()
    {
        var set = await new DefaultAbacAttributeCollector().CollectAsync(
            CreatePrincipal(), null, "Sys.User.Read", "same_tenant", TestContext.Current.CancellationToken);

        Assert.Equal("Sys.User.Read", set.EnvironmentAttributes["permission_code"]);
        Assert.Equal("same_tenant", set.EnvironmentAttributes["policy_code"]);
        Assert.True(set.EnvironmentAttributes.ContainsKey("utc_now"));
        Assert.True(set.EnvironmentAttributes.ContainsKey("utc_hour"));
        Assert.True(set.EnvironmentAttributes.ContainsKey("day_of_week"));
    }

    /// <summary>
    /// 资源是 HttpContext 时，路由、查询串、方法、路径与客户端地址都进资源属性
    /// </summary>
    [Fact]
    public async Task CollectAsync_WithHttpContextResource_FillsRequestAttributes()
    {
        var httpContext = CreateHttpContext();

        var set = await new DefaultAbacAttributeCollector().CollectAsync(
            CreatePrincipal(), httpContext, "p", "same_tenant", TestContext.Current.CancellationToken);

        Assert.Equal("t9", set.ResourceAttributes["route.tenant_id"]);
        Assert.Equal("u1", set.ResourceAttributes["query.user_id"]);
        Assert.Equal("POST", set.ResourceAttributes["http.method"]);
        Assert.Equal("/api/orders/9", set.ResourceAttributes["http.path"]);
        Assert.Equal("10.0.0.1", set.ResourceAttributes["http.client_ip"]);
    }

    /// <summary>
    /// 资源是 HttpContext 时，环境属性另有一套不带前缀的请求键
    /// </summary>
    [Fact]
    public async Task CollectAsync_WithHttpContextResource_FillsEnvironmentRequestAttributes()
    {
        var httpContext = CreateHttpContext();

        var set = await new DefaultAbacAttributeCollector().CollectAsync(
            CreatePrincipal(), httpContext, "p", "same_tenant", TestContext.Current.CancellationToken);

        Assert.Equal("10.0.0.1", set.EnvironmentAttributes["client_ip"]);
        Assert.Equal("/api/orders/9", set.EnvironmentAttributes["request_path"]);
        Assert.Equal("POST", set.EnvironmentAttributes["request_method"]);
        Assert.Equal("xihan-test", set.EnvironmentAttributes["user_agent"]);
    }

    /// <summary>
    /// 资源自身带 HttpContext 属性时也能解析出请求上下文（MVC 的资源包装场景）
    /// </summary>
    [Fact]
    public async Task CollectAsync_WithHttpContextCarrier_ResolvesNestedHttpContext()
    {
        var carrier = new HttpContextCarrier { HttpContext = CreateHttpContext() };

        var set = await new DefaultAbacAttributeCollector().CollectAsync(
            CreatePrincipal(), carrier, "p", "same_tenant", TestContext.Current.CancellationToken);

        Assert.Equal("HttpContextCarrier", set.ResourceAttributes["resource_type"]);
        Assert.Equal("t9", set.ResourceAttributes["route.tenant_id"]);
        Assert.Equal("POST", set.EnvironmentAttributes["request_method"]);
    }

    /// <summary>
    /// 非 HttpContext 资源不会污染环境属性里的请求键
    /// </summary>
    [Fact]
    public async Task CollectAsync_WithPocoResource_LeavesRequestEnvironmentUnset()
    {
        var set = await new DefaultAbacAttributeCollector().CollectAsync(
            CreatePrincipal(), new OrderResource(), "p", "allow", TestContext.Current.CancellationToken);

        Assert.False(set.EnvironmentAttributes.ContainsKey("request_method"));
        Assert.False(set.EnvironmentAttributes.ContainsKey("client_ip"));
    }

    /// <summary>
    /// 令牌已取消时直接抛出，不做任何收集
    /// </summary>
    [Fact]
    public async Task CollectAsync_WhenTokenCancelled_Throws()
    {
        var collector = new DefaultAbacAttributeCollector();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => collector.CollectAsync(CreatePrincipal(), null, "p", "allow", cts.Token));
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = "/api/orders/9";
        httpContext.Request.QueryString = new QueryString("?user_id=u1");
        httpContext.Request.RouteValues["tenant_id"] = "t9";
        httpContext.Request.Headers.UserAgent = "xihan-test";
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        return httpContext;
    }

    /// <summary>
    /// 用于验证属性展开的普通资源对象
    /// </summary>
    private sealed class OrderResource
    {
        public string OwnerUserId { get; set; } = string.Empty;

        public string TenantId { get; set; } = string.Empty;

        public int Amount { get; set; }
    }

    /// <summary>
    /// 用于验证 HttpContext 鸭子类型解析的资源包装对象
    /// </summary>
    private sealed class HttpContextCarrier
    {
        public HttpContext? HttpContext { get; set; }
    }
}
