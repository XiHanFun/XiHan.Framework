// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Frozen;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authentication;
using XiHan.Framework.Security.Abstractions.Authorization;
using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Security.Database.Grants;
using XiHan.Framework.Security.Database.Tests.Fixtures;
using XiHan.Framework.Security.InMemory;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.Database.Tests;

/// <summary>
/// 数据库授权特性与内存安全特性同时装配。
/// </summary>
/// <remarks>
/// 本包顶替授权、内存实现只剩 ApiKey 认证；操作按数据库里的授予放行或拒绝；批量授予 / 撤销的逐项结果与本地化文案；授予缓存的命中、失效、过期与容量；选项解析。
/// </remarks>
public sealed class DatabaseSecurityTests
{
    /// <summary>
    /// 两个 ApiKey 主体（alice 带角色 editor，bob 没有角色）。
    /// </summary>
    /// <remarks>
    /// 内存授予配置故意给 editor 授了 orders.write——数据库授权生效时它不起作用。
    /// </remarks>
    private static readonly KeyValuePair<string, string?>[] _configuration =
    [
        new("XiHan:Security:InMemory:Principals:alice:ApiKey", "dev-alice-key"),
        new("XiHan:Security:InMemory:Principals:alice:Roles", "editor"),
        new("XiHan:Security:InMemory:Principals:bob:ApiKey", "dev-bob-key"),
        new("XiHan:Security:InMemory:Grants:role:editor", WriteOrderOperation.Permission),
    ];

    /// <summary>
    /// 用户 alice。
    /// </summary>
    private static readonly GrantSubject _alice = GrantSubject.ForUser("alice");

    /// <summary>
    /// 用户 bob。
    /// </summary>
    private static readonly GrantSubject _bob = GrantSubject.ForUser("bob");

    /// <summary>
    /// 角色 editor。
    /// </summary>
    private static readonly GrantSubject _editor = GrantSubject.ForRole("editor");

    [Fact]
    public void Manifest_ProvidesOnlyAuthorization_AndDeclaresNoHealth()
    {
        var feature = new DatabaseSecurityFeature();

        Assert.Equal(("xihan.security.database", ManifestRole.Provider, ManifestKind.Provider), (feature.Manifest.Id, feature.Manifest.Role, feature.Manifest.Kind));
        Assert.Equal([SecurityCapabilities.Authorization], feature.Manifest.Provides.Select(static capability => capability.Id));
        Assert.Equal(["kernel.hosting", "persistence.relational"], feature.Manifest.Requires.Select(static requirement => requirement.Id).Order(StringComparer.Ordinal));
        Assert.Equal([(typeof(AuthorizationMiddleware).FullName!, PipelineStage.Authorize)], feature.Manifest.Contributes.Pipeline.Select(static p => (p.Middleware, p.Stage)));
        Assert.Equal(SecurityOptions.ConfigurationSection, feature.Manifest.Contributes.Config?.Section);
        Assert.Empty(feature.Manifest.Contributes.Health);
    }

    [Fact]
    public async Task Feature_ReplacesAuthorization_AndAuthorizesOperationsFromDatabaseGrants()
    {
        await using var host = await StartAsync();
        Assert.Equal([SecurityCapabilities.Authorization], host.App.Graph["xihan.security.database"].SelectedCapabilities);
        Assert.Equal([SecurityCapabilities.Authentication], host.App.Graph["xihan.security.inmemory"].SelectedCapabilities);
        Assert.IsType<DatabasePermissionGrantStore>(host.Services.GetRequiredService<IPermissionGrantStore>());
        Assert.Null(host.Services.GetService<InMemoryPermissionGrantStore>());
        Assert.Contains(nameof(AuthenticationMiddleware), host.App.DescribePipeline(), StringComparison.Ordinal);
        Assert.Contains(nameof(AuthorizationMiddleware), host.App.DescribePipeline(), StringComparison.Ordinal);

        // 数据库里没有授予：内存配置里给 editor 的授予不再起作用
        Assert.Equal(SecurityErrors.PermissionMissing, (await WriteOrderAsync(host, "dev-alice-key")).Error?.Code);
        Assert.Equal(SecurityErrors.NotAuthenticated, (await WriteOrderAsync(host, null)).Error?.Code);

        // 直接授予用户
        Assert.All(await GrantAsync(host, new PermissionGrant(_alice, WriteOrderOperation.Permission)), static r => Assert.True(r.Outcome.IsSuccess));
        var written = await WriteOrderAsync(host, "dev-alice-key");
        Assert.Equal(("alice", null), (written.ValueOrThrow().Principal, written.ValueOrThrow().Tenant));
        Assert.All(await RevokeAsync(host, new PermissionGrant(_alice, WriteOrderOperation.Permission)), static r => Assert.True(r.Outcome.IsSuccess));
        Assert.Equal(SecurityErrors.PermissionMissing, (await WriteOrderAsync(host, "dev-alice-key")).Error?.Code);

        // 经角色授予：alice 的 ApiKey 主体带 role=editor 声明，bob 没有角色
        Assert.All(await GrantAsync(host, new PermissionGrant(_editor, WriteOrderOperation.Permission)), static r => Assert.True(r.Outcome.IsSuccess));
        Assert.True((await WriteOrderAsync(host, "dev-alice-key")).IsSuccess);
        Assert.Equal(SecurityErrors.PermissionMissing, (await WriteOrderAsync(host, "dev-bob-key")).Error?.Code);

        // 租户专属授予只对该租户放行
        Assert.All(await GrantAsync(host, new PermissionGrant(_bob, WriteOrderOperation.Permission, "acme")), static r => Assert.True(r.Outcome.IsSuccess));
        var scoped = await WriteOrderAsync(host, "dev-bob-key", tenant: "acme");
        Assert.Equal(("bob", "acme"), (scoped.ValueOrThrow().Principal, scoped.ValueOrThrow().Tenant));
        Assert.Equal(SecurityErrors.PermissionMissing, (await WriteOrderAsync(host, "dev-bob-key", tenant: "other")).Error?.Code);
        Assert.Equal(SecurityErrors.PermissionMissing, (await WriteOrderAsync(host, "dev-bob-key")).Error?.Code);
    }

    [Fact]
    public async Task RecordStore_GrantsIdempotently_RevokesPerItem_AndLists()
    {
        await using var host = await StartAsync();
        var global = new PermissionGrant(_alice, WriteOrderOperation.Permission);
        var scoped = new PermissionGrant(_alice, WriteOrderOperation.Permission, "acme");
        var wildcard = new PermissionGrant(_editor, PermissionGrant.All);

        // 同一批里的重复项与已存在的项都成功，只落一行
        var granted = await GrantAsync(host, global, global, scoped, wildcard);
        Assert.All(granted, static r => Assert.True(r.Outcome.IsSuccess));
        Assert.Equal([global, global, scoped, wildcard], granted.Select(static r => r.Grant));
        Assert.All(await GrantAsync(host, global), static r => Assert.True(r.Outcome.IsSuccess));

        // 按主体种类、主体 ID、权限码、租户排序；全局行的租户列是空字符串，换算回契约是 null
        var rows = await ListAsync(host, null, null, 0, 10);
        Assert.Equal([wildcard, global, scoped], rows.Select(static r => r.ToGrant()));
        Assert.Equal([PermissionGrantRecord.GlobalTenantId, "acme"], rows.Skip(1).Select(static r => r.TenantId));
        Assert.All(rows, static r => Assert.NotEqual(default, r.CreatedAt));
        Assert.Equal([global, scoped], (await ListAsync(host, _alice, null, 0, 10)).Select(static r => r.ToGrant()));
        Assert.Equal([scoped], (await ListAsync(host, null, "acme", 0, 10)).Select(static r => r.ToGrant()));
        Assert.Equal([global], (await ListAsync(host, null, null, 1, 1)).Select(static r => r.ToGrant()));

        // 撤销逐项：存在的成功，同一批第二次与从未存在的都不存在，错误能按调用方语言翻译
        var missing = new PermissionGrant(GrantSubject.ForUser("carol"), "reports.read");
        var revoked = await RevokeAsync(host, global, global, missing, new PermissionGrant(_alice, "reports.read", "acme"));
        Assert.True(revoked[0].Outcome.IsSuccess);
        Assert.All(revoked.Skip(1), static r => Assert.Equal(SecurityErrors.GrantNotFound, r.Outcome.Error?.Code));
        var localizer = host.Services.GetRequiredService<IMessageLocalizer>();
        Assert.Equal("Grant of 'reports.read' to 'user:carol' in tenant '*' was not found.", localizer.Localize(revoked[2].Outcome.Error!, "en-US"));
        Assert.Equal("租户 '*' 中对 'user:carol' 的权限 'reports.read' 授予不存在。", localizer.Localize(revoked[2].Outcome.Error!, "zh-CN"));
        Assert.Equal("租户 'acme' 中对 'user:alice' 的权限 'reports.read' 授予不存在。", localizer.Localize(revoked[3].Outcome.Error!, "zh-CN"));
        Assert.Equal([wildcard, scoped], (await ListAsync(host, null, null, 0, 10)).Select(static r => r.ToGrant()));

        // 空集合不触后端；空白租户是编程错误
        var empty = await host.InvokeAsync(async context =>
        {
            var store = context.Services.GetRequiredService<PermissionGrantRecordStore>();
            Assert.Empty(await store.GrantAsync([], context.CancellationToken));
            Assert.Empty(await store.RevokeAsync([], context.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(async () => await store.GrantAsync([new PermissionGrant(_alice, "reports.read", " ")], context.CancellationToken));
        });
        empty.ThrowIfFailure();
    }

    [Fact]
    public async Task Cache_ServesHitsWithoutTheDatabase_UntilInvalidatedOrExpired()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero));
        await using var host = await StartAsync(clock);
        var store = Assert.IsType<DatabasePermissionGrantStore>(host.Services.GetRequiredService<IPermissionGrantStore>());

        Assert.Empty(await store.GetGrantsAsync(_alice, null, CancellationToken.None));
        Assert.Equal(1, store.CachedEntryCount);

        // 绕过记录存储直接写行（不触发失效）：缓存命中，不再查库
        await InsertRowAsync(host, _alice, WriteOrderOperation.Permission);
        Assert.Empty(await store.GetGrantsAsync(_alice, null, CancellationToken.None));

        // 按主体失效后重新查库
        store.Invalidate(_alice);
        Assert.Equal(0, store.CachedEntryCount);
        Assert.Equal([WriteOrderOperation.Permission], await store.GetGrantsAsync(_alice, null, CancellationToken.None));

        // 过了存活时间也重新查库
        await InsertRowAsync(host, _alice, "reports.read");
        Assert.Single(await store.GetGrantsAsync(_alice, null, CancellationToken.None));
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.Single(await store.GetGrantsAsync(_alice, null, CancellationToken.None));
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(2, (await store.GetGrantsAsync(_alice, null, CancellationToken.None)).Count);

        // 不给主体清空全部；经记录存储写入的立刻可见
        Assert.Empty(await store.GetGrantsAsync(_bob, "acme", CancellationToken.None));
        Assert.Equal(2, store.CachedEntryCount);
        store.Invalidate();
        Assert.Equal(0, store.CachedEntryCount);
        Assert.Empty(await store.GetGrantsAsync(_bob, "acme", CancellationToken.None));
        Assert.All(await GrantAsync(host, new PermissionGrant(_bob, "reports.read", "acme")), static r => Assert.True(r.Outcome.IsSuccess));
        Assert.Equal(["reports.read"], await store.GetGrantsAsync(_bob, "acme", CancellationToken.None));
        Assert.Empty(await store.GetGrantsAsync(_bob, "other", CancellationToken.None));
        // 返回的是冻结集合：不可变，调用方改不了缓存
        Assert.IsAssignableFrom<FrozenSet<string>>(await store.GetGrantsAsync(_bob, "acme", CancellationToken.None));
    }

    [Fact]
    public async Task Cache_ClearsWhenFull_AndNeverExpiresWithoutTimeToLive()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero));
        await using var host = await StartAsync(clock, new("XiHan:Security:Database:CacheSize", "2"), new("XiHan:Security:Database:CacheTimeToLiveSeconds", "0"));
        var store = Assert.IsType<DatabasePermissionGrantStore>(host.Services.GetRequiredService<IPermissionGrantStore>());
        Assert.Null(host.Services.GetRequiredService<DatabaseSecurityOptions>().CacheTimeToLive);

        Assert.Empty(await store.GetGrantsAsync(_alice, null, CancellationToken.None));
        Assert.Empty(await store.GetGrantsAsync(_alice, "acme", CancellationToken.None));
        Assert.Equal(2, store.CachedEntryCount);

        // 不过期：一天后仍是缓存里的旧结果
        await InsertRowAsync(host, _alice, WriteOrderOperation.Permission);
        clock.Advance(TimeSpan.FromDays(1));
        Assert.Empty(await store.GetGrantsAsync(_alice, null, CancellationToken.None));

        // 第三个条目进来时已满：整体清空后再写入，之前的主体重新查库
        Assert.Empty(await store.GetGrantsAsync(_bob, null, CancellationToken.None));
        Assert.Equal(1, store.CachedEntryCount);
        Assert.Equal([WriteOrderOperation.Permission], await store.GetGrantsAsync(_alice, null, CancellationToken.None));
        Assert.Equal(2, store.CachedEntryCount);
    }

    [Fact]
    public void Options_ParseAndValidate()
    {
        var defaults = new DatabaseSecurityOptions();
        Assert.Equal((10_000, TimeSpan.FromSeconds(30)), (defaults.CacheSize, defaults.CacheTimeToLive));

        var options = DatabaseSecurityOptions.FromConfiguration(Configuration(("CacheSize", "5"), ("CacheTimeToLiveSeconds", "1.5")));
        Assert.Equal((5, TimeSpan.FromSeconds(1.5)), (options.CacheSize, options.CacheTimeToLive));
        Assert.Null(DatabaseSecurityOptions.FromConfiguration(Configuration(("CacheTimeToLiveSeconds", "0"))).CacheTimeToLive);

        Assert.Throws<ArgumentException>(static () => DatabaseSecurityOptions.FromConfiguration(Configuration(("CacheSize", "0"))));
        Assert.Throws<ArgumentException>(static () => DatabaseSecurityOptions.FromConfiguration(Configuration(("CacheSize", "-1"))));
        Assert.Throws<ArgumentException>(static () => DatabaseSecurityOptions.FromConfiguration(Configuration(("CacheTimeToLiveSeconds", "-1"))));
        Assert.Throws<ArgumentException>(static () => DatabaseSecurityOptions.FromConfiguration(Configuration(("CacheTimeToLiveSeconds", "soon"))));
        Assert.Throws<ArgumentOutOfRangeException>(static () => new DatabaseSecurityOptions { CacheSize = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new DatabaseSecurityOptions { CacheTimeToLive = TimeSpan.Zero });
    }

    /// <summary>
    /// 启动装配了内存持久化、内存本地化、内存安全（认证）、数据库授权与订单特性的宿主。
    /// </summary>
    /// <remarks>
    /// 可换时钟、追加配置。
    /// </remarks>
    private static Task<TestHost> StartAsync(FakeTimeProvider? clock = null, params KeyValuePair<string, string?>[] extra)
        => TestHost.StartAsync(
            static b => b.UseFeature<InMemoryPersistenceFeature>().UseFeature<InMemoryLocalizationFeature>().UseFeature<InMemorySecurityFeature>().UseFeature<DatabaseSecurityFeature>().UseFeature<OrdersFeature>(),
            _configuration.Concat(extra),
            services =>
            {
                if (clock is not null)
                {
                    services.AddSingleton<TimeProvider>(clock);
                }
            });

    /// <summary>
    /// 以可选的 ApiKey 凭据与租户经完整管道调用写订单操作。
    /// </summary>
    private static async Task<Result<OrderResponse>> WriteOrderAsync(TestHost host, string? apiKey, string? tenant = null)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None);
        if (apiKey is not null)
        {
            context.SetCredential(new AuthenticationCredential(ApiKeyAuthenticationHandler.SchemeName, apiKey));
        }

        if (tenant is not null)
        {
            context.SetTenant(new TenantContext(tenant));
        }

        return await scope.ServiceProvider.GetRequiredService<IOperationInvoker>().InvokeAsync<OrderRequest, OrderResponse>(context, WriteOrderOperation.Id, new OrderRequest("o-1"));
    }

    /// <summary>
    /// 在一次调用（Transaction 阶段内）里批量授予。
    /// </summary>
    private static async Task<IReadOnlyList<PermissionGrantResult>> GrantAsync(TestHost host, params PermissionGrant[] grants)
    {
        IReadOnlyList<PermissionGrantResult> results = [];
        (await host.InvokeAsync(async context => results = await context.Services.GetRequiredService<PermissionGrantRecordStore>().GrantAsync(grants, context.CancellationToken))).ThrowIfFailure();
        return results;
    }

    /// <summary>
    /// 在一次调用里批量撤销。
    /// </summary>
    private static async Task<IReadOnlyList<PermissionGrantResult>> RevokeAsync(TestHost host, params PermissionGrant[] grants)
    {
        IReadOnlyList<PermissionGrantResult> results = [];
        (await host.InvokeAsync(async context => results = await context.Services.GetRequiredService<PermissionGrantRecordStore>().RevokeAsync(grants, context.CancellationToken))).ThrowIfFailure();
        return results;
    }

    /// <summary>
    /// 在一次调用里列出授予行。
    /// </summary>
    private static async Task<IReadOnlyList<PermissionGrantRecord>> ListAsync(TestHost host, GrantSubject? subject, string? tenantId, int skip, int take)
    {
        IReadOnlyList<PermissionGrantRecord> rows = [];
        (await host.InvokeAsync(async context => rows = await context.Services.GetRequiredService<PermissionGrantRecordStore>().ListAsync(subject, tenantId, skip, take, context.CancellationToken))).ThrowIfFailure();
        return rows;
    }

    /// <summary>
    /// 绕过记录存储直接向仓储写一行全局授予：不触发缓存失效，用来证明命中时不查库。
    /// </summary>
    private static async Task InsertRowAsync(TestHost host, GrantSubject subject, string permission)
    {
        var inserted = await host.InvokeAsync(async context =>
        {
            var repository = context.Services.GetRequiredService<IRepository<PermissionGrantRecord, Guid>>();
            var row = new PermissionGrantRecord { Id = Guid.NewGuid(), SubjectKind = subject.Kind, SubjectId = subject.Id, Permission = permission };
            (await repository.AddAsync(row, context.CancellationToken)).ThrowIfFailure();
        });
        inserted.ThrowIfFailure();
    }

    /// <summary>
    /// 只含 <c>XiHan:Security:Database</c> 节下给定键值的配置。
    /// </summary>
    private static IConfiguration Configuration(params (string Key, string Value)[] pairs)
        => new ConfigurationBuilder().AddInMemoryCollection(pairs.Select(static p => new KeyValuePair<string, string?>($"{DatabaseSecurityOptions.ConfigurationSection}:{p.Key}", p.Value))).Build();
}
