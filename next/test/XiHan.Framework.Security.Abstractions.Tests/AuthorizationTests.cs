// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Tests;

public sealed class AuthorizationTests
{
    private static readonly PermissionGrant[] _grants =
    [
        new(GrantSubject.ForRole("editor"), "orders.read"),
        new(GrantSubject.ForRole("admin"), PermissionGrant.All),
        new(GrantSubject.ForUser("alice"), "reports.read"),
        new(GrantSubject.ForUser("alice"), "reports.export", "acme"),
    ];

    private static PipelineStep Handler(bool[] reached) => _ =>
    {
        reached[0] = true;
        return ValueTask.CompletedTask;
    };

    private static IServiceProvider Services(IPolicy? extra = null)
    {
        var checker = new PermissionChecker(new DictionaryGrantStore(_grants), new SecurityOptions());
        var policies = new List<IPolicy> { new AuthenticatedPolicy() };
        if (extra is not null)
        {
            policies.Add(extra);
        }

        return SecurityHosts.Services(s => s.AddSingleton(new PolicyCatalog(policies, SecurityHosts.Registry(SecurityHosts.OrdersRead), checker)));
    }

    [Fact]
    public async Task PermissionChecker_UnionsUserAndRoleGrants_AndHonoursWildcardAndTenant()
    {
        var checker = new PermissionChecker(new DictionaryGrantStore(_grants), new SecurityOptions());
        var alice = new SecurityContext(SecurityHosts.Alice("editor"), null);
        var aliceInAcme = new SecurityContext(SecurityHosts.Alice("editor"), new TenantContext("acme"));
        var root = new SecurityContext(new Principal("root", "test") { Claims = [new("role", "admin")] }, null);

        Assert.True(await checker.HasPermissionAsync(alice, "orders.read", CancellationToken.None));
        Assert.True(await checker.HasPermissionAsync(alice, "reports.read", CancellationToken.None));
        Assert.False(await checker.HasPermissionAsync(alice, "reports.export", CancellationToken.None));
        Assert.True(await checker.HasPermissionAsync(aliceInAcme, "reports.export", CancellationToken.None));
        Assert.False(await checker.HasPermissionAsync(alice, "orders.delete", CancellationToken.None));
        Assert.True(await checker.HasPermissionAsync(root, "anything.at.all", CancellationToken.None));
        Assert.False(await checker.HasPermissionAsync(SecurityContext.Anonymous, "orders.read", CancellationToken.None));
    }

    [Fact]
    public void PolicyCatalog_FindsRegistered_AndDynamicPermissionPolicies_OnlyForDefinedPermissions()
    {
        var catalog = new PolicyCatalog([new AuthenticatedPolicy()], SecurityHosts.Registry(SecurityHosts.OrdersRead), new PermissionChecker(new DictionaryGrantStore(), new SecurityOptions()));

        Assert.True(catalog.Find(AuthenticatedPolicy.PolicyName).HasValue);
        Assert.IsType<PermissionPolicy>(catalog.Find("permission:orders.read").ValueOrThrow());
        Assert.False(catalog.Find("permission:orders.delete").HasValue);
        Assert.False(catalog.Find("permission:").HasValue);
        Assert.False(catalog.Find("nope").HasValue);
        Assert.Equal([AuthenticatedPolicy.PolicyName], catalog.Names);
        Assert.Equal("permission:orders.read", AuthorizationRequirement.Permission("orders.read").Policy);
        Assert.Throws<ArgumentException>(() => new PolicyCatalog([new AuthenticatedPolicy(), new AuthenticatedPolicy()], new PermissionRegistry(), new PermissionChecker(new DictionaryGrantStore(), new SecurityOptions())));
    }

    [Fact]
    public async Task NoRequirement_PassesThrough()
    {
        var context = SecurityHosts.Context(Services());
        var reached = new bool[1];

        await new AuthorizationMiddleware().InvokeAsync(context, Handler(reached));

        Assert.True(reached[0]);
    }

    [Fact]
    public async Task UnknownPolicy_IsDenied_NotFound()
    {
        var context = SecurityHosts.Context(Services());
        context.RequireAuthorization(new AuthorizationRequirement("missing"));
        var reached = new bool[1];

        await new AuthorizationMiddleware().InvokeAsync(context, Handler(reached));

        Assert.False(reached[0]);
        Assert.Equal(SecurityErrors.PolicyNotFound, context.GetOutcome().ValueOrThrow().Error?.Code);
    }

    [Fact]
    public async Task AuthenticatedPolicy_DeniesAnonymous_AllowsAuthenticated()
    {
        var anonymous = SecurityHosts.Context(Services());
        anonymous.RequireAuthorization(AuthorizationRequirement.Authenticated);
        var reachedAnonymous = new bool[1];
        await new AuthorizationMiddleware().InvokeAsync(anonymous, Handler(reachedAnonymous));
        Assert.False(reachedAnonymous[0]);
        Assert.Equal(SecurityErrors.NotAuthenticated, anonymous.GetOutcome().ValueOrThrow().Error?.Code);

        var authenticated = SecurityHosts.Context(Services());
        authenticated.SetSecurity(new SecurityContext(SecurityHosts.Alice(), null));
        authenticated.RequireAuthorization(AuthorizationRequirement.Authenticated);
        var reached = new bool[1];
        await new AuthorizationMiddleware().InvokeAsync(authenticated, Handler(reached));
        Assert.True(reached[0]);
    }

    [Fact]
    public async Task PermissionPolicy_AllowsGranted_DeniesMissing_AndAnonymous()
    {
        var editor = SecurityHosts.Context(Services());
        editor.SetSecurity(new SecurityContext(SecurityHosts.Alice("editor"), null));
        editor.RequireAuthorization(AuthorizationRequirement.Permission("orders.read"));
        var reachedEditor = new bool[1];
        await new AuthorizationMiddleware().InvokeAsync(editor, Handler(reachedEditor));
        Assert.True(reachedEditor[0]);

        var viewer = SecurityHosts.Context(Services());
        viewer.SetSecurity(new SecurityContext(SecurityHosts.Alice("viewer"), null));
        viewer.RequireAuthorization(AuthorizationRequirement.Permission("orders.read"));
        var reachedViewer = new bool[1];
        await new AuthorizationMiddleware().InvokeAsync(viewer, Handler(reachedViewer));
        Assert.False(reachedViewer[0]);
        Assert.Equal(SecurityErrors.PermissionMissing, viewer.GetOutcome().ValueOrThrow().Error?.Code);

        var anonymous = SecurityHosts.Context(Services());
        anonymous.RequireAuthorization(AuthorizationRequirement.Permission("orders.read"));
        var reachedAnonymous = new bool[1];
        await new AuthorizationMiddleware().InvokeAsync(anonymous, Handler(reachedAnonymous));
        Assert.False(reachedAnonymous[0]);
        Assert.Equal(SecurityErrors.NotAuthenticated, anonymous.GetOutcome().ValueOrThrow().Error?.Code);
    }

    [Fact]
    public async Task CustomPolicy_ReceivesSecurityActionAndResource()
    {
        AuthorizationRequest? seen = null;
        var policy = new RecordingPolicy("owner-only", request =>
        {
            seen = request;
            return AuthorizationResult.Deny(Error.Denied("ORDERS_DENIED_001", "不是所有者"));
        });
        var context = SecurityHosts.Context(Services(policy));
        var resource = ResourceName.Parse("xrn:xihan:orders:acme:order/42");
        context.SetSecurity(new SecurityContext(SecurityHosts.Alice(), new TenantContext("acme")));
        context.RequireAuthorization(new AuthorizationRequirement("owner-only", resource));
        var reached = new bool[1];

        await new AuthorizationMiddleware().InvokeAsync(context, Handler(reached));

        Assert.False(reached[0]);
        Assert.Equal("ORDERS_DENIED_001", context.GetOutcome().ValueOrThrow().Error?.Code);
        Assert.NotNull(seen);
        Assert.Equal("owner-only", seen.Action);
        Assert.Equal(resource, seen.Resource);
        Assert.Equal("acme", seen.Security.Tenant?.Id);
    }

    [Fact]
    public void GrantSubject_And_Grant_ValidateInput()
    {
        Assert.Equal("role:admin", GrantSubject.ForRole("admin").ToString());
        Assert.Throws<ArgumentException>(static () => new GrantSubject("group", "x"));
        Assert.Throws<ArgumentException>(static () => GrantSubject.ForUser(" "));
        Assert.Throws<ArgumentException>(static () => new PermissionGrant(GrantSubject.ForUser("a"), ""));
        Assert.Throws<ArgumentException>(static () => new AuthorizationRequirement(" "));
    }

    private sealed class RecordingPolicy(string name, Func<AuthorizationRequest, AuthorizationResult> evaluate) : IPolicy
    {
        public string Name => name;

        public ValueTask<AuthorizationResult> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(evaluate(request));
    }
}
