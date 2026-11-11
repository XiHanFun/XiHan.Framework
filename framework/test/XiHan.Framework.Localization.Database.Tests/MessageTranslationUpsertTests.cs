// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Localization.Abstractions.Locales;
using XiHan.Framework.Localization.Database.Translations;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Persistence.SqlSugar;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Localization.Database.Tests;

/// <summary>
/// 同一 (键, 区域) 只有一行（ADR-0072）
/// </summary>
/// <remarks>
/// 再次写入是更新，并发新增撞上唯一索引时返回明确的冲突
/// 已有行超过一页时也按更新处理（之前只读第一页，超出的行被当成新行再插一遍）；两个调用同时新增同一个键与区域，后插入的那一批返回 <c>PERSISTENCE_CONFLICT_003</c>、调用回滚，重试按更新处理
/// </remarks>
public sealed class MessageTranslationUpsertTests : IDisposable
{
    /// <summary>
    /// 竞争用例的键
    /// </summary>
    private const string RaceKey = "orders.race";

    /// <summary>
    /// 竞争用例的临时数据库文件
    /// </summary>
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"xihan-localization-upsert-{Guid.NewGuid():N}.db");

    /// <summary>
    /// 竞争开关：为 1 时让另一个连接抢先提交
    /// </summary>
    /// <remarks>
    /// 下一条按 (键, 区域) 查已有翻译的查询执行完后，另一个连接抢先插入并提交同一个键与区域，用过一次即复位
    /// </remarks>
    private int _armed;

    /// <summary>
    /// 清空本库的连接池后删除数据库文件
    /// </summary>
    /// <remarks>
    /// 只清本库连接串的连接池（连接池按连接串分组）。不用 <c>SqliteConnection.ClearAllPools()</c>：它清整个进程的连接池，会把并行用例此刻正在打开的连接当作泄漏回收并关掉句柄，那个用例随即以 <c>ObjectDisposedException</c>（<c>SQLitePCL.sqlite3</c>）失败
    /// </remarks>
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
    public async Task Upsert_UpdatesEveryExistingRow_WhenTheyExceedOneQueryPage()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryPersistenceFeature>().UseFeature<InMemoryLocalizationFeature>().UseFeature<DatabaseLocalizationFeature>(),
            [new("XiHan:Localization:Database:RefreshIntervalSeconds", "0")]);
        // 150 个键 × 10 个区域 = 1500 行，超过查询一页的上限 1000
        var locales = new[] { "en-US", "zh-CN", "zh-TW", "ja-JP", "ko-KR", "de-DE", "es-ES", "fr-FR", "pt-BR", "ru-RU" };
        var keys = Enumerable.Range(0, 150).Select(static i => $"orders.key{i:000}").ToList();

        foreach (var template in new[] { "first", "second" })
        {
            var entries = keys.SelectMany(key => locales.Select(locale => new MessageTranslationEntry(key, locale, template))).ToList();
            var written = await host.InvokeAsync(async context =>
            {
                var results = await context.Services.GetRequiredService<MessageTranslationStore>().UpsertAsync(entries, context.CancellationToken);
                Assert.All(results, static r => Assert.True(r.IsSuccess));
            });
            written.ThrowIfFailure();
        }

        var rows = new List<MessageTranslation>();
        (await host.InvokeAsync(async context =>
        {
            var store = context.Services.GetRequiredService<MessageTranslationStore>();
            for (var skip = 0; ; skip += 1000)
            {
                var page = await store.ListAsync(null, skip, 1000, context.CancellationToken);
                rows.AddRange(page);
                if (page.Count < 1000)
                {
                    break;
                }
            }
        })).ThrowIfFailure();

        // 第二次写入全部是更新：没有第二行，模板都换成了新的
        Assert.Equal(1500, rows.Count);
        Assert.All(rows, static r => Assert.Equal("second", r.Template));
    }

    [Fact]
    public async Task ConcurrentInsertOfTheSameKeyAndLocale_ReturnsUniqueKeyConflict_AndTheRetryUpdates()
    {
        EnsureWriteAheadLog();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature(new SqlSugarPersistenceFeature()).UseFeature<InMemoryLocalizationFeature>().UseFeature<DatabaseLocalizationFeature>(),
            [new("XiHan:Persistence:SqlSugar:InitTables", "true"), new("XiHan:Localization:Database:RefreshIntervalSeconds", "0")],
            services => services.AddScoped<ISqlSugarClient>(_ => RacingClient()));

        // 本次写入查完已有行、还没插入时，另一个调用抢先提交了同一个键与区域
        Interlocked.Exchange(ref _armed, 1);
        var raced = await host.InvokeAsync(async context =>
        {
            var results = await context.Services.GetRequiredService<MessageTranslationStore>().UpsertAsync([new MessageTranslationEntry(RaceKey, "zh-CN", "mine")], context.CancellationToken);
            Assert.Equal(PersistenceErrors.UniqueKeyConflict, Assert.Single(results).Error?.Code);
        });

        // 撞上唯一索引后工作单元只能回滚：处理器没有返回失败，调用也以同一错误结束
        Assert.Equal(0, _armed);
        Assert.Equal(PersistenceErrors.UniqueKeyConflict, raced.Error?.Code);
        Assert.Equal([("zh-CN", "rival")], await RowsAsync(host));

        // 重试读到先提交的那一行，按更新处理
        var retried = await host.InvokeAsync(async context =>
        {
            var results = await context.Services.GetRequiredService<MessageTranslationStore>().UpsertAsync([new MessageTranslationEntry(RaceKey, "zh-CN", "mine")], context.CancellationToken);
            Assert.True(Assert.Single(results).IsSuccess);
        });
        retried.ThrowIfFailure();
        Assert.Equal([("zh-CN", "mine")], await RowsAsync(host));
    }

    /// <summary>
    /// 竞争键的全部行：区域与模板
    /// </summary>
    private static async Task<List<(string Locale, string Template)>> RowsAsync(TestHost host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<ISqlSugarClient>().Queryable<MessageTranslation>().Where(static t => t.Key == RaceKey).ToListAsync();
        return [.. rows.Select(static r => (r.Locale, r.Template))];
    }

    /// <summary>
    /// 按约定配置、会制造竞争的客户端
    /// </summary>
    /// <remarks>
    /// 开关打开后，按 (键, 区域) 查已有翻译的查询一执行完就让另一个连接抢先插入并提交同一个键与区域
    /// 读在工作单元开启事务之前执行，抢先的插入在那之后、本次插入之前提交，本次插入撞上唯一索引
    /// </remarks>
    private SqlSugarClient RacingClient()
    {
        var client = Client();
        client.Aop.OnLogExecuted = (sql, _) =>
        {
            if (sql.Contains(nameof(MessageTranslation), StringComparison.Ordinal) && sql.Contains(" IN ", StringComparison.OrdinalIgnoreCase) && Interlocked.Exchange(ref _armed, 0) == 1)
            {
                using var rival = Client();
                rival.Insertable(new MessageTranslation { Id = Guid.CreateVersion7(), Key = RaceKey, Locale = LocaleName.Normalize("zh-CN"), Template = "rival" }).ExecuteCommand();
            }
        };
        return client;
    }

    /// <summary>
    /// 连到测试数据库、按协议约定配置的客户端
    /// </summary>
    private SqlSugarClient Client()
        => new(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = $"Data Source={_path}", IsAutoCloseConnection = true }));

    /// <summary>
    /// 启动宿主前把文件切到 WAL 日志：读不阻塞另一个连接的写
    /// </summary>
    private void EnsureWriteAheadLog()
    {
        using var connection = new SqliteConnection($"Data Source={_path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL";
        command.ExecuteNonQuery();
    }
}
