// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
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
    /// 两个主体（alice 带角色、租户与附加声明，bob 只有 API Key）与角色、用户授予的配置。
    /// </summary>
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
    public async Task Feature_WhenExplicitAuthenticationProviderWins_RegistersOnlyAuthorization()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemorySecurityFeature>().UseFeature(new StubFeature(TestManifests.Create("xihan.security.fake-jwt", provides: ["security.authentication@1"], requires: ["kernel.hosting@1"]))), _configuration);

        Assert.Empty(host.Services.GetServices<IAuthenticationHandler>());
        Assert.DoesNotContain(nameof(AuthenticationMiddleware), host.App.DescribePipeline(), StringComparison.Ordinal);
        Assert.Contains(nameof(AuthorizationMiddleware), host.App.DescribePipeline(), StringComparison.Ordinal);
        Assert.NotNull(host.Services.GetService<PolicyCatalog>());
    }

    /// <summary>
    /// 在新作用域里经完整管道调用：可选放入 Authorization 头形式的凭据与授权要求，返回结果与处理器看到的主体。
    /// </summary>
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
