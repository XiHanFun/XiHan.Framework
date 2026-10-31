// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authentication;
using XiHan.Framework.Security.Abstractions.Authorization;
using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.InMemory.Tests;

/// <summary>
/// 内存安全特性的选项解析、存储容量与端到端认证授权测试。
/// </summary>
public sealed class InMemorySecurityTests
{
    /// <summary>
    /// 两个主体与角色、用户授予的配置。
    /// </summary>
    /// <remarks>
    /// alice 带角色、租户与附加声明，bob 只有 API Key。
    /// </remarks>
    private static readonly KeyValuePair<string, string?>[] _configuration =
    [
        new("XiHan:Security:InMemory:Principals:alice:ApiKey", "dev-alice-key"),
        new("XiHan:Security:InMemory:Principals:alice:DisplayName", "Alice"),
        new("XiHan:Security:InMemory:Principals:alice:Roles", "editor, auditor"),
        new("XiHan:Security:InMemory:Principals:alice:Tenant", "acme"),
        new("XiHan:Security:InMemory:Principals:alice:Claims:email", "alice@example.com"),
        new("XiHan:Security:InMemory:Principals:bob:ApiKey", "dev-bob-key"),
        new("XiHan:Security:InMemory:Grants:role:editor", "orders.read,orders.write"),
        new("XiHan:Security:InMemory:Grants:user:bob", "reports.read"),
    ];

    /// <summary>
    /// 登记权限码的特性，模拟业务能力包。
    /// </summary>
    private sealed class OrdersFeature : IFeature
    {
        /// <inheritdoc />
        public Manifest Manifest { get; } = TestManifests.Create("xihan.app.orders", requires: ["security.authorization@1"]);

        /// <inheritdoc />
        public void Configure(FeatureContext context)
        {
            context.AddPermission(new PermissionDefinition("orders.read", "读订单"));
            context.AddPermission(new PermissionDefinition("orders.delete", "删订单"));
        }
    }

    [Fact]
    public void Options_FromConfiguration_LoadsPrincipalsAndGrants()
    {
        var options = InMemorySecurityOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(_configuration).Build());

        var alice = options.Principals.Single(static p => p.Id == "alice");
        Assert.Equal("dev-alice-key", alice.ApiKey);
        Assert.Equal("Alice", alice.DisplayName);
        Assert.Equal(["editor", "auditor"], alice.Roles);
        Assert.Equal("acme", alice.Tenant);
        Assert.Equal([new("email", "alice@example.com")], alice.Claims);
        Assert.Equal(3, options.Grants.Count);
        Assert.Contains(options.Grants, static g => g.Subject == GrantSubject.ForRole("editor") && g.Permission == "orders.write");
        Assert.Contains(options.Grants, static g => g.Subject == GrantSubject.ForUser("bob") && g.Permission == "reports.read");

        Assert.Throws<ArgumentException>(static () => InMemorySecurityOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection([new("XiHan:Security:InMemory:Principals:eve:DisplayName", "Eve")]).Build()));
        Assert.Throws<ArgumentException>(static () => InMemorySecurityOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection([new("XiHan:Security:InMemory:Principals:eve:ApiKey", "short")]).Build()));
    }

    [Fact]
    public void Stores_AreBounded_AndSupportRuntimeChanges()
    {
        var principals = new InMemoryPrincipalStore(new InMemorySecurityOptions { MaxPrincipals = 1 });
        Assert.True(principals.Set(new PrincipalRecord { Id = "a", ApiKey = "aaaaaaaa" }).IsSuccess);
        Assert.Equal(SecurityErrors.CapacityExceeded, principals.Set(new PrincipalRecord { Id = "b", ApiKey = "bbbbbbbb" }).Error?.Code);
        Assert.True(principals.FindByApiKey("aaaaaaaa").HasValue);
        Assert.False(principals.FindByApiKey("aaaaaaab").HasValue);
        Assert.True(principals.Remove("a"));
        Assert.Equal(0, principals.Count);

        var grants = new InMemoryPermissionGrantStore(new InMemorySecurityOptions { MaxGrants = 2 });
        Assert.True(grants.Grant(new PermissionGrant(GrantSubject.ForRole("r"), "a")).IsSuccess);
        Assert.True(grants.Grant(new PermissionGrant(GrantSubject.ForRole("r"), "a")).IsSuccess);
        Assert.True(grants.Grant(new PermissionGrant(GrantSubject.ForRole("r"), "b", "acme")).IsSuccess);
        Assert.Equal(SecurityErrors.CapacityExceeded, grants.Grant(new PermissionGrant(GrantSubject.ForRole("r"), "c")).Error?.Code);
        Assert.Equal(2, grants.Count);
        Assert.True(grants.Revoke(new PermissionGrant(GrantSubject.ForRole("r"), "b", "acme")));
        Assert.False(grants.Revoke(new PermissionGrant(GrantSubject.ForRole("r"), "b", "acme")));
        Assert.True(grants.Grant(new PermissionGrant(GrantSubject.ForRole("r"), "c")).IsSuccess);
    }

    [Fact]
    public async Task GrantStore_StaysBounded_InSubjectsAsWellAsGrants()
    {
        var grants = new InMemoryPermissionGrantStore(new InMemorySecurityOptions { MaxGrants = 1 });
        Assert.True(grants.Grant(new PermissionGrant(GrantSubject.ForUser("u0"), "a")).IsSuccess);

        // 满了以后新主体的授予被拒绝，授予表里也不多出这个主体的键
        for (var i = 1; i <= 50; i++)
        {
            Assert.Equal(SecurityErrors.CapacityExceeded, grants.Grant(new PermissionGrant(GrantSubject.ForUser($"u{i}"), "a", "acme")).Error?.Code);
        }

        Assert.Equal(1, grants.Count);
        Assert.Equal(1, grants.SubjectCount);
        Assert.Empty(await grants.GetGrantsAsync(GrantSubject.ForUser("u1"), "acme", CancellationToken.None));

        // 撤销主体的最后一条授予连同它的键一起移除，重复撤销返回假
        Assert.True(grants.Revoke(new PermissionGrant(GrantSubject.ForUser("u0"), "a")));
        Assert.Equal(0, grants.Count);
        Assert.Equal(0, grants.SubjectCount);
        Assert.False(grants.Revoke(new PermissionGrant(GrantSubject.ForUser("u0"), "a")));
        Assert.Empty(await grants.GetGrantsAsync(GrantSubject.ForUser("u0"), null, CancellationToken.None));

        // 不同主体反复授予再撤销，授予表不随次数增长
        for (var i = 0; i < 50; i++)
        {
            var grant = new PermissionGrant(GrantSubject.ForUser($"s{i}"), "a", "acme");
            Assert.True(grants.Grant(grant).IsSuccess);
            Assert.Equal(1, grants.SubjectCount);
            Assert.True(grants.Revoke(grant));
        }

        Assert.Equal(0, grants.SubjectCount);

        // 同一主体还有别的授予时只撤掉那一条，键保留
        var wide = new InMemoryPermissionGrantStore(new InMemorySecurityOptions { MaxGrants = 3 });
        Assert.True(wide.Grant(new PermissionGrant(GrantSubject.ForRole("r"), "a")).IsSuccess);
        Assert.True(wide.Grant(new PermissionGrant(GrantSubject.ForRole("r"), "b")).IsSuccess);
        Assert.True(wide.Revoke(new PermissionGrant(GrantSubject.ForRole("r"), "a")));
        Assert.Equal(1, wide.SubjectCount);
        Assert.Equal(["b"], await wide.GetGrantsAsync(GrantSubject.ForRole("r"), null, CancellationToken.None));
    }

    [Fact]
    public async Task Feature_EndToEnd_ApiKeyAuthentication_AndPermissionAuthorization()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemorySecurityFeature>().UseFeature<OrdersFeature>(), _configuration);
        Assert.Contains(nameof(AuthenticationMiddleware), host.App.DescribePipeline(), StringComparison.Ordinal);
        Assert.Contains(nameof(AuthorizationMiddleware), host.App.DescribePipeline(), StringComparison.Ordinal);

        Assert.True((await InvokeAsync(host, "ApiKey dev-alice-key", AuthorizationRequirement.Permission("orders.read"))).Result.IsSuccess);
        var (deleted, _) = await InvokeAsync(host, "ApiKey dev-alice-key", AuthorizationRequirement.Permission("orders.delete"));
        Assert.Equal(SecurityErrors.PermissionMissing, deleted.Error?.Code);
        var (undefined, _) = await InvokeAsync(host, "ApiKey dev-alice-key", AuthorizationRequirement.Permission("orders.archive"));
        Assert.Equal(SecurityErrors.PolicyNotFound, undefined.Error?.Code);
        var (wrongKey, _) = await InvokeAsync(host, "ApiKey nope-nope", AuthorizationRequirement.Authenticated);
        Assert.Equal(SecurityErrors.InvalidCredential, wrongKey.Error?.Code);
        var (anonymous, _) = await InvokeAsync(host, null, AuthorizationRequirement.Authenticated);
        Assert.Equal(SecurityErrors.NotAuthenticated, anonymous.Error?.Code);
        Assert.True((await InvokeAsync(host, null, requirement: null)).Result.IsSuccess);

        var (_, principal) = await InvokeAsync(host, "ApiKey dev-alice-key", requirement: null);
        Assert.NotNull(principal);
        Assert.Equal("alice", principal.Id);
        Assert.Equal("Alice", principal.DisplayName);
        Assert.Equal(["editor", "auditor"], principal.ClaimValues("role"));
        Assert.Equal(["acme"], principal.ClaimValues("tenant"));
        Assert.True(principal.HasClaim("email", "alice@example.com"));
    }

    [Fact]
    public async Task ApiKeyTenantClaim_FollowsMultiTenancyTenantClaim_AndWinsOverBaggage()
    {
        // 改了 XiHan:MultiTenancy:TenantClaim 之后，ApiKey 主体的租户写成同一类型的声明，Tenant 阶段的声明解析器照样认出它
        KeyValuePair<string, string?>[] configuration =
        [
            .. _configuration,
            new("XiHan:MultiTenancy:TenantClaim", "tid"),
            new("XiHan:MultiTenancy:Tenants:acme:DisplayName", "Acme"),
            new("XiHan:MultiTenancy:Tenants:other:DisplayName", "Other"),
        ];
        Assert.Equal("tid", InMemorySecurityOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(configuration).Build()).TenantClaim);
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemorySecurityFeature>().UseFeature<InMemoryMultiTenancyFeature>(), configuration);

        await using var scope = host.Services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None, baggage: new Dictionary<string, string> { [BaggageKeys.Tenant] = "other" });
        Assert.True(AuthenticationCredential.TryParse("ApiKey dev-alice-key", out var credential));
        context.SetCredential(credential);
        SecurityContext? security = null;
        var result = await host.App.InvokeAsync(context, ctx =>
        {
            security = ctx.GetSecurity();
            return ValueTask.CompletedTask;
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(security);
        Assert.Equal(["acme"], security.Principal.ClaimValues("tid"));
        Assert.Empty(security.Principal.ClaimValues("tenant"));
        // 已认证主体的租户声明优先于调用方自报的 Baggage
        Assert.Equal("acme", security.Tenant?.Id);

        // 缺省仍是 tenant；空值不合法
        Assert.Equal("tenant", new InMemorySecurityOptions().TenantClaim);
        Assert.Throws<ArgumentException>(static () => new InMemorySecurityOptions { TenantClaim = " " });
    }

    [Fact]
    public async Task Feature_AppliesRegisteredPermissionFilters_AfterTheGrantMatches()
    {
        // 应用按单例登记的收窄过滤器被 Provider 登记的检查器接上：editor 持 orders.read，但白名单是空的
        var filter = new DenyAllPermissionFilter();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemorySecurityFeature>().UseFeature<OrdersFeature>(),
            _configuration,
            services => services.AddSingleton<IPermissionFilter>(filter));

        var (narrowed, _) = await InvokeAsync(host, "ApiKey dev-alice-key", AuthorizationRequirement.Permission("orders.read"));
        Assert.Equal(SecurityErrors.PermissionMissing, narrowed.Error?.Code);
        Assert.Equal(["orders.read"], filter.Permissions);

        // 授予未命中（alice 没有 orders.delete）与匿名都不调用过滤器
        await InvokeAsync(host, "ApiKey dev-alice-key", AuthorizationRequirement.Permission("orders.delete"));
        await InvokeAsync(host, null, AuthorizationRequirement.Permission("orders.read"));
        Assert.Equal(["orders.read"], filter.Permissions);
    }

    [Fact]
    public async Task Feature_AppliesRegisteredGrantSources_WhenTheStoreMisses()
    {
        // 应用按单例登记的授予来源被 Provider 登记的检查器接上：alice 的存储授予里没有 orders.delete，来源补上
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemorySecurityFeature>().UseFeature<OrdersFeature>(),
            _configuration,
            static services => services.AddSingleton<IPermissionGrantSource>(new FixedGrantSource("orders.delete")));

        var (granted, _) = await InvokeAsync(host, "ApiKey dev-alice-key", AuthorizationRequirement.Permission("orders.delete"));
        Assert.True(granted.IsSuccess, granted.ToString());
        var (anonymous, _) = await InvokeAsync(host, null, AuthorizationRequirement.Permission("orders.delete"));
        Assert.True(anonymous.IsFailure);
    }

    /// <summary>
    /// 给任何已认证主体固定额外授予的来源。
    /// </summary>
    private sealed class FixedGrantSource(params string[] grants) : IPermissionGrantSource
    {
        /// <inheritdoc />
        public ValueTask<IReadOnlyCollection<string>> GetGrantsAsync(SecurityContext security, CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyCollection<string>>(grants);
    }

    /// <summary>
    /// 一律拒绝并记录被问到的权限码的过滤器。
    /// </summary>
    private sealed class DenyAllPermissionFilter : IPermissionFilter
    {
        /// <summary>
        /// 被问到的权限码，按调用顺序。
        /// </summary>
        public List<string> Permissions { get; } = [];

        /// <inheritdoc />
        public ValueTask<bool> AllowsAsync(SecurityContext security, string permission, CancellationToken cancellationToken)
        {
            Permissions.Add(permission);
            return ValueTask.FromResult(false);
        }
    }

    [Fact]
    public async Task Feature_WhenExplicitAuthenticationProviderWins_RegistersOnlyAuthorization()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemorySecurityFeature>().UseFeature(new StubFeature(TestManifests.Create("xihan.security.fake-jwt", provides: ["security.authentication@1"], requires: ["kernel.hosting@1"]))), _configuration);

        Assert.Empty(host.Services.GetServices<IAuthenticationHandler>());
        Assert.DoesNotContain(nameof(AuthenticationMiddleware), host.App.DescribePipeline(), StringComparison.Ordinal);
        Assert.Contains(nameof(AuthorizationMiddleware), host.App.DescribePipeline(), StringComparison.Ordinal);
        Assert.NotNull(host.Services.GetService<PolicyCatalog>());
    }

    /// <summary>
    /// 在新作用域里经完整管道调用。
    /// </summary>
    /// <remarks>
    /// 可选放入 Authorization 头形式的凭据与授权要求，返回结果与处理器看到的主体。
    /// </remarks>
    private static async Task<(Result Result, Principal? Principal)> InvokeAsync(TestHost host, string? authorization, AuthorizationRequirement? requirement)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None);
        if (authorization is not null && AuthenticationCredential.TryParse(authorization, out var credential))
        {
            context.SetCredential(credential);
        }

        if (requirement is not null)
        {
            context.RequireAuthorization(requirement);
        }

        Principal? principal = null;
        var result = await host.App.InvokeAsync(context, ctx =>
        {
            principal = ctx.GetSecurity().Principal;
            return ValueTask.CompletedTask;
        });
        return (result, principal);
    }
}
