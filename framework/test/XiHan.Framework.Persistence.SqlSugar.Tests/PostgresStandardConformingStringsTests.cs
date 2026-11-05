// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 库的缺省字面量规则是 off 时值照样保留（ADR-0100）
/// </summary>
/// <remarks>
/// 库的缺省 <c>standard_conforming_strings</c> 是 off 时，批量写入与按主键的读取照样保留反斜杠、单引号与换行。环境变量 <c>XIHAN_TEST_POSTGRES</c>，未设置即跳过。每个用例一个临时库，建库后 <c>ALTER DATABASE … SET standard_conforming_strings = off</c>，之后的新会话都是 off。SqlSugar 在多行写入、批量更新、按主键删除、<c>In</c> 与集合 <c>Contains</c> 里把字符串内联成 <c>'…'</c> 字面量，只把单引号写成两个：off 下普通字面量里的反斜杠是转义符，<c>x\'y</c> 让语句不合法（42601），<c>a\b</c>、<c>line\n</c> 被静默改成退格与换行，按主键的更新、删除与查找静默漏掉行。约定在 PostgreSQL 连接串的启动参数里写 <c>-c standard_conforming_strings=on</c>，会话从建立起就是 on，库与角色上的缺省值不再起作用
/// </remarks>
public sealed class PostgresStandardConformingStringsTests : IDisposable
{
    /// <summary>
    /// 反斜杠
    /// </summary>
    private const string Backslash = "\\";

    /// <summary>
    /// 用例值：反斜杠在各个位置、反斜杠后跟单引号、单引号、真换行、两个反斜杠
    /// </summary>
    private static readonly string[] _samples =
    [
        "a" + Backslash + "b",
        "x" + Backslash + "'y",
        "tail" + Backslash,
        "line" + Backslash + "n",
        "tab" + Backslash + "t",
        "it's",
        "real\nnewline",
        Backslash + Backslash,
        "plain",
    ];

    /// <summary>
    /// 本测试实例独占的临时库
    /// </summary>
    /// <remarks>
    /// 首次访问时建出并把库的缺省设为 off（未设置环境变量时在此跳过用例）
    /// </remarks>
    private PostgresDatabase? _database;

    /// <summary>
    /// 临时库，按需创建
    /// </summary>
    private PostgresDatabase Database => _database ??= CreateDatabase();

    [Fact]
    public async Task Connections_RunWithStandardConformingStringsOn_WhileTheDatabaseDefaultIsOff()
    {
        await using var host = await StartAsync();
        await using var scope = host.Services.CreateAsyncScope();

        Assert.Equal("on", await scope.ServiceProvider.GetRequiredService<ISqlSugarClient>().Ado.GetStringAsync("SHOW standard_conforming_strings"));
        using var raw = new SqlSugarClient(new ConnectionConfig { DbType = DbType.PostgreSQL, ConnectionString = Database.ConnectionString, IsAutoCloseConnection = true });
        Assert.Equal("off", await raw.Ado.GetStringAsync("SHOW standard_conforming_strings"));
    }

    [Fact]
    public async Task RepositoryBatchWrites_AndKeyLookups_KeepTheValues()
    {
        await using var host = await StartAsync();
        var ids = _samples.Select(static (sample, index) => $"k{index}{sample}").ToList();

        Assert.All(await RunAsync(host, (records, ct) => records.AddRangeAsync([.. _samples.Select((sample, index) => new TextKeyRecord { Id = ids[index], Name = sample })], ct)), static r => Assert.True(r.IsSuccess, r.ToString()));
        var found = await FindManyAsync(host, ids);
        Assert.Equal(ids, found.Select(static r => r.Id));
        Assert.Equal(_samples, found.Select(static r => r.Name));
        Assert.Equal(ids.Count, await CountAsync(host, ids));

        Assert.All(await RunAsync(host, async (records, ct) =>
        {
            var rows = await records.FindManyAsync(ids, ct);
            foreach (var row in rows)
            {
                row.Name = "renamed " + row.Name;
            }

            return await records.UpdateRangeAsync(rows, ct);
        }), static r => Assert.True(r.IsSuccess, r.ToString()));
        Assert.Equal(_samples.Select(static s => "renamed " + s), (await FindManyAsync(host, ids)).Select(static r => r.Name));

        Assert.All(await RunAsync(host, async (records, ct) => await records.RemoveRangeAsync(await records.FindManyAsync(ids, ct), ct)), static r => Assert.True(r.IsSuccess, r.ToString()));
        Assert.Empty(await FindManyAsync(host, ids));
    }

    [Fact]
    public async Task ClientBatchWrites_AndInLists_KeepTheValues()
    {
        await using var host = await StartAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        var rows = _samples.Select(static (sample, index) => new TextKeyRecord { Id = $"c{index}{sample}", Name = sample }).ToList();
        var ids = rows.Select(static r => r.Id).ToList();

        Assert.Equal(rows.Count, await client.InsertableByObject(rows.Cast<object>().ToList()).ExecuteCommandAsync());
        Assert.Equal(_samples, (await client.Queryable<TextKeyRecord>().In(ids).ToListAsync()).OrderBy(r => ids.IndexOf(r.Id)).Select(static r => r.Name));
        Assert.Equal(rows.Count, await client.Queryable<TextKeyRecord>().Where(r => ids.Contains(r.Id)).CountAsync());

        foreach (var row in rows)
        {
            row.Name = "renamed " + row.Name;
        }

        Assert.Equal(rows.Count, await client.UpdateableByObject(rows.Cast<object>().ToList()).ExecuteCommandAsync());
        Assert.Equal(_samples.Select(static s => "renamed " + s), (await client.Queryable<TextKeyRecord>().In(ids).ToListAsync()).OrderBy(r => ids.IndexOf(r.Id)).Select(static r => r.Name));
        Assert.Equal(rows.Count, await client.DeleteableByObject(rows.Cast<object>().ToList()).ExecuteCommandAsync());
        Assert.Equal(0, await client.Queryable<TextKeyRecord>().CountAsync());
    }

    /// <summary>
    /// 测试结束后删库
    /// </summary>
    public void Dispose() => _database?.Dispose();

    /// <summary>
    /// 建临时库，并把它的缺省 <c>standard_conforming_strings</c> 设为 off
    /// </summary>
    private static PostgresDatabase CreateDatabase()
    {
        var database = PostgresTestServer.CreateDatabase();
        using var maintenance = PostgresTestServer.Maintenance(PostgresTestServer.RequireConfiguration().TrimEnd(';'));
        maintenance.Ado.ExecuteCommand($"ALTER DATABASE \"{database.Name}\" SET standard_conforming_strings = off");
        return database;
    }

    /// <summary>
    /// 启动登记了字符串主键实体的宿主
    /// </summary>
    private Task<TestHost> StartAsync()
    {
        _ = Database;
        return TestHost.StartAsync(
            static builder => builder.UseFeature(PostgresDatabase.CreateFeature()),
            Database.Configuration,
            static services => services.AddRepository<TextKeyRecord, string>());
    }

    /// <summary>
    /// 在新调用里对仓储执行一次批量操作
    /// </summary>
    /// <remarks>
    /// 返回逐项结果；有失败项时调用以第一个失败结束
    /// </remarks>
    private static async Task<IReadOnlyList<Result>> RunAsync(TestHost host, Func<IRepository<TextKeyRecord, string>, CancellationToken, Task<IReadOnlyList<Result>>> action)
    {
        IReadOnlyList<Result> results = [];
        (await host.InvokeAsync(async context =>
        {
            results = await action(Records(context), context.CancellationToken);
            context.SetOutcome(results.FirstOrDefault(static r => r.IsFailure, Result.Success()));
        })).ThrowIfFailure();
        return results;
    }

    /// <summary>
    /// 在新调用里按主键批量查找
    /// </summary>
    private static async Task<IReadOnlyList<TextKeyRecord>> FindManyAsync(TestHost host, IReadOnlyList<string> ids)
    {
        IReadOnlyList<TextKeyRecord> found = [];
        (await host.InvokeAsync(async context => found = await Records(context).FindManyAsync(ids, context.CancellationToken))).ThrowIfFailure();
        return found;
    }

    /// <summary>
    /// 在新调用里按主键集合计数（规约里的集合 <c>Contains</c>）
    /// </summary>
    private static async Task<long> CountAsync(TestHost host, IReadOnlyList<string> ids)
    {
        long count = -1;
        (await host.InvokeAsync(async context =>
            count = await Records(context).CountAsync(EntityQuery.All<TextKeyRecord>().Where(new Specification<TextKeyRecord>(r => ids.Contains(r.Id))), context.CancellationToken))).ThrowIfFailure();
        return count;
    }

    /// <summary>
    /// 调用作用域里的仓储
    /// </summary>
    private static IRepository<TextKeyRecord, string> Records(InvocationContext context) => context.Services.GetRequiredService<IRepository<TextKeyRecord, string>>();
}
