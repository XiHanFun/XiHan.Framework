// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Authorization;
using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Security.Abstractions.Tests.Fixtures;

namespace XiHan.Framework.Security.Abstractions.Tests.Authorization;

/// <summary>
/// 权限检查器、策略目录与授权中间件的契约测试。
/// </summary>
public sealed class AuthorizationTests
{
    /// <summary>
    /// 测试用授予：角色授予、通配授予、用户全局授予与租户专属授予。
    /// </summary>
    private static readonly PermissionGrant[] _grants =
    [
        new(GrantSubject.ForRole("editor"), "orders.read"),
        new(GrantSubject.ForRole("admin"), PermissionGrant.All),
        new(GrantSubject.ForUser("alice"), "reports.read"),
        new(GrantSubject.ForUser("alice"), "reports.export", "acme"),
    ];

    /// <summary>
    /// 记录是否到达下游的终端步骤。
    /// </summary>
    private static PipelineStep Handler(bool[] reached) => _ =>
    {
        reached[0] = true;
        return ValueTask.CompletedTask;
    };

    /// <summary>
    /// 构造带策略目录的服务：已认证策略、可选的额外策略，权限登记表只含 <c>orders.read</c>。
    /// </summary>
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
    public async Task PermissionChecker_NarrowsGrantedPermissions_ThroughEveryFilter()
    {
        // 套餐白名单式的收窄：只放行 orders.read 与 reports.read；第二个过滤器全部放行，只用来证明逐个 AND
        var edition = new RecordingPermissionFilter(new HashSet<string>(StringComparer.Ordinal) { "orders.read", "reports.read" });
        var audit = new RecordingPermissionFilter(allowed: null);
        var checker = new PermissionChecker(new DictionaryGrantStore(_grants), new SecurityOptions(), [edition, audit]);
        var alice = new SecurityContext(SecurityHosts.Alice("editor"), null);
        var root = new SecurityContext(new Principal("root", "test") { Claims = [new("role", "admin")] }, null);

        // 授予命中且全部放行 → 持有；两个过滤器按登记顺序都被调用
        Assert.True(await checker.HasPermissionAsync(alice, "orders.read", CancellationToken.None));
        Assert.Equal([("alice", "orders.read")], edition.Calls);
        Assert.Equal([("alice", "orders.read")], audit.Calls);

        // 持 * 的主体也被收窄：过滤器收到的是具体权限码，拒绝即不持有，后面的过滤器不再调用
        Assert.False(await checker.HasPermissionAsync(root, "orders.delete", CancellationToken.None));
        Assert.Equal(("root", "orders.delete"), edition.Calls[^1]);
        Assert.Single(audit.Calls);
        Assert.True(await checker.HasPermissionAsync(root, "reports.read", CancellationToken.None));

        // 授予未命中与匿名主体不调用过滤器：过滤器只能拿走权限、不能凭空给出
        var before = edition.Calls.Count;
        Assert.False(await checker.HasPermissionAsync(alice, "orders.delete", CancellationToken.None));
        Assert.False(await checker.HasPermissionAsync(SecurityContext.Anonymous, "orders.read", CancellationToken.None));
        Assert.Equal(before, edition.Calls.Count);

        // 没有过滤器与过滤器为空集合时行为同原来；集合里有 null 是编程错误
        Assert.True(await new PermissionChecker(new DictionaryGrantStore(_grants), new SecurityOptions(), []).HasPermissionAsync(root, "orders.delete", CancellationToken.None));
        Assert.Throws<ArgumentException>(() => new PermissionChecker(new DictionaryGrantStore(_grants), new SecurityOptions(), [edition, null!]));
    }

    [Fact]
    public async Task PermissionChecker_AddsGrantsFromSources_OnlyAfterTheStoreMisses_AndStillNarrows()
    {
        // 继承式的来源给 alice 加 orders.write，委托式的来源给 bob 加 *；过滤器不放行 orders.delete
        var inherited = new RecordingGrantSource(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["alice"] = ["orders.write"] });
        var delegated = new RecordingGrantSource(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["bob"] = [PermissionGrant.All] });
        var narrowing = new RecordingPermissionFilter(new HashSet<string>(StringComparer.Ordinal) { "orders.read", "orders.write", "reports.read" });
        var checker = new PermissionChecker(new DictionaryGrantStore(_grants), new SecurityOptions(), [narrowing], [inherited, delegated]);
        var alice = new SecurityContext(SecurityHosts.Alice("editor"), null);
        var bob = new SecurityContext(new Principal("bob", "test"), null);

        // 存储命中时不查来源
        Assert.True(await checker.HasPermissionAsync(alice, "orders.read", CancellationToken.None));
        Assert.Empty(inherited.Calls);

        // 存储没命中：按登记顺序查来源，第一个命中即止；来源给出的授予同样经过滤器
        Assert.True(await checker.HasPermissionAsync(alice, "orders.write", CancellationToken.None));
        Assert.Equal(["alice"], inherited.Calls);
        Assert.Empty(delegated.Calls);
        Assert.Equal(("alice", "orders.write"), narrowing.Calls[^1]);

        // 来源的 * 也只是授予命中，过滤器照常收窄；来源都没命中就不调用过滤器
        Assert.True(await checker.HasPermissionAsync(bob, "reports.read", CancellationToken.None));
        Assert.False(await checker.HasPermissionAsync(bob, "orders.delete", CancellationToken.None));
        var filtered = narrowing.Calls.Count;
        Assert.False(await checker.HasPermissionAsync(alice, "orders.delete", CancellationToken.None));
        Assert.Equal(filtered, narrowing.Calls.Count);
        Assert.Equal(["bob", "bob", "alice"], delegated.Calls);

        // 匿名主体不查来源；集合里有 null 是编程错误
        var asked = inherited.Calls.Count;
        Assert.False(await checker.HasPermissionAsync(SecurityContext.Anonymous, "orders.write", CancellationToken.None));
        Assert.Equal(asked, inherited.Calls.Count);
        Assert.Throws<ArgumentException>(() => new PermissionChecker(new DictionaryGrantStore(_grants), new SecurityOptions(), [], [inherited, null!]));
    }

    [Fact]
    public async Task PermissionPolicy_ReportsPermissionMissing_WhenAFilterNarrowsTheGrant()
    {
        var edition = new RecordingPermissionFilter(new HashSet<string>(StringComparer.Ordinal));
        var catalog = new PolicyCatalog([new AuthenticatedPolicy()], SecurityHosts.Registry(SecurityHosts.OrdersRead), new PermissionChecker(new DictionaryGrantStore(_grants), new SecurityOptions(), [edition]));
        var policy = catalog.Find(PermissionPolicy.NameFor(SecurityHosts.OrdersRead.Code)).ValueOrThrow();

        // 授予命中（editor 持 orders.read）但过滤器拒绝：与缺权限同一个错误码，调用方不需要区分"没授予"与"被收窄"
        var result = await policy.EvaluateAsync(new AuthorizationRequest(new SecurityContext(SecurityHosts.Alice("editor"), null), PermissionPolicy.NameFor(SecurityHosts.OrdersRead.Code)), CancellationToken.None);
        Assert.False(result.IsAllowed);
        Assert.Equal(SecurityErrors.PermissionMissing, result.Reason?.Code);
        Assert.Single(edition.Calls);
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
            return AuthorizationResult.Deny(Error.Literal("ORDERS_DENIED_001", "Not the owner."));
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

    /// <summary>
    /// 把判定委托给回调的策略，用于观察中间件传入的请求。
    /// </summary>
    private sealed class RecordingPolicy(string name, Func<AuthorizationRequest, AuthorizationResult> evaluate) : IPolicy
    {
        /// <inheritdoc />
        public string Name => name;

        /// <inheritdoc />
        public ValueTask<AuthorizationResult> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(evaluate(request));
    }
}
