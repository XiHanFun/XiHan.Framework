// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Caching.Hybrid;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Caching.Redis;
using XiHan.Framework.Kernel;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Security.Database.Grants;
using XiHan.Framework.Security.Database.Tests.Fixtures;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.Database.Tests;

/// <summary>
/// 授予缓存走 ICache：多实例一致与缓存后端故障（ADR-0229）
/// </summary>
/// <remarks>
/// 两个宿主共用一个库与一份缓存时，一个实例的授予与撤销另一个实例立即看到；缓存后端故障时判定直接查库、结果不变，写入时的失效失败让写入回滚，提交之后的失效失败只记日志
/// </remarks>
public sealed class GrantCacheConsistencyTests
{
    /// <summary>
    /// 被授予与撤销的主体
    /// </summary>
    private static readonly GrantSubject _editor = GrantSubject.ForRole("editor");

    /// <summary>
    /// 平台授予
    /// </summary>
    private static readonly PermissionGrant _write = new(_editor, "orders.write");

    /// <summary>
    /// 租户 acme 专属的授予
    /// </summary>
    /// <remarks>
    /// 租户调用看不到平台授予（ADR-0197），按租户读取的用例用它
    /// </remarks>
    private static readonly PermissionGrant _writeInAcme = new(_editor, "orders.write", "acme");

    [Fact]
    public async Task InstancesSharingTheCache_SeeGrantsAndRevocationsWithoutWaitingForExpiry()
    {
        await using var database = new SqliteDatabase();
        var shared = new FaultyCache(NewInMemoryCache());
        var first = await database.StartHostAsync(services: s => s.AddSingleton<ICache>(shared));
        var second = await database.StartHostAsync(services: s => s.AddSingleton<ICache>(shared));
        var reader = second.Services.GetRequiredService<IPermissionGrantStore>();

        // 第二个实例先缓存「没有授予」
        Assert.Empty((await reader.GetGrantsAsync(_editor, null, CancellationToken.None)).ValueOrThrow());

        // 第一个实例授予：第二个实例的下一次判定就看到，不等存活时间
        await AdministerAsync(first, static (store, ct) => store.GrantAsync([_write], ct));
        Assert.Equal(["orders.write"], (await reader.GetGrantsAsync(_editor, null, CancellationToken.None)).ValueOrThrow());

        // 第一个实例撤销：同样立即生效
        await AdministerAsync(first, static (store, ct) => store.RevokeAsync([_write], ct));
        Assert.Empty((await reader.GetGrantsAsync(_editor, null, CancellationToken.None)).ValueOrThrow());

        // 第一个实例清空全部，第二个实例绕过写入方直接写进库的行随之可见
        await InsertRowAsync(second, _editor, "orders.read");
        Assert.Empty((await reader.GetGrantsAsync(_editor, null, CancellationToken.None)).ValueOrThrow());
        await first.Services.GetRequiredService<DatabasePermissionGrantStore>().InvalidateAllAsync(CancellationToken.None);
        Assert.Equal(["orders.read"], (await reader.GetGrantsAsync(_editor, null, CancellationToken.None)).ValueOrThrow());
    }

    [Fact]
    public async Task CacheOutage_ChecksReadTheDatabase_AndNeverTurnIntoMissingGrants()
    {
        await using var database = new SqliteDatabase();
        var cache = new FaultyCache(NewInMemoryCache());
        var host = await database.StartHostAsync(services: s => s.AddSingleton<ICache>(cache));
        var store = host.Services.GetRequiredService<IPermissionGrantStore>();
        await AdministerAsync(host, static (store, ct) => store.GrantAsync([_write], ct));

        // 缓存后端不可达：判定直接查库，结果与缓存正常时相同
        cache.Failing = true;
        Assert.Equal(["orders.write"], (await store.GetGrantsAsync(_editor, null, CancellationToken.None)).ValueOrThrow());
        Assert.Empty((await store.GetGrantsAsync(GrantSubject.ForUser("nobody"), null, CancellationToken.None)).ValueOrThrow());

        // 缓存与数据库都不可达：仍是存储不可达（503），不是空集合
        database.Unreachable = true;
        Assert.Equal(SecurityErrors.StoreUnavailable, (await store.GetGrantsAsync(_editor, null, CancellationToken.None)).Error?.Code);

        // 都恢复后照常命中
        database.Unreachable = false;
        cache.Failing = false;
        Assert.Equal(["orders.write"], (await store.GetGrantsAsync(_editor, null, CancellationToken.None)).ValueOrThrow());
    }

    [Fact]
    public async Task CacheOutageDuringTheWrite_FailsTheCall_AndRollsTheWriteBack()
    {
        await using var database = new SqliteDatabase();
        var cache = new FaultyCache(NewInMemoryCache());
        var host = await database.StartHostAsync(services: s => s.AddSingleton<ICache>(cache));
        var store = host.Services.GetRequiredService<IPermissionGrantStore>();
        Assert.Empty((await store.GetGrantsAsync(_editor, null, CancellationToken.None)).ValueOrThrow());

        // 写入时的失效传不到别的实例：调用失败、事务回滚，不报成功。基础设施异常照内核约定成为 Unexpected，原因是缓存不可达
        cache.Failing = true;
        var granted = await host.InvokeAsync(static async context => await context.Services.GetRequiredService<PermissionGrantRecordStore>().GrantAsync([_write], context.CancellationToken));
        Assert.Equal(KernelErrors.Unexpected, granted.Error?.Code);
        Assert.Equal(CachingErrors.StoreUnavailable, granted.Error?.Inner?.Code);

        cache.Failing = false;
        Assert.Empty((await store.GetGrantsAsync(_editor, null, CancellationToken.None)).ValueOrThrow());
        PermissionGrantPage? page = null;
        (await host.InvokeAsync(async context => page = (await context.Services.GetRequiredService<IPermissionGrantAdministration>().ListAsync(new PermissionGrantQuery(), context.CancellationToken)).ValueOrThrow())).ThrowIfFailure();
        Assert.Empty(page!.Items);
    }

    [Fact]
    public async Task CacheOutageAfterTheCommit_IsLogged_AndTheWriteStands()
    {
        await using var database = new SqliteDatabase();
        var cache = new FaultyCache(NewInMemoryCache());
        var logs = new LogCapture();
        var host = await database.StartHostAsync(services: s => s.AddSingleton<ICache>(cache).AddSingleton<ILoggerFactory>(logs));
        var store = host.Services.GetRequiredService<IPermissionGrantStore>();
        Assert.Empty((await store.GetGrantsAsync(_editor, null, CancellationToken.None)).ValueOrThrow());

        // 写入时的失效成功；提交之后、作用域释放时缓存后端故障：释放不抛出，只记错误日志
        var granted = await host.InvokeAsync(async context =>
        {
            (await context.Services.GetRequiredService<PermissionGrantRecordStore>().GrantAsync([_write], context.CancellationToken)).Single().ThrowIfFailure();
            cache.Failing = true;
        });
        granted.ThrowIfFailure();
        Assert.Contains(logs.Entries, static e => e.Level == LogLevel.Error && e.Category == typeof(PermissionGrantRecordStore).FullName && e.Exception is FrameworkException { Code: CachingErrors.StoreUnavailable });

        // 写入已经提交：缓存恢复后看到新授予（写入时那次失效已经换了代数）
        cache.Failing = false;
        Assert.Equal(["orders.write"], (await store.GetGrantsAsync(_editor, null, CancellationToken.None)).ValueOrThrow());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TwoInstancesOverRedisAndHybridCaching_SeeEachOthersWrites(bool applicationSerializer)
    {
        var redis = Environment.GetEnvironmentVariable("XIHAN_TEST_REDIS");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(redis), "Set the environment variable 'XIHAN_TEST_REDIS' to a Redis instance to run this test.");
        await using var database = new SqliteDatabase();
        // 存活时间都设得很长：另一个实例看到写入只能靠失效，不能靠过期
        KeyValuePair<string, string?>[] settings =
        [
            new("XiHan:Caching:Redis:Configuration", redis),
            new("XiHan:Caching:Redis:KeyPrefix", $"xihan-test:{Guid.NewGuid():N}:"),
            new("XiHan:Caching:Hybrid:LocalTimeToLiveSeconds", "600"),
            new("XiHan:Security:Database:CacheTimeToLiveSeconds", "600"),
        ];
        // 应用登记了只认识自己类型的序列化选项时，框架经 AddTypeInfo 登记的缓存类型并在它后面，照样能写进 Redis
        Action<IServiceCollection>? services = applicationSerializer ? static s => s.AddSingleton(new JsonSerializerOptions { TypeInfoResolver = OrdersJsonContext.Default }) : null;
        var first = await database.StartHostAsync(settings, static b => b.UseFeature<RedisCachingFeature>().UseFeature<HybridCachingFeature>(), services);
        var second = await database.StartHostAsync(settings, static b => b.UseFeature<RedisCachingFeature>().UseFeature<HybridCachingFeature>(), services);
        try
        {
            Assert.True(Assert.IsType<HybridObjectCache>(second.Services.GetRequiredService<ICache>()).InvalidatesAcrossInstances);
            var reader = second.Services.GetRequiredService<IPermissionGrantStore>();
            Assert.Empty((await reader.GetGrantsAsync(_editor, "acme", CancellationToken.None)).ValueOrThrow());
            // 再读一次：已在第二个实例的一级缓存里
            Assert.Empty((await reader.GetGrantsAsync(_editor, "acme", CancellationToken.None)).ValueOrThrow());

            // 第一个实例授予；Redis 频道把失效带到第二个实例，它的下一次判定读到新值（通知是异步的，最多等几秒，远小于存活时间）
            await AdministerAsync(first, static (store, ct) => store.GrantAsync([_writeInAcme], ct));
            await WaitUntilAsync(async () => (await reader.GetGrantsAsync(_editor, "acme", CancellationToken.None)).ValueOrThrow().Count == 1);

            await AdministerAsync(first, static (store, ct) => store.RevokeAsync([_writeInAcme], ct));
            await WaitUntilAsync(async () => (await reader.GetGrantsAsync(_editor, "acme", CancellationToken.None)).ValueOrThrow().Count == 0);
        }
        finally
        {
            // 清掉本用例在 Redis 里的键
            var inspector = Assert.IsAssignableFrom<ICacheStoreInspector>(first.Services.GetRequiredService<ICacheStore>());
            _ = (await inspector.RemoveByPrefixAsync(DatabasePermissionGrantStore.CacheRegion + ":", CancellationToken.None)).ValueOrThrow();
        }
    }

    /// <summary>
    /// 一份独立的内存缓存，可登记进几个宿主共用
    /// </summary>
    private static InMemoryObjectCache NewInMemoryCache()
    {
        var options = new InMemoryCachingOptions();
        return new InMemoryObjectCache(new InMemoryCacheStore(options, TimeProvider.System), options, TimeProvider.System);
    }

    /// <summary>
    /// 在一次调用（Transaction 阶段内）里经写入方授予或撤销，每一项都必须成功
    /// </summary>
    private static async Task AdministerAsync(TestHost host, Func<PermissionGrantRecordStore, CancellationToken, ValueTask<IReadOnlyList<Result>>> write)
        => (await host.InvokeAsync(async context => Assert.All(await write(context.Services.GetRequiredService<PermissionGrantRecordStore>(), context.CancellationToken), static r => r.ThrowIfFailure()))).ThrowIfFailure();

    /// <summary>
    /// 绕过写入方直接写一行（不失效缓存）
    /// </summary>
    private static async Task InsertRowAsync(TestHost host, GrantSubject subject, string permission)
        => (await host.InvokeAsync(async context =>
        {
            var repository = context.Services.GetRequiredService<IRepository<PermissionGrantRecord, Guid>>();
            (await repository.AddAsync(new PermissionGrantRecord { Id = Guid.NewGuid(), SubjectKind = subject.Kind, SubjectId = subject.Id, Permission = permission }, context.CancellationToken)).ThrowIfFailure();
        })).ThrowIfFailure();

    /// <summary>
    /// 轮询到条件成立，最多 5 秒
    /// </summary>
    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!await condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "The other instance did not see the change within 5 seconds.");
            await Task.Delay(20);
        }
    }
}
