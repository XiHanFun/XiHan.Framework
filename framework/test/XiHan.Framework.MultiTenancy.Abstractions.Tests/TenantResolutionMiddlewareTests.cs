// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.MultiTenancy.Abstractions.Resolution;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests;

/// <summary>
/// <see cref="TenantResolutionMiddleware"/> 的单元测试
/// </summary>
/// <remarks>
/// 解析、格式与存储校验、默认租户、必须带租户，以及解析器顺序
/// </remarks>
public sealed class TenantResolutionMiddlewareTests
{
    /// <summary>
    /// 固定租户集合的只读存储
    /// </summary>
    private sealed class DictionaryTenantStore(params TenantInfo[] tenants) : ITenantStore
    {
        /// <summary>
        /// 按 ID 索引的租户，区分大小写
        /// </summary>
        private readonly Dictionary<string, TenantInfo> _tenants = tenants.ToDictionary(static t => t.Id, StringComparer.Ordinal);

        /// <summary>
        /// 按 ID 查找
        /// </summary>
        public ValueTask<Result<Maybe<TenantInfo>>> FindAsync(string tenantId, CancellationToken cancellationToken)
            => ValueTask.FromResult(Failure is { } failure
                ? Result.Failure<Maybe<TenantInfo>>(failure)
                : Result.Success(_tenants.TryGetValue(tenantId, out var tenant) ? Maybe.Some(tenant) : default));

        /// <summary>
        /// 不为 <see langword="null"/> 时查找返回的失败，模拟存储不可达
        /// </summary>
        public Error? Failure { get; set; }

        /// <summary>
        /// 一页列出全部租户
        /// </summary>
        public ValueTask<Result<TenantPage>> ListAsync(TenantQuery query, CancellationToken cancellationToken)
            => ValueTask.FromResult(Result.Success(new TenantPage { Items = [.. _tenants.Values.Where(tenant => !query.EnabledOnly || tenant.IsEnabled)] }));
    }

    /// <summary>
    /// 总是返回固定租户 ID 的解析器
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 表示总是解析不到
    /// </remarks>
    private sealed class FixedResolver(string? tenantId) : ITenantResolver
    {
        /// <summary>
        /// 不看上下文，直接返回构造时给定的值
        /// </summary>
        public ValueTask<Result<Maybe<string>>> ResolveAsync(InvocationContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(Result.Success(tenantId is null ? Maybe.None<string>() : Maybe.Some(tenantId)));
    }

    /// <summary>
    /// 拒绝调用的解析器，记录自己是否被调用
    /// </summary>
    private sealed class RejectingResolver(Error error) : ITenantResolver
    {
        /// <summary>
        /// 是否被调用过
        /// </summary>
        public bool Called { get; private set; }

        /// <summary>
        /// 不看上下文，直接返回构造时给定的失败
        /// </summary>
        public ValueTask<Result<Maybe<string>>> ResolveAsync(InvocationContext context, CancellationToken cancellationToken)
        {
            Called = true;
            return ValueTask.FromResult(Result.Failure<Maybe<string>>(error));
        }
    }

    /// <summary>
    /// 启用且带显示名的租户
    /// </summary>
    private static readonly TenantInfo _acme = new() { Id = "acme", DisplayName = "Acme" };

    /// <summary>
    /// 已禁用的租户
    /// </summary>
    private static readonly TenantInfo _dormant = new() { Id = "dormant", IsEnabled = false };

    /// <summary>
    /// 装配带存储、选项与给定解析器的上下文
    /// </summary>
    /// <remarks>
    /// <paramref name="baggageTenant"/> 不为 <see langword="null"/> 时放入租户 Baggage。返回的数组用于记录处理器是否被执行
    /// </remarks>
    private static (InvocationContext Context, bool[] Reached) Arrange(MultiTenancyOptions options, string? baggageTenant = null, params ITenantResolver[] resolvers)
        => Arrange(new DictionaryTenantStore(_acme, _dormant), options, baggageTenant, resolvers);

    /// <summary>
    /// 装配给定存储、选项与解析器的上下文
    /// </summary>
    private static (InvocationContext Context, bool[] Reached) Arrange(DictionaryTenantStore store, MultiTenancyOptions options, string? baggageTenant, params ITenantResolver[] resolvers)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantStore>(store);
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
    /// 把到达标记置为 <see langword="true"/> 的处理器
    /// </summary>
    private static PipelineStep Handler(bool[] reached) => _ =>
    {
        reached[0] = true;
        return ValueTask.CompletedTask;
    };

    [Fact]
    public async Task UnavailableStore_FailsTheCall_InsteadOfReportingTheTenantMissing()
    {
        // 存储读不到不是没有：故障原样返回（503），不当作租户不存在（404），也不进处理器
        var store = new DictionaryTenantStore(_acme) { Failure = Error.Unavailable(MultiTenancyErrors.StoreUnavailable, MultiTenancyMessages.StoreUnavailable) };
        var (context, reached) = Arrange(store, new MultiTenancyOptions(), "acme", new BaggageTenantResolver());

        await new TenantResolutionMiddleware(context.Services.GetRequiredService<MultiTenancyOptions>()).InvokeAsync(context, Handler(reached));

        Assert.False(reached[0]);
        Assert.Equal(MultiTenancyErrors.StoreUnavailable, context.FindOutcome().ValueOrThrow().Error?.Code);
        Assert.Null(context.GetSecurity().Tenant);
    }

    [Fact]
    public async Task ResolvedAndEnabled_SetsTenantWithDisplayName()
    {
        var (context, reached) = Arrange(new MultiTenancyOptions(), baggageTenant: "acme", new BaggageTenantResolver());

        await new TenantResolutionMiddleware(context.Services.GetRequiredService<MultiTenancyOptions>()).InvokeAsync(context, Handler(reached));

        Assert.True(reached[0]);
        Assert.Equal("acme", context.GetSecurity().Tenant?.Id);
        Assert.Equal("Acme", context.GetSecurity().Tenant?.DisplayName);
        Assert.False(context.FindOutcome().HasValue);
    }

    [Theory]
    [InlineData("ghost", MultiTenancyErrors.NotFound)]
    [InlineData("dormant", MultiTenancyErrors.Disabled)]
    [InlineData("bad id", MultiTenancyErrors.InvalidTenantId)]
    // 大写不合文法：修之前它是另一个租户，在不区分大小写的排序规则下读写的却是 acme 的行（ADR-0196）
    [InlineData("ACME", MultiTenancyErrors.InvalidTenantId)]
    [InlineData("acme\n", MultiTenancyErrors.InvalidTenantId)]
    public async Task UnknownDisabledOrMalformed_IsRejectedWithoutReachingHandler(string tenantId, string expectedCode)
    {
        // 经解析器送入，而不是 Baggage：带换行等不可接受字符的 Baggage 值在建上下文时就被丢弃（ADR-0204），到不了这里
        var (context, reached) = Arrange(new MultiTenancyOptions(), baggageTenant: null, new FixedResolver(tenantId));

        await new TenantResolutionMiddleware(new MultiTenancyOptions()).InvokeAsync(context, Handler(reached));

        Assert.False(reached[0]);
        Assert.Equal(expectedCode, context.FindOutcome().ValueOrThrow().Error?.Code);
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
        Assert.Equal(MultiTenancyErrors.MissingTenant, context.FindOutcome().ValueOrThrow().Error?.Code);
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
        Assert.Equal(MultiTenancyErrors.Disabled, rejected.FindOutcome().ValueOrThrow().Error?.Code);
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
    public async Task RejectingResolver_EndsTheCall_WithItsError_AndLaterResolversAreNotAsked()
    {
        // 拒绝不往下问：优先级更低的 Baggage 不能绕过拒绝，默认租户也不顶替
        var rejection = Error.Denied(MultiTenancyErrors.Disabled, MultiTenancyMessages.TenantDisabled, MessageArguments.Of(("tenantId", "acme")));
        var rejecting = new RejectingResolver(rejection);
        var later = new RejectingResolver(Error.Unavailable(MultiTenancyErrors.StoreUnavailable, MultiTenancyMessages.StoreUnavailable));
        var options = new MultiTenancyOptions { DefaultTenant = "acme" };
        var (context, reached) = Arrange(options, baggageTenant: "acme", new FixedResolver(null), rejecting, later, new BaggageTenantResolver());

        await new TenantResolutionMiddleware(options).InvokeAsync(context, Handler(reached));

        Assert.False(reached[0]);
        Assert.True(rejecting.Called);
        Assert.False(later.Called);
        Assert.Same(rejection, context.FindOutcome().ValueOrThrow().Error);
        Assert.Null(context.GetSecurity().Tenant);
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
        anonymous.SetSecurity(new SecurityContext(Principal.Anonymous with { Claims = [new("tenant", "dormant")] }, null));
        await new TenantResolutionMiddleware(new MultiTenancyOptions()).InvokeAsync(anonymous, Handler(reachedAnonymous));
        Assert.Equal("acme", anonymous.GetSecurity().Tenant?.Id);
    }

    [Fact]
    public async Task ClaimResolver_RejectsConflictingTenantClaims_AndCollapsesDuplicates()
    {
        // 加固（ADR-0218）：原先取第一个声明，签发方给出两个不同的租户时调用落进其中任意一个
        var (conflicting, reachedConflicting) = Arrange(new MultiTenancyOptions(), baggageTenant: null, new ClaimTenantResolver(new MultiTenancyOptions()));
        conflicting.SetSecurity(new SecurityContext(new Principal("u1", "test") { Claims = [new("tenant", "acme"), new("tenant", "dormant")] }, null));
        await new TenantResolutionMiddleware(new MultiTenancyOptions()).InvokeAsync(conflicting, Handler(reachedConflicting));
        Assert.False(reachedConflicting[0]);
        Assert.Equal(MultiTenancyErrors.ClaimsConflict, conflicting.FindOutcome().ValueOrThrow().Error?.Code);
        Assert.Null(conflicting.GetSecurity().Tenant);

        // 同一个值出现两次不算矛盾
        var (duplicated, reachedDuplicated) = Arrange(new MultiTenancyOptions(), baggageTenant: null, new ClaimTenantResolver(new MultiTenancyOptions()));
        duplicated.SetSecurity(new SecurityContext(new Principal("u1", "test") { Claims = [new("tenant", "acme"), new("tenant", "acme")] }, null));
        await new TenantResolutionMiddleware(new MultiTenancyOptions()).InvokeAsync(duplicated, Handler(reachedDuplicated));
        Assert.True(reachedDuplicated[0]);
        Assert.Equal("acme", duplicated.GetSecurity().Tenant?.Id);
    }
}
