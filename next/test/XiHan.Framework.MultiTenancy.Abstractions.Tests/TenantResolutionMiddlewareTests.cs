// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests;

/// <summary>
/// <see cref="TenantResolutionMiddleware"/> 的单元测试：解析、格式与存储校验、默认租户、必须带租户，以及解析器顺序。
/// </summary>
public sealed class TenantResolutionMiddlewareTests
{
    /// <summary>
    /// 固定租户集合的只读存储。
    /// </summary>
    private sealed class DictionaryTenantStore(params TenantInfo[] tenants) : ITenantStore
    {
        /// <summary>
        /// 按 ID 索引的租户，区分大小写。
        /// </summary>
        private readonly Dictionary<string, TenantInfo> _tenants = tenants.ToDictionary(static t => t.Id, StringComparer.Ordinal);

        /// <summary>
        /// 按 ID 查找。
        /// </summary>
        public ValueTask<Maybe<TenantInfo>> FindAsync(string tenantId, CancellationToken cancellationToken)
            => ValueTask.FromResult(_tenants.TryGetValue(tenantId, out var tenant) ? Maybe.Some(tenant) : default);

        /// <summary>
        /// 枚举全部租户。
        /// </summary>
        public ValueTask<IReadOnlyList<TenantInfo>> ListAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<TenantInfo>>(_tenants.Values.ToList());
    }

    /// <summary>
    /// 总是返回固定租户 ID 的解析器；<see langword="null"/> 表示总是解析不到。
    /// </summary>
    private sealed class FixedResolver(string? tenantId) : ITenantResolver
    {
        /// <summary>
        /// 不看上下文，直接返回构造时给定的值。
        /// </summary>
        public ValueTask<Maybe<string>> ResolveAsync(InvocationContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(tenantId is null ? default : Maybe.Some(tenantId));
    }

    /// <summary>
    /// 启用且带显示名的租户。
    /// </summary>
    private static readonly TenantInfo _acme = new() { Id = "acme", DisplayName = "Acme" };

    /// <summary>
    /// 已禁用的租户。
    /// </summary>
    private static readonly TenantInfo _dormant = new() { Id = "dormant", IsEnabled = false };

    /// <summary>
    /// 装配带存储、选项与给定解析器的上下文；<paramref name="baggageTenant"/> 不为 <see langword="null"/> 时放入租户 Baggage。返回的数组用于记录处理器是否被执行。
    /// </summary>
    private static (InvocationContext Context, bool[] Reached) Arrange(MultiTenancyOptions options, string? baggageTenant = null, params ITenantResolver[] resolvers)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantStore>(new DictionaryTenantStore(_acme, _dormant));
        services.AddSingleton(options);
        foreach (var resolver in resolvers)
        {
            services.AddSingleton(resolver);
        }

        var provider = services.BuildServiceProvider();
        var baggage = baggageTenant is null ? null : new Dictionary<string, string> { [BaggageKeys.Tenant] = baggageTenant };
        return (TestInvocationContext.Create(provider, baggage: baggage), new bool[1]);
    }

    /// <summary>
    /// 把到达标记置为 <see langword="true"/> 的处理器。
    /// </summary>
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
