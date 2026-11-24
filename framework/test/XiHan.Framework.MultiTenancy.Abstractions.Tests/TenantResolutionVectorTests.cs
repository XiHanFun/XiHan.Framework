// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.MultiTenancy.Abstractions.Resolution;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests;

/// <summary>
/// 回放 <c>tenant-resolution.json</c>：Tenant 阶段的裁定
/// </summary>
/// <remarks>
/// <c>multitenancy/tenant-resolution.json</c>（RFC-0007「解析」第 1–5 条，ADR-0143、ADR-0192、ADR-0218、ADR-0277）。每个用例是一次调用：照用例装好租户存储、主体、Baggage 与选项，登记缺省的两个解析器（<see cref="ClaimTenantResolver"/> 在前，<see cref="BaggageTenantResolver"/> 在后，与各 Provider 的特性登记的顺序一致），直接调用 <see cref="TenantResolutionMiddleware"/>，看安全上下文里的租户或结束调用的错误码
/// </remarks>
public sealed class TenantResolutionVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly ResolutionVectors _vectors = ConformanceVectors.Load<ResolutionVectors>("tenant-resolution.json");

    [Fact]
    public async Task Middleware_SettlesTheTenantOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Tenants);
        Assert.NotEmpty(_vectors.Cases);
        var failures = new List<string>();
        foreach (var vector in _vectors.Cases)
        {
            var (settled, lookups) = await SettleAsync(vector);
            var expected = Describe(vector.Expected);
            if (settled != expected)
            {
                failures.Add($"{vector.Name}: the call ended with {settled}, the vector has {expected}");
            }

            // 文法先于存储、不核对存储就不读存储：不合文法的 ID 与关掉核对的调用都不该碰到租户存储
            var grammarError = vector.Expected.TryGetProperty("error", out var error) && error.GetString() == MultiTenancyErrors.InvalidTenantId;
            if ((grammarError || !vector.Options.ValidateAgainstStore) && lookups != 0)
            {
                failures.Add($"{vector.Name}: the tenant store was read {lookups} time(s), but the call must not reach it");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_CoverEveryOutcomeAndEveryErrorCode()
    {
        // 防止向量换了写法后某一类一条都没核对还照样通过：带租户、无租户继续、被拒绝三种结局都要有
        var outcomes = _vectors.Cases.Select(static vector => Describe(vector.Expected)).ToList();
        Assert.Contains(outcomes, static outcome => outcome.StartsWith("tenant ", StringComparison.Ordinal));
        Assert.Contains("no tenant", outcomes);

        // Tenant 阶段自己给出的五个错误码，加上原样返回的存储不可达，各至少一条
        var codes = _vectors.Cases.Where(static vector => vector.Expected.TryGetProperty("error", out _)).Select(static vector => vector.Expected.GetProperty("error").GetString()).Distinct().Order(StringComparer.Ordinal);
        Assert.Equal(
            new[] { MultiTenancyErrors.InvalidTenantId, MultiTenancyErrors.MissingTenant, MultiTenancyErrors.ClaimsConflict, MultiTenancyErrors.NotFound, MultiTenancyErrors.Disabled, MultiTenancyErrors.StoreUnavailable }.Order(StringComparer.Ordinal),
            codes);

        // 次序是规范性的：平台调用、声明冲突时还带着 Baggage 或默认租户、存储不可达时的文法错误都要有
        Assert.Contains(_vectors.Cases, static vector => vector.PlatformCall);
        Assert.Contains(_vectors.Cases, static vector => Describe(vector.Expected) == $"error {MultiTenancyErrors.ClaimsConflict}" && vector.Baggage is not null);
        Assert.Contains(_vectors.Cases, static vector => Describe(vector.Expected) == $"error {MultiTenancyErrors.ClaimsConflict}" && vector.Options.DefaultTenant is not null);
        Assert.Contains(_vectors.Cases, static vector => Describe(vector.Expected) == $"error {MultiTenancyErrors.InvalidTenantId}" && vector.StoreUnavailable);
        Assert.Contains(_vectors.Cases, static vector => !vector.Options.ValidateAgainstStore && vector.StoreUnavailable);
    }

    /// <summary>
    /// 照用例开一次调用、走 Tenant 阶段，返回结局的描述与租户存储被读的次数
    /// </summary>
    /// <param name="vector">用例</param>
    private static async Task<(string Settled, int Lookups)> SettleAsync(ResolutionCase vector)
    {
        var options = new MultiTenancyOptions
        {
            DefaultTenant = vector.Options.DefaultTenant,
            RequireTenant = vector.Options.RequireTenant,
            ValidateAgainstStore = vector.Options.ValidateAgainstStore,
        };
        var store = new VectorTenantStore(_vectors.Tenants.Select(static tenant => new TenantInfo { Id = tenant.Id, DisplayName = tenant.DisplayName, IsEnabled = tenant.Enabled }), vector.StoreUnavailable);
        await using var services = new ServiceCollection()
            .AddSingleton<ITenantStore>(store)
            .AddSingleton<ITenantResolver>(new ClaimTenantResolver(options))
            .AddSingleton<ITenantResolver>(new BaggageTenantResolver())
            .BuildServiceProvider();

        var context = TestInvocationContext.Create(services, baggage: vector.Baggage is null ? null : new Dictionary<string, string>(StringComparer.Ordinal) { [BaggageKeys.Tenant] = vector.Baggage });
        var claims = vector.Claims.Select(value => KeyValuePair.Create(options.TenantClaim, value)).ToList();
        context.SetSecurity(new SecurityContext(vector.Authenticated ? new Principal("u1", "test") { Claims = claims } : Principal.Anonymous with { Claims = claims }, null));
        if (vector.PlatformCall)
        {
            context.MarkPlatformCall();
        }

        var reached = false;
        await new TenantResolutionMiddleware(options).InvokeAsync(context, _ =>
        {
            reached = true;
            return ValueTask.CompletedTask;
        });

        // 错误结束调用：不进下一步，安全上下文里没有租户；否则进下一步，租户就是安全上下文里的那个
        var tenant = context.GetSecurity().Tenant;
        if (context.FindOutcome().TryGetValue(out var outcome) && outcome.Error is { } error)
        {
            return (reached || tenant is not null ? $"error {error.Code}, yet the call went on or kept the tenant '{tenant?.Id}'" : $"error {error.Code}", store.Lookups);
        }

        if (!reached)
        {
            return ("neither an error nor the next step", store.Lookups);
        }

        return (tenant is null ? "no tenant" : $"tenant '{tenant.Id}', display name {Quote(tenant.DisplayName)}", store.Lookups);
    }

    /// <summary>
    /// 向量里期望的结局的描述
    /// </summary>
    /// <remarks>
    /// 三选一：<c>error</c> 是结束调用的错误码；<c>tenant</c> 为 <see langword="null"/> 是无租户继续；否则是写进安全上下文的租户 ID 与随之带入的显示名
    /// </remarks>
    /// <param name="expected">用例的 <c>expected</c></param>
    private static string Describe(JsonElement expected)
    {
        if (expected.TryGetProperty("error", out var error))
        {
            return $"error {error.GetString()}";
        }

        var tenant = expected.GetProperty("tenant");
        return tenant.ValueKind == JsonValueKind.Null ? "no tenant" : $"tenant '{tenant.GetString()}', display name {Quote(expected.GetProperty("displayName").GetString())}";
    }

    /// <summary>
    /// 描述里的显示名
    /// </summary>
    /// <param name="displayName">显示名；<see langword="null"/> 表示没有</param>
    private static string Quote(string? displayName) => displayName is null ? "none" : $"'{displayName}'";

    /// <summary>
    /// 向量里的租户存储
    /// </summary>
    /// <remarks>
    /// 不可达时查找返回 <see cref="MultiTenancyErrors.StoreUnavailable"/>，这是存储契约规定的错误；记下被读的次数
    /// </remarks>
    /// <param name="tenants">存储里的租户</param>
    /// <param name="unavailable">存储是否不可达</param>
    private sealed class VectorTenantStore(IEnumerable<TenantInfo> tenants, bool unavailable) : ITenantStore
    {
        /// <summary>
        /// 按 ID 索引的租户，区分大小写
        /// </summary>
        private readonly Dictionary<string, TenantInfo> _tenants = tenants.ToDictionary(static tenant => tenant.Id, StringComparer.Ordinal);

        /// <summary>
        /// 查找被调用的次数
        /// </summary>
        public int Lookups { get; private set; }

        /// <inheritdoc />
        public ValueTask<Result<Maybe<TenantInfo>>> FindAsync(string tenantId, CancellationToken cancellationToken)
        {
            Lookups++;
            return ValueTask.FromResult(unavailable
                ? Result.Failure<Maybe<TenantInfo>>(Error.Unavailable(MultiTenancyErrors.StoreUnavailable, MultiTenancyMessages.StoreUnavailable))
                : Result.Success(_tenants.TryGetValue(tenantId, out var tenant) ? Maybe.Some(tenant) : default));
        }

        /// <inheritdoc />
        public ValueTask<Result<TenantPage>> ListAsync(TenantQuery query, CancellationToken cancellationToken)
            => ValueTask.FromResult(unavailable
                ? Result.Failure<TenantPage>(Error.Unavailable(MultiTenancyErrors.StoreUnavailable, MultiTenancyMessages.StoreUnavailable))
                : Result.Success(new TenantPage { Items = [.. _tenants.Values.Where(tenant => !query.EnabledOnly || tenant.IsEnabled)] }));
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Tenants">租户存储里的租户</param>
    /// <param name="Cases">用例，每个是一次调用</param>
    private sealed record ResolutionVectors(StoredTenant[] Tenants, ResolutionCase[] Cases);

    /// <summary>
    /// 租户存储里的一个租户
    /// </summary>
    /// <param name="Id">租户 ID</param>
    /// <param name="DisplayName">显示名；<see langword="null"/> 表示没有</param>
    /// <param name="Enabled">是否启用</param>
    private sealed record StoredTenant(string Id, string? DisplayName, bool Enabled);

    /// <summary>
    /// 一次调用
    /// </summary>
    /// <param name="Name">名字</param>
    /// <param name="Authenticated">主体是否已认证</param>
    /// <param name="Claims">主体的 <c>tenant</c> 声明的值</param>
    /// <param name="Baggage">Baggage 里的 <c>xihan.tenant</c>；<see langword="null"/> 表示没有</param>
    /// <param name="PlatformCall">进程内的代码是否声明了平台调用</param>
    /// <param name="Options">三个选项</param>
    /// <param name="StoreUnavailable">租户存储是否不可达</param>
    /// <param name="Expected">期望的结局，三选一</param>
    private sealed record ResolutionCase(string Name, bool Authenticated, string[] Claims, string? Baggage, bool PlatformCall, ResolutionOptions Options, bool StoreUnavailable, JsonElement Expected);

    /// <summary>
    /// 一次调用的三个选项
    /// </summary>
    /// <param name="DefaultTenant">默认租户；<see langword="null"/> 表示没有</param>
    /// <param name="RequireTenant">调用是否必须带租户</param>
    /// <param name="ValidateAgainstStore">是否对照存储核对</param>
    private sealed record ResolutionOptions(string? DefaultTenant, bool RequireTenant, bool ValidateAgainstStore);
}
