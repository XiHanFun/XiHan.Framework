// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// SQLite 上 UTC ticks 时间列的启动核对。
/// </summary>
/// <remarks>
/// RFC-0005「EF Core 在 SQLite 上的时间列核对」，ADR-0094。约定把时刻映射成 UTC ticks 整数（ADR-0091）之前建出的库，列是 <c>TEXT</c>、存 EF Core 的文本；照读会静默读成错误的时刻。特性启动时逐连接核对：列声明不是 INTEGER（空表也算）或抽样最早写入的行查到非整数的值即启动失败（<see cref="PersistenceErrors.StorageFormatMismatch"/>）；新库、库文件还不存在、按 ADR-0091 的换算式重建过的库照常启动。
/// </remarks>
public sealed class EntityFrameworkCoreSqliteTimestampCheckTests : IDisposable
{
    /// <summary>
    /// 开启建表的配置键。
    /// </summary>
    private const string EnsureCreatedKey = $"{EntityFrameworkCorePersistenceOptions.ConfigurationSection}:EnsureCreated";

    /// <summary>
    /// 命名连接的名字。
    /// </summary>
    private const string Archive = "archive";

    /// <summary>
    /// 应用默认库。
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <summary>
    /// 命名连接 <see cref="Archive"/> 的库。
    /// </summary>
    private readonly SqliteDatabase _archive = new();

    /// <summary>
    /// 旧库里的三行：不同偏移、带小数秒，一行有到期时刻。
    /// </summary>
    private readonly TimedRow[] _rows =
    [
        new() { Id = Guid.CreateVersion7(), At = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.FromHours(8)), Due = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero) },
        new() { Id = Guid.CreateVersion7(), At = new DateTimeOffset(2026, 10, 31, 20, 0, 0, TimeSpan.FromHours(-5)).AddTicks(1234567) },
        new() { Id = Guid.CreateVersion7(), At = new DateTimeOffset(2026, 11, 1, 0, 30, 0, TimeSpan.Zero).AddTicks(5000000) },
    ];

    /// <summary>
    /// 删除两个临时数据库文件。
    /// </summary>
    public void Dispose()
    {
        _database.Dispose();
        _archive.Dispose();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Startup_Fails_WhenTimestampColumnsAreStillDeclaredAsText(bool withRows)
    {
        // 旧库：列是 TEXT；空表也失败——TEXT 亲和性的列会把之后写入的整数也存成文本，失败要发生在重建代价最小的时候
        await CreateLegacyAsync(_database, withRows ? _rows : []);

        var cause = await StartupFailureAsync(() => StartAsync(_database.ConnectionString, ensureCreated: true));

        Assert.Equal(PersistenceErrors.StorageFormatMismatch, cause.Code);
        Assert.Contains("TimedRow.At (TEXT)", cause.Message, StringComparison.Ordinal);
        Assert.Contains("TimedRow.Due (TEXT)", cause.Message, StringComparison.Ordinal);
        Assert.Contains("default", cause.Message, StringComparison.Ordinal);
        // 核对只读：库里的文本原样留着，EnsureCreated 也不会动已有的表
        Assert.Equal(withRows ? 3L : 0L, Scalar(_database, "SELECT count(*) FROM \"TimedRow\" WHERE typeof(\"At\") = 'text'"));
        Assert.Equal("TEXT", Scalar(_database, "SELECT type FROM pragma_table_info('TimedRow') WHERE name = 'At'"));
    }

    [Fact]
    public async Task Startup_Fails_WhenATableWasRebuiltWithoutConvertingItsValues()
    {
        // EF Core 迁移在 SQLite 上改列类型就是这样重建表：列建成 INTEGER，数据原样 INSERT … SELECT，文本搬进去仍是文本
        await CreateLegacyAsync(_database, _rows);
        Execute(_database, Rebuild(static column => column));
        // 升级后又写了 1000 行整数，再建一个覆盖两列的索引：走索引会按值排序、整数排在文本之前，抽样就看不到旧行，所以抽样不走索引
        AppendTicks(_database, 1000);
        Execute(_database, "CREATE INDEX \"IX_TimedRow_At_Due\" ON \"TimedRow\" (\"At\", \"Due\")");

        // 照读不报错，只是读成错误的时刻：SQLite 把文本开头的年份当整数
        await using (var context = Timed(_database))
        {
            var misread = await context.Set<TimedRow>().SingleAsync(row => row.Id == _rows[0].Id);
            Assert.Equal(1, misread.At.Year);
        }

        var cause = await StartupFailureAsync(() => StartAsync(_database.ConnectionString, ensureCreated: false));

        Assert.Equal(PersistenceErrors.StorageFormatMismatch, cause.Code);
        Assert.Contains("TimedRow.At (INTEGER, text)", cause.Message, StringComparison.Ordinal);
        Assert.Contains("TimedRow.Due (INTEGER, text)", cause.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_Succeeds_AfterTheTableIsRebuiltWithTheConversionFormula()
    {
        // 按 ADR-0091 第三节：重建表、列建成 INTEGER，搬数据时把文本换算成 UTC ticks（NULL 换算后仍是 NULL）
        await CreateLegacyAsync(_database, _rows);
        Execute(_database, Rebuild(UtcTicksFormula));

        await using var host = await StartAsync(_database.ConnectionString, ensureCreated: false);

        // 换算后读回的是同一时刻（偏移为零），按时刻排序
        await using var scope = host.Services.CreateAsyncScope();
        var read = await scope.ServiceProvider.GetRequiredService<TimedDbContext>().Set<TimedRow>().OrderBy(static row => row.At).ToListAsync();
        Assert.Equal(_rows.OrderBy(static row => row.At.UtcTicks).Select(static row => (row.Id, row.At.UtcTicks, row.Due?.UtcTicks)), read.Select(static row => (row.Id, row.At.UtcTicks, row.Due?.UtcTicks)));
        Assert.All(read, static row => Assert.Equal(TimeSpan.Zero, row.At.Offset));
    }

    [Fact]
    public async Task Startup_Succeeds_OnANewDatabase_WithAndWithoutRows()
    {
        // 新库：EnsureCreated 建出 INTEGER 列，空表照常
        await using (await StartAsync(_database.ConnectionString, ensureCreated: true))
        {
        }

        // 写了行之后再启动：存的是整数，照常
        await using (var context = Timed(_database))
        {
            context.AddRange(_rows);
            await context.SaveChangesAsync();
        }

        await using var host = await StartAsync(_database.ConnectionString, ensureCreated: false);
        Assert.True(host.App.State.IsServing());
        Assert.Equal(3L, Scalar(_database, "SELECT count(*) FROM \"TimedRow\" WHERE typeof(\"At\") = 'integer'"));
    }

    [Fact]
    public async Task Startup_Succeeds_WithoutCreatingAMissingDatabaseFile()
    {
        // 库文件还不存在：没有旧数据，核对不打开连接——SQLite 打开不存在的文件会建出空库
        Assert.False(File.Exists(_database.Path));

        await using var host = await StartAsync(_database.ConnectionString, ensureCreated: false);

        Assert.True(host.App.State.IsServing());
        Assert.False(File.Exists(_database.Path));
    }

    [Fact]
    public async Task Startup_IgnoresColumnsWithTheApplicationsOwnConversion()
    {
        // 应用自己配了转换的列（这里一列二进制整数、一列文本）不归约定管，也不核对
        await using (var context = new ConvertedTimedDbContext(new DbContextOptionsBuilder<ConvertedTimedDbContext>().UseSqlite(_database.ConnectionString).Options))
        {
            await context.Database.EnsureCreatedAsync();
            context.AddRange(_rows);
            await context.SaveChangesAsync();
        }

        await using var host = await TestHost.StartAsync(
            static builder => builder.UseFeature(new EntityFrameworkCorePersistenceFeature<ConvertedTimedDbContext>(static (binding, options) => options.UseSqlite(binding.ValueOrThrow().Value))),
            [new("XiHan:Bindings:db", _database.ConnectionString)],
            static services => services.AddSingleton(TimeProvider.System));

        Assert.True(host.App.State.IsServing());
        Assert.Equal("TEXT", Scalar(_database, "SELECT type FROM pragma_table_info('TimedRow') WHERE name = 'Due'"));
    }

    [Fact]
    public async Task Startup_Fails_WhenTheDatabaseOfANamedConnectionIsLegacy()
    {
        // 实体登记到命名连接：默认库上没有它的表要核对，命名连接的旧库照样查出来，消息里是那个连接的名字
        await CreateLegacyAsync(_archive, _rows);

        var cause = await StartupFailureAsync(() => StartAsync(
            _database.ConnectionString,
            ensureCreated: true,
            [new($"{EntityFrameworkCorePersistenceOptions.ConfigurationSection}:Connections:{Archive}", _archive.ConnectionString)],
            static services => services.AddRepository<TimedRow, Guid>(Archive)));

        Assert.Equal(PersistenceErrors.StorageFormatMismatch, cause.Code);
        Assert.Contains(Archive, cause.Message, StringComparison.Ordinal);
        Assert.Contains("TimedRow.At (TEXT)", cause.Message, StringComparison.Ordinal);
        // 默认库是新建的：表建成了 INTEGER，空着
        Assert.Equal("INTEGER", Scalar(_database, "SELECT type FROM pragma_table_info('TimedRow') WHERE name = 'At'"));
    }

    [Theory]
    [InlineData("INTEGER", true)]
    [InlineData("bigint", true)]
    [InlineData("UNSIGNED BIG INT", true)]
    [InlineData("TEXT", false)]
    [InlineData("NUMERIC", false)]
    [InlineData("", false)]
    public void IntegerAffinity_FollowsTheSqliteRule(string declaredType, bool expected)
        => Assert.Equal(expected, SqliteTimestampCheck.HasIntegerAffinity(declaredType));

    /// <summary>
    /// 启动以 <see cref="TimedDbContext"/> 与 SQLite 装配的宿主。
    /// </summary>
    /// <remarks>
    /// 连接绑定 <c>db</c> 指向 <paramref name="connectionString"/>；<paramref name="settings"/> 与 <paramref name="services"/> 追加配置与服务。
    /// </remarks>
    private static Task<TestHost> StartAsync(string connectionString, bool ensureCreated, IEnumerable<KeyValuePair<string, string?>>? settings = null, Action<IServiceCollection>? services = null) => TestHost.StartAsync(
        static builder => builder.UseFeature(new EntityFrameworkCorePersistenceFeature<TimedDbContext>(static (binding, options) => options.UseSqlite(binding.ValueOrThrow().Value))),
        [new("XiHan:Bindings:db", connectionString), new(EnsureCreatedKey, ensureCreated ? "true" : "false"), .. settings ?? []],
        collection =>
        {
            collection.AddSingleton(TimeProvider.System);
            services?.Invoke(collection);
        });

    /// <summary>
    /// 启动应当失败：返回特性启动失败的原因。
    /// </summary>
    /// <remarks>
    /// 宿主把特性启动失败包成 <c>KERNEL_HOSTING_002</c>，原因在内部异常里。
    /// </remarks>
    private static async Task<FrameworkException> StartupFailureAsync(Func<Task<TestHost>> start)
    {
        var error = await Assert.ThrowsAsync<FrameworkException>(start);
        return Assert.IsType<FrameworkException>(error.InnerException);
    }

    /// <summary>
    /// 用旧库的 DbContext 建表并写入 <paramref name="rows"/>：时刻列是文本。
    /// </summary>
    internal static async Task CreateLegacyAsync(SqliteDatabase database, IReadOnlyList<TimedRow> rows)
    {
        await using var context = new LegacyTimedDbContext(new DbContextOptionsBuilder<LegacyTimedDbContext>().UseSqlite(database.ConnectionString).Options);
        await context.Database.EnsureCreatedAsync();
        context.AddRange(rows.Select(static row => new TimedRow { Id = row.Id, At = row.At, Due = row.Due }));
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// 连到 <paramref name="database"/>、施加了约定的上下文。
    /// </summary>
    private static TimedDbContext Timed(SqliteDatabase database)
        => new(new DbContextOptionsBuilder<TimedDbContext>().UseSqlite(database.ConnectionString).Options);

    /// <summary>
    /// 把 <c>TimedRow</c> 重建成 INTEGER 列的语句：搬数据时每个时刻列套上 <paramref name="convert"/>。
    /// </summary>
    /// <remarks>
    /// 与 EF Core 在 SQLite 上改列类型的迁移同形：建临时表、<c>INSERT … SELECT</c>、删旧表、改名。
    /// </remarks>
    internal static string Rebuild(Func<string, string> convert) => $"""
        CREATE TABLE "ef_temp_TimedRow" ("Id" TEXT NOT NULL CONSTRAINT "PK_TimedRow" PRIMARY KEY, "At" INTEGER NOT NULL, "Due" INTEGER NULL);
        INSERT INTO "ef_temp_TimedRow" ("Id", "At", "Due") SELECT "Id", {convert("\"At\"")}, {convert("\"Due\"")} FROM "TimedRow";
        DROP TABLE "TimedRow";
        ALTER TABLE "ef_temp_TimedRow" RENAME TO "TimedRow";
        """;

    /// <summary>
    /// ADR-0091 第三节的换算式：EF Core 的 <see cref="DateTimeOffset"/> 文本换算成 UTC ticks。
    /// </summary>
    /// <remarks>
    /// 文本形如 <c>2026-10-31 20:00:00.1234567-05:00</c>：前 19 位按 UTC 取 Unix 秒，减去偏移，乘 10⁷，加上补足 7 位的小数秒与 0001-01-01 到 Unix 纪元的 ticks。
    /// </remarks>
    internal static string UtcTicksFormula(string column) => $"""
        (unixepoch(substr({column}, 1, 19))
         - (CASE substr({column}, -6, 1) WHEN '-' THEN -1 ELSE 1 END) * (CAST(substr({column}, -5, 2) AS INTEGER) * 3600 + CAST(substr({column}, -2, 2) AS INTEGER) * 60)) * 10000000
        + CASE WHEN substr({column}, 20, 1) = '.' THEN CAST(substr(substr({column}, 21, length({column}) - 26) || '0000000', 1, 7) AS INTEGER) ELSE 0 END
        + 621355968000000000
        """;

    /// <summary>
    /// 追加 <paramref name="count"/> 行按 UTC ticks 存的整数时刻。
    /// </summary>
    private static void AppendTicks(SqliteDatabase database, int count)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO \"TimedRow\" (\"Id\", \"At\", \"Due\") VALUES ($id, $at, $at)";
        var id = command.Parameters.Add("$id", SqliteType.Text);
        var at = command.Parameters.Add("$at", SqliteType.Integer);
        var start = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero).UtcTicks;
        for (var i = 0; i < count; i++)
        {
            id.Value = Guid.CreateVersion7().ToString();
            at.Value = start + i;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    /// <summary>
    /// 直接连库执行语句。
    /// </summary>
    internal static void Execute(SqliteDatabase database, string sql)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 直接连库取一个标量。
    /// </summary>
    private static object? Scalar(SqliteDatabase database, string sql)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
