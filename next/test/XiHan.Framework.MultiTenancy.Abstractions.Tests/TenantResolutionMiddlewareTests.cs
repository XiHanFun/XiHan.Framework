// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests;

public sealed class TenantResolutionMiddlewareTests
{
    private sealed class DictionaryTenantStore(params TenantInfo[] tenants) : ITenantStore
    {
        private readonly Dictionary<string, TenantInfo> _tenants = tenants.ToDictionary(static t => t.Id, StringComparer.Ordinal);

        public ValueTask<Maybe<TenantInfo>> FindAsync(string tenantId, CancellationToken cancellationToken)
            => ValueTask.FromResult(_tenants.TryGetValue(tenantId, out var tenant) ? Maybe.Some(tenant) : default);

        public ValueTask<IReadOnlyList<TenantInfo>> ListAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<TenantInfo>>(_tenants.Values.ToList());
    }

    private sealed class FixedResolver(string? tenantId) : ITenantResolver
    {
        public ValueTask<Maybe<string>> ResolveAsync(InvocationContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(tenantId is null ? default : Maybe.Some(tenantId));
    }

    private static readonly TenantInfo Acme = new() { Id = "acme", DisplayName = "Acme" };
    private static readonly TenantInfo Dormant = new() { Id = "dormant", IsEnabled = false };

    private static (InvocationContext Context, bool[] Reached) Arrange(MultiTenancyOptions options, string? baggageTenant = null, params ITenantResolver[] resolvers)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantStore>(new DictionaryTenantStore(Acme, Dormant));
        services.AddSingleton(options);
        foreach (var resolver in resolvers)
        {
            services.AddSingleton(resolver);
        }

        var provider = services.BuildServiceProvider();
        var baggage = baggageTenant is null ? null : new Dictionary<string, string> { [BaggageKeys.Tenant] = baggageTenant };
        return (TestInvocationContext.Create(provider, baggage: baggage), new bool[1]);
    }

    private static PipelineStep Handler(bool[] reached) => _ =>
    {
        reached[0] = true;
        return ValueTask.CompletedTask;
    };

    [Fact]
    public async Task ResolvedAndEnabled_SetsTenantWithDisplayName()
    {
        var (context, reached) = Arrange(new MultiTenancyOptions(), baggageTenant: "acme", new BaggageTenantResolver());

        await new TenantResolutionMiddleware(context.Services.GetRequiredService<MultiTenancyOptions>()).InvokeAsync(context, Handler(reached));

        Assert.True(reached[0]);
        Assert.Equal("acme", context.GetSecurity().Tenant?.Id);
        Assert.Equal("Acme", context.GetSecurity().Tenant?.DisplayName);
        Assert.False(context.GetOutcome().HasValue);
    }

    [Theory]
    [InlineData("ghost", MultiTenancyErrors.NotFound)]
    [InlineData("dormant", MultiTenancyErrors.Disabled)]
    [InlineData("bad id", MultiTenancyErrors.InvalidTenantId)]
    public async Task UnknownDisabledOrMalformed_IsRejectedWithoutReachingHandler(string tenantId, string expectedCode)
    {
        var (context, reached) = Arrange(new MultiTenancyOptions(), baggageTenant: tenantId, new BaggageTenantResolver());

        await new TenantResolutionMiddleware(new MultiTenancyOptions()).InvokeAsync(context, Handler(reached));

        Assert.False(reached[0]);
        Assert.Equal(expectedCode, context.GetOutcome().ValueOrThrow().Error?.Code);
        Assert.Null(context.GetSecurity().Tenant);
    }

    [Fact]
    public async Task NoTenant_PassesThrough_UnlessRequired()
    {
        var (lenient, reachedLenient) = Arrange(new MultiTenancyOptions(), baggageTenant: null, new BaggageTenantResolver());
        await new TenantResolutionMiddleware(new MultiTenancyOptions()).InvokeAsync(lenient, Handler(reachedLenient));
        Assert.True(reachedLenient[0]);
        Assert.Null(lenient.GetSecurity().Tenant);

        var strict = new MultiTenancyOptions { RequireTenant = true };
        var (context, reached) = Arrange(strict, baggageTenant: null, new BaggageTenantResolver());
        await new TenantResolutionMiddleware(strict).InvokeAsync(context, Handler(reached));
        Assert.False(reached[0]);
        Assert.Equal(MultiTenancyErrors.MissingTenant, context.GetOutcome().ValueOrThrow().Error?.Code);
    }

    [Fact]
    public async Task DefaultTenant_IsUsed_AndStillValidated()
    {
        var options = new MultiTenancyOptions { DefaultTenant = "acme", RequireTenant = true };
        var (context, reached) = Arrange(options, baggageTenant: null, new BaggageTenantResolver());
        await new TenantResolutionMiddleware(options).InvokeAsync(context, Handler(reached));
        Assert.True(reached[0]);
        Assert.Equal("acme", context.GetSecurity().Tenant?.Id);

        var disabledDefault = new MultiTenancyOptions { DefaultTenant = "dormant" };
        var (rejected, reachedRejected) = Arrange(disabledDefault, baggageTenant: null, new BaggageTenantResolver());
        await new TenantResolutionMiddleware(disabledDefault).InvokeAsync(rejected, Handler(reachedRejected));
        Assert.False(reachedRejected[0]);
        Assert.Equal(MultiTenancyErrors.Disabled, rejected.GetOutcome().ValueOrThrow().Error?.Code);
    }

    [Fact]
    public async Task WithoutStoreValidation_AcceptsAnyWellFormedTenant()
    {
        var options = new MultiTenancyOptions { ValidateAgainstStore = false };
        var (context, reached) = Arrange(options, baggageTenant: "unknown-but-fine", new BaggageTenantResolver());

        await new TenantResolutionMiddleware(options).InvokeAsync(context, Handler(reached));

        Assert.True(reached[0]);
        Assert.Equal("unknown-but-fine", context.GetSecurity().Tenant?.Id);
        Assert.Null(context.GetSecurity().Tenant?.DisplayName);
    }

    [Fact]
    public async Task Resolvers_RunInRegistrationOrder_FirstHitWins()
    {
        var (context, reached) = Arrange(new MultiTenancyOptions(), baggageTenant: "dormant", new FixedResolver(null), new FixedResolver("acme"), new BaggageTenantResolver());

        await new TenantResolutionMiddleware(new MultiTenancyOptions()).InvokeAsync(context, Handler(reached));

        Assert.True(reached[0]);
        Assert.Equal("acme", context.GetSecurity().Tenant?.Id);
    }

    [Fact]
    public async Task ClaimResolver_UsesAuthenticatedPrincipalClaim_BeforeBaggage()
    {
        var (context, reached) = Arrange(new MultiTenancyOptions(), baggageTenant: "dormant", new ClaimTenantResolver(new MultiTenancyOptions()), new BaggageTenantResolver());
        context.SetSecurity(new SecurityContext(new Principal("u1", "test") { Claims = [new("tenant", "acme")] }, null));

        await new TenantResolutionMiddleware(new MultiTenancyOptions()).InvokeAsync(context, Handler(reached));

        Assert.Equal("acme", context.GetSecurity().Tenant?.Id);
        Assert.Equal("u1", context.GetSecurity().Principal.Id);

        // 未认证主体的声明不作数，退回 Baggage
        var (anonymous, reachedAnonymous) = Arrange(new MultiTenancyOptions(), baggageTenant: "acme", new ClaimTenantResolver(new MultiTenancyOptions()), new BaggageTenantResolver());
        anonymous.SetSecurity(new SecurityContext(new Principal("ghost", null) { Claims = [new("tenant", "dormant")] }, null));
        await new TenantResolutionMiddleware(new MultiTenancyOptions()).InvokeAsync(anonymous, Handler(reachedAnonymous));
        Assert.Equal("acme", anonymous.GetSecurity().Tenant?.Id);
    }
}
