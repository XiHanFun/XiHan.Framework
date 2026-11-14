// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlSugar;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Caching.Hybrid;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Caching.Redis;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Localization.Database.Translations;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Localization.Database.Tests;

/// <summary>
/// 翻译版本戳放在 ICache 里，多实例在一个核对间隔内用上新翻译（ADR-0229）
/// </summary>
/// <remarks>
/// 两个宿主共用一个 SQLite 文件与一份缓存，周期刷新都关掉：一个实例写入后，另一个实例只能靠版本戳看到新翻译。缓存后端故障时写入照常提交、本实例照常重新加载，只记错误日志
/// </remarks>
public sealed class TranslationVersionTests : IDisposable
{
    /// <summary>
    /// 用到的消息键
    /// </summary>
    private const string Key = "orders.order.not_found";

    /// <summary>
    /// 业务目录
    /// </summary>
    private static readonly MessageCatalog _orders = new("orders", new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        ["en-US"] = new Dictionary<string, string> { [Key] = "Order '{id}' was not found." },
        ["zh-CN"] = new Dictionary<string, string> { [Key] = "订单 '{id}' 不存在。" },
    });

    /// <summary>
    /// 临时数据库文件
    /// </summary>
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"xihan-localization-version-{Guid.NewGuid():N}.db");

    /// <summary>
    /// 清空本库的连接池后删除数据库文件
    /// </summary>
    public void Dispose()
    {
        using (var connection = new SqliteConnection($"Data Source={_path}"))
        {
            SqliteConnection.ClearPool(connection);
        }

        File.Delete(_path);
        File.Delete(_path + "-wal");
        File.Delete(_path + "-shm");
    }

    [Fact]
    public async Task AnotherInstanceSharingTheCache_ReloadsWhenTheVersionChanges()
    {
        var shared = new FaultyCache(NewInMemoryCache());
        await using var writer = await StartAsync(services: s => s.AddSingleton<ICache>(shared));
        await using var reader = await StartAsync(services: s => s.AddSingleton<ICache>(shared));
        var localizer = reader.Services.GetRequiredService<IMessageLocalizer>();
        await WaitUntilAsync(() => reader.Services.GetRequiredService<MessageTranslationLoader>().LastLoadedAt is not null);

        // 写入方提交后换版本戳：读取方的周期刷新是关的，只能靠核对版本戳重新加载
        await UpsertAsync(writer, "订单 '{id}' 找不到。");
        await WaitUntilAsync(() => localizer.Localize(Key, "zh-CN", MessageArguments.Of(("id", 1))) == "订单 '1' 找不到。");
        Assert.Equal("订单 '1' 找不到。", writer.Services.GetRequiredService<IMessageLocalizer>().Localize(Key, "zh-CN", MessageArguments.Of(("id", 1))));

        // 绕过写入方直接改表后手动发布，同样传到另一个实例
        await using (var scope = writer.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISqlSugarClient>().Updateable<MessageTranslation>().SetColumns(static t => t.Template == "订单 '{id}' 改过了。").Where(static t => t.Key == Key).ExecuteCommandAsync();
        }

        await writer.Services.GetRequiredService<MessageTranslationLoader>().PublishChangeAsync(CancellationToken.None);
        await WaitUntilAsync(() => localizer.Localize(Key, "zh-CN", MessageArguments.Of(("id", 1))) == "订单 '1' 改过了。");
    }

    [Fact]
    public async Task CacheOutage_TheWriteStands_AndThisInstanceStillReloads()
    {
        var cache = new FaultyCache(NewInMemoryCache());
        var logs = new LogCapture();
        await using var host = await StartAsync(services: s => s.AddSingleton<ICache>(cache).AddSingleton<ILoggerFactory>(logs));
        var loader = host.Services.GetRequiredService<MessageTranslationLoader>();
        await WaitUntilAsync(() => loader.LastLoadedAt is not null);

        // 版本戳写不进缓存：写入已经提交，释放不抛出、记错误日志；本实例照常重新加载
        cache.Failing = true;
        await UpsertAsync(host, "订单 '{id}' 找不到。");
        Assert.Contains(logs.Entries, static e => e.Level == LogLevel.Error && e.Category == typeof(MessageTranslationStore).FullName && e.Exception is FrameworkException { Code: CachingErrors.StoreUnavailable });
        await WaitUntilAsync(() => host.Services.GetRequiredService<IMessageLocalizer>().Localize(Key, "zh-CN", MessageArguments.Of(("id", 1))) == "订单 '1' 找不到。");

        // 手动发布在缓存故障时抛出，但本实例仍然重新加载
        await Assert.ThrowsAsync<FrameworkException>(async () => await loader.PublishChangeAsync(CancellationToken.None));
        cache.Failing = false;
    }

    [Fact]
    public async Task TwoInstancesOverRedisAndHybridCaching_ReloadEachOthersTranslations()
    {
        var redis = Environment.GetEnvironmentVariable("XIHAN_TEST_REDIS");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(redis), "Set the environment variable 'XIHAN_TEST_REDIS' to a Redis instance to run this test.");
        KeyValuePair<string, string?>[] settings =
        [
            new("XiHan:Caching:Redis:Configuration", redis),
            new("XiHan:Caching:Redis:KeyPrefix", $"xihan-test:{Guid.NewGuid():N}:"),
            new("XiHan:Caching:Hybrid:LocalTimeToLiveSeconds", "600"),
        ];
        await using var writer = await StartAsync(settings, static b => b.UseFeature<RedisCachingFeature>().UseFeature<HybridCachingFeature>());
        await using var reader = await StartAsync(settings, static b => b.UseFeature<RedisCachingFeature>().UseFeature<HybridCachingFeature>());
        try
        {
            Assert.True(Assert.IsType<HybridObjectCache>(reader.Services.GetRequiredService<ICache>()).InvalidatesAcrossInstances);
            var localizer = reader.Services.GetRequiredService<IMessageLocalizer>();
            await WaitUntilAsync(() => reader.Services.GetRequiredService<MessageTranslationLoader>().LastLoadedAt is not null);

            await UpsertAsync(writer, "订单 '{id}' 找不到。");
            await WaitUntilAsync(() => localizer.Localize(Key, "zh-CN", MessageArguments.Of(("id", 1))) == "订单 '1' 找不到。");
        }
        finally
        {
            // 清掉本用例在 Redis 里的版本戳
            var inspector = Assert.IsAssignableFrom<ICacheStoreInspector>(writer.Services.GetRequiredService<ICacheStore>());
            _ = (await inspector.RemoveByPrefixAsync("localization:", CancellationToken.None)).ValueOrThrow();
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
    /// 在一次调用里经写入方写一条中文翻译
    /// </summary>
    private static async Task UpsertAsync(TestHost host, string template)
        => (await host.InvokeAsync(async context => Assert.All(await context.Services.GetRequiredService<MessageTranslationStore>().UpsertAsync([new MessageTranslationEntry(Key, "zh-CN", template)], context.CancellationToken), static r => r.ThrowIfFailure()))).ThrowIfFailure();

    /// <summary>
    /// 轮询到条件成立，最多 5 秒
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "The condition did not become true within 5 seconds.");
            await Task.Delay(20);
        }
    }

    /// <summary>
    /// 启动连到本测试 SQLite 文件的宿主：周期刷新关掉，版本戳每 50 毫秒核对一次
    /// </summary>
    private async Task<TestHost> StartAsync(IEnumerable<KeyValuePair<string, string?>>? configuration = null, Action<AppBuilder>? features = null, Action<IServiceCollection>? services = null)
    {
        using (var connection = new SqliteConnection($"Data Source={_path}"))
        {
            // WAL：读不阻塞另一个宿主的写
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL";
            command.ExecuteNonQuery();
        }

        return await TestHost.StartAsync(
            b =>
            {
                b.UseFeature(new SqlSugarPersistenceFeature()).UseFeature<InMemoryLocalizationFeature>().UseFeature<InMemoryCachingFeature>().UseFeature<DatabaseLocalizationFeature>();
                features?.Invoke(b);
            },
            [
                new("XiHan:Persistence:SqlSugar:InitTables", "true"),
                new("XiHan:Localization:Database:RefreshIntervalSeconds", "0"),
                new("XiHan:Localization:Database:ChangeCheckIntervalSeconds", "0.05"),
                .. configuration ?? [],
            ],
            collection =>
            {
                collection.AddMessageCatalog(_orders);
                collection.AddScoped<ISqlSugarClient>(_ => new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = $"Data Source={_path}", IsAutoCloseConnection = true })));
                services?.Invoke(collection);
            });
    }
}
