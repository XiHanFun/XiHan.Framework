// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.DataSources;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// SqlSugar 的运行时数据源。
/// </summary>
/// <remarks>
/// 按定义连外部库（SQLite 文件、真实 PostgreSQL）、数据库类型名、连接串格式与脱敏、测试连接、健康项、不进应用的作用域客户端。
/// </remarks>
public sealed class SqlSugarDataSourceTests : IDisposable
{
    /// <summary>
    /// 应用自己的库。
    /// </summary>
    private readonly SqliteDatabase _application = new();

    /// <summary>
    /// 作为外部库的另一个 SQLite 文件。
    /// </summary>
    private readonly SqliteDatabase _external = new();

    /// <summary>
    /// 测试结束后删除两个临时库。
    /// </summary>
    public void Dispose()
    {
        _application.Dispose();
        _external.Dispose();
    }

    [Fact]
    public async Task ExternalSqlite_IsRegistered_AndItsClientReadsAndWritesTheExternalFile()
    {
        await using var host = await _application.StartHostAsync();
        var registry = host.Services.GetRequiredService<DataSourceRegistry>();

        var source = (await registry.GetOrAddAsync(new DataSourceDefinition("external", "SQLite", _external.ConnectionString), TestContext.Current.CancellationToken)).ValueOrThrow();
        var client = source.GetSqlSugarClient();
        await client.Ado.ExecuteCommandAsync("CREATE TABLE \"Legacy\" (\"Id\" INTEGER PRIMARY KEY, \"Title\" varchar(20))");
        await client.Ado.ExecuteCommandAsync("INSERT INTO \"Legacy\" (\"Id\", \"Title\") VALUES (1, 'hello')");
        var titles = await client.Ado.SqlQueryAsync<string>("SELECT \"Title\" FROM \"Legacy\"");

        Assert.IsType<SqlSugarDataSource>(source);
        Assert.Equal("SQLite", source.DatabaseType);
        Assert.Equal(["hello"], titles);
        // 外部库的表不在应用库里：运行时数据源不进应用的作用域客户端
        await using var scope = host.Services.CreateAsyncScope();
        Assert.False(scope.ServiceProvider.GetRequiredService<global::SqlSugar.ISqlSugarClient>().DbMaintenance.IsAnyTable("Legacy", false));
        Assert.False(scope.ServiceProvider.GetRequiredService<global::SqlSugar.ISqlSugarClient>().AsTenant().IsAnyConnection("external"));
    }

    [Fact]
    public async Task TestConnection_Succeeds_WithoutRegistering()
    {
        await using var host = await _application.StartHostAsync();
        var registry = host.Services.GetRequiredService<DataSourceRegistry>();

        var tested = await registry.TestAsync(new DataSourceDefinition("external", DatabaseTypes.Sqlite, _external.ConnectionString), TestContext.Current.CancellationToken);

        Assert.True(tested.IsSuccess, tested.ToString());
        Assert.Empty(registry.Names);
    }

    [Fact]
    public async Task TestConnection_Failure_IsUnavailable_WithoutTheSecret()
    {
        await using var host = await _application.StartHostAsync();
        var registry = host.Services.GetRequiredService<DataSourceRegistry>();
        var missing = Path.Combine(Path.GetTempPath(), $"xihan-missing-{Guid.NewGuid():N}", "x.db");
        // 只读模式打开不存在的文件：驱动报打不开，口令不能出现在原因里
        var definition = new DataSourceDefinition("broken", DatabaseTypes.Sqlite, $"Data Source={missing};Mode=ReadOnly;Password=Sq1-Secret");

        var tested = await registry.TestAsync(definition, TestContext.Current.CancellationToken);

        Assert.Equal(PersistenceErrors.DataSourceUnavailable, tested.Error!.Code);
        Assert.Equal("broken", tested.Error.Details!["name"]);
        Assert.DoesNotContain("Sq1-Secret", tested.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Sq1-Secret", (string)tested.Error.Details!["reason"]!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownDatabaseTypeOrMalformedConnectionString_IsValidation()
    {
        await using var host = await _application.StartHostAsync();
        var registry = host.Services.GetRequiredService<DataSourceRegistry>();

        var unknown = await registry.GetOrAddAsync(new DataSourceDefinition("a", "nosuchdb", _external.ConnectionString), TestContext.Current.CancellationToken);
        var numeric = await registry.GetOrAddAsync(new DataSourceDefinition("b", "2", _external.ConnectionString), TestContext.Current.CancellationToken);
        var custom = await registry.GetOrAddAsync(new DataSourceDefinition("c", "Custom", _external.ConnectionString), TestContext.Current.CancellationToken);
        var malformed = await registry.GetOrAddAsync(new DataSourceDefinition("d", DatabaseTypes.Sqlite, "Data Source=x.db;Bogus=1;Password=Kw-Secret-7"), TestContext.Current.CancellationToken);

        Assert.All(new[] { unknown, numeric, custom, malformed }, static r => Assert.Equal(PersistenceErrors.DataSourceInvalid, r.Error!.Code));
        Assert.Equal(PersistenceMessages.DataSourceTypeUnknown, unknown.Error!.MessageKey);
        Assert.Equal(PersistenceMessages.DataSourceInvalid, malformed.Error!.MessageKey);
        Assert.Contains("Bogus", (string)malformed.Error.Details!["reason"]!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Kw-Secret-7", malformed.Error.Message, StringComparison.Ordinal);
        Assert.Empty(registry.Names);
    }

    [Fact]
    public async Task Health_ReportsEachRegisteredSource()
    {
        await using var host = await _application.StartHostAsync();
        var registry = host.Services.GetRequiredService<DataSourceRegistry>();
        var missing = Path.Combine(Path.GetTempPath(), $"xihan-missing-{Guid.NewGuid():N}", "x.db");
        (await registry.GetOrAddAsync(new DataSourceDefinition("good", DatabaseTypes.Sqlite, _external.ConnectionString), TestContext.Current.CancellationToken)).ValueOrThrow();
        (await registry.GetOrAddAsync(new DataSourceDefinition("bad", DatabaseTypes.Sqlite, $"Data Source={missing};Mode=ReadOnly"), TestContext.Current.CancellationToken)).ValueOrThrow();

        await registry.ProbeAsync(TestContext.Current.CancellationToken);
        var statuses = host.Services.GetServices<IHealthContributor>().SelectMany(static c => c.Check()).Where(static s => s.Name.StartsWith(DataSourceRegistry.HealthPrefix, StringComparison.Ordinal)).ToDictionary(static s => s.Name);

        Assert.Equal(LifecycleState.Ready, statuses["persistence.datasource.good"].State);
        Assert.Equal(LifecycleState.Degraded, statuses["persistence.datasource.bad"].State);
        Assert.Equal(PersistenceErrors.DataSourceUnavailable, statuses["persistence.datasource.bad"].Error!.Code);
        Assert.Equal("sqlite", statuses["persistence.datasource.good"].Data["databaseType"]);
        Assert.Equal("sqlsugar", statuses["persistence.datasource.good"].Data["provider"]);
        Assert.DoesNotContain(missing, statuses["persistence.datasource.good"].ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostgreSql_WrongPassword_IsUnavailable_WithSqlState_ButWithoutThePassword()
    {
        var server = PostgresTestServer.RequireConfiguration().TrimEnd(';');
        await using var host = await _application.StartHostAsync();
        var registry = host.Services.GetRequiredService<DataSourceRegistry>();
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(server) { Password = "Wrong-Pg-Secret-42", Database = "postgres" };

        var tested = await registry.TestAsync(new DataSourceDefinition("pg", DatabaseTypes.PostgreSql, builder.ConnectionString), TestContext.Current.CancellationToken);

        Assert.Equal(PersistenceErrors.DataSourceUnavailable, tested.Error!.Code);
        Assert.Contains("28P01", (string)tested.Error.Details!["reason"]!, StringComparison.Ordinal);
        Assert.DoesNotContain("Wrong-Pg-Secret-42", tested.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostgreSql_MixedCaseIdentifiers_AreQuotedAsIs()
    {
        using var database = PostgresTestServer.CreateDatabase();
        await using var host = await _application.StartHostAsync();
        var registry = host.Services.GetRequiredService<DataSourceRegistry>();
        var client = (await registry.GetOrAddAsync(new DataSourceDefinition("pg", DatabaseTypes.PostgreSql, database.ConnectionString), TestContext.Current.CancellationToken)).ValueOrThrow().GetSqlSugarClient();
        await client.Ado.ExecuteCommandAsync("CREATE TABLE \"LegacyOrder\" (\"Id\" int PRIMARY KEY, \"Title\" varchar(20)); INSERT INTO \"LegacyOrder\" VALUES (1, 'a'), (2, 'b');");

        // 运行时数据源不自动转小写：按库里的原样名字查询，零代码运行时据此按表名读外部表
        var count = await client.Queryable<object>().AS("LegacyOrder").CountAsync();

        Assert.Equal(2, count);
        await registry.RemoveAsync(["pg"], TestContext.Current.CancellationToken);
        PostgresTestServer.ClearPools();
    }
}
