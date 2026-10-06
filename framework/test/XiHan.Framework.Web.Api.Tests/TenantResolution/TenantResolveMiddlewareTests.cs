// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.ConfigurationStore;
using XiHan.Framework.Security.Users;
using XiHan.Framework.Web.Api.Constants;
using XiHan.Framework.Web.Api.Contexts;
using XiHan.Framework.Web.Api.Middlewares;
using XiHan.Framework.Web.Api.TenantResolvers;

namespace XiHan.Framework.Web.Api.Tests.TenantResolution;

/// <summary>
/// 租户解析中间件：已认证请求的租户只取令牌；外部输入给出的租户必须经租户存储验证；解析后定型请求身份。
/// </summary>
/// <remarks>
/// 宿主（平台）令牌不带租户声明。若放行请求头或查询参数，宿主身份就能逐请求切进任意租户，且不经成员关系校验。
/// </remarks>
public sealed class TenantResolveMiddlewareTests
{
    private const long KnownTenantId = 7;

    private const long InactiveTenantId = 8;

    /// <summary>
    /// 宿主令牌带上租户请求头：不采信，请求仍处于无租户上下文
    /// </summary>
    [Fact]
    public async Task AuthenticatedHost_WithTenantHeader_StaysWithoutTenant()
    {
        var run = await RunAsync(new FakeCurrentUser { IsAuthenticated = true, UserId = 1 }, headerTenant: KnownTenantId.ToString());

        Assert.True(run.NextCalled);
        Assert.Null(run.TenantIdInNext);
    }

    /// <summary>
    /// 宿主令牌带上租户查询参数：同样不采信
    /// </summary>
    [Fact]
    public async Task AuthenticatedHost_WithTenantQuery_StaysWithoutTenant()
    {
        var run = await RunAsync(new FakeCurrentUser { IsAuthenticated = true, UserId = 1 }, queryTenant: KnownTenantId.ToString());

        Assert.Null(run.TenantIdInNext);
    }

    /// <summary>
    /// 租户令牌带上别的租户请求头：以令牌为准
    /// </summary>
    [Fact]
    public async Task AuthenticatedTenantUser_WithOtherTenantHeader_UsesTokenTenant()
    {
        var run = await RunAsync(new FakeCurrentUser { IsAuthenticated = true, UserId = 2, TenantId = 99 }, headerTenant: KnownTenantId.ToString());

        Assert.Equal(99, run.TenantIdInNext);
    }

    /// <summary>
    /// 令牌里的租户已签名，租户存储未登记它（应用自管租户目录）时仍以令牌为准
    /// </summary>
    [Fact]
    public async Task AuthenticatedTenantUser_TenantUnknownToStore_UsesTokenTenant()
    {
        var run = await RunAsync(new FakeCurrentUser { IsAuthenticated = true, UserId = 2, TenantId = 1234 });

        Assert.Equal(1234, run.TenantIdInNext);
    }

    /// <summary>
    /// 匿名请求给出租户存储里登记且激活的租户：进入该租户
    /// </summary>
    [Fact]
    public async Task Anonymous_WithKnownActiveTenant_EntersTenant()
    {
        var run = await RunAsync(new FakeCurrentUser(), headerTenant: KnownTenantId.ToString());

        Assert.Equal(KnownTenantId, run.TenantIdInNext);
    }

    /// <summary>
    /// 匿名请求给出租户存储里查不到的租户：400 拒绝，不拿未经验证的数字建租户上下文
    /// </summary>
    [Fact]
    public async Task Anonymous_WithUnknownTenant_IsRejected()
    {
        var run = await RunAsync(new FakeCurrentUser(), headerTenant: "4242");

        Assert.False(run.NextCalled);
        Assert.Equal(StatusCodes.Status400BadRequest, run.StatusCode);
    }

    /// <summary>
    /// 匿名请求给出已停用的租户：同样拒绝
    /// </summary>
    [Fact]
    public async Task Anonymous_WithInactiveTenant_IsRejected()
    {
        var run = await RunAsync(new FakeCurrentUser(), headerTenant: InactiveTenantId.ToString());

        Assert.False(run.NextCalled);
        Assert.Equal(StatusCodes.Status400BadRequest, run.StatusCode);
    }

    /// <summary>
    /// 匿名且未给出租户：无租户放行
    /// </summary>
    [Fact]
    public async Task Anonymous_WithoutTenant_PassesWithoutTenant()
    {
        var run = await RunAsync(new FakeCurrentUser());

        Assert.True(run.NextCalled);
        Assert.Null(run.TenantIdInNext);
    }

    /// <summary>
    /// 解析完成后请求上下文带上最终生效的身份，位于解析之前的日志中间件据此记账
    /// </summary>
    [Fact]
    public async Task FinalizesRequestIdentity()
    {
        var run = await RunAsync(new FakeCurrentUser { IsAuthenticated = true, UserId = 2, UserName = "u2", TenantId = 99 }, headerTenant: KnownTenantId.ToString());

        Assert.NotNull(run.FinalContext);
        Assert.Equal(2, run.FinalContext.UserId);
        Assert.Equal("u2", run.FinalContext.UserName);
        Assert.Equal(99, run.FinalContext.TenantId);
    }

    private static async Task<RunResult> RunAsync(FakeCurrentUser currentUser, string? headerTenant = null, string? queryTenant = null)
    {
        var httpContext = new DefaultHttpContext();
        if (headerTenant is not null)
        {
            httpContext.Request.Headers["X-Tenant-Id"] = headerTenant;
        }

        if (queryTenant is not null)
        {
            httpContext.Request.QueryString = new QueryString($"?tenantId={queryTenant}");
        }

        httpContext.Response.Body = new MemoryStream();

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUser>(currentUser);
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });
        services.AddSingleton<IRequestContextAccessor, RequestContextAccessor>();
        services.AddSingleton<IOptions<XiHanTenantResolveOptions>>(Options.Create(CreateOptions()));
        httpContext.RequestServices = services.BuildServiceProvider();

        var accessor = httpContext.RequestServices.GetRequiredService<IRequestContextAccessor>();
        accessor.Current = new RequestContext { TraceId = "trace" };
        httpContext.Items[XiHanWebApiConstants.TraceIdItemKey] = "trace";

        var currentTenant = new CurrentTenant(AsyncLocalCurrentTenantAccessor.Instance);
        var result = new RunResult();
        var middleware = new XiHanTenantResolveMiddleware(
            _ =>
            {
                result.NextCalled = true;
                result.TenantIdInNext = currentTenant.Id;
                return Task.CompletedTask;
            },
            Options.Create(CreateOptions()));

        await middleware.InvokeAsync(httpContext, currentTenant, new FakeTenantStore());

        result.StatusCode = httpContext.Response.StatusCode;
        result.FinalContext = accessor.Current;
        return result;
    }

    private static XiHanTenantResolveOptions CreateOptions()
    {
        var options = new XiHanTenantResolveOptions();
        options.TenantResolvers.Add(new CurrentUserTenantResolveContributor());
        options.TenantResolvers.Add(new HeaderTenantResolveContributor());
        options.TenantResolvers.Add(new QueryStringTenantResolveContributor());
        return options;
    }

    private sealed class RunResult
    {
        public bool NextCalled { get; set; }

        public long? TenantIdInNext { get; set; }

        public int StatusCode { get; set; }

        public RequestContext? FinalContext { get; set; }
    }

    private sealed class FakeTenantStore : ITenantStore
    {
        private readonly TenantConfiguration[] _tenants =
        [
            new(KnownTenantId, "known"),
            new(InactiveTenantId, "inactive") { IsActive = false }
        ];

        public Task<TenantConfiguration?> FindAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_tenants.FirstOrDefault(tenant => tenant.Id == id));

        public Task<TenantConfiguration?> FindAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(_tenants.FirstOrDefault(tenant => tenant.Name == name));

        public Task<IReadOnlyList<TenantConfiguration>> GetListAsync(bool includeInactive = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TenantConfiguration>>(_tenants);
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated { get; set; }

        public long? UserId { get; set; }

        public string? UserName { get; set; }

        public string? Name { get; set; }

        public string? SurName { get; set; }

        public string? PhoneNumber { get; set; }

        public bool PhoneNumberVerified { get; set; }

        public string? Email { get; set; }

        public bool EmailVerified { get; set; }

        public long? TenantId { get; set; }

        public string[] Roles { get; set; } = [];

        public Claim? FindClaim(string claimType) => null;

        public Claim[] FindClaims(string claimType) => [];

        public Claim[] GetAllClaims() => [];

        public bool IsInRole(string roleName) => Array.IndexOf(Roles, roleName) >= 0;
    }
}
