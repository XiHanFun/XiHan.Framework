// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Domain.Specifications;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 字符串的包含、前缀与后缀按字面匹配（ADR-0098）
/// </summary>
/// <remarks>
/// 子类给出数据库：SQLite 不设门控，PostgreSQL、MySQL、SQL Server、Oracle 各自按环境变量门控。SqlSugar 原本把 <c>Contains</c> / <c>StartsWith</c> / <c>EndsWith</c> 译成不转义的 <c>LIKE</c>——<c>_</c>、<c>%</c> 是通配符，SQL Server 的 <c>[</c> 开字符类，PostgreSQL 与 MySQL 的 <c>LIKE</c> 缺省以反斜杠转义；约定改了翻译之后，实参里的这些字符（连同转义符 <c>!</c> 与单引号）按字面匹配，经仓储的规约与直接用客户端的查询都一样，实参是闭包变量、常量或另一列也一样。大小写是否敏感照各库，由子类照实给出（<see cref="IgnoresCase"/>）
/// </remarks>
public abstract class SqlSugarLiteralMatchingTests
{
    /// <summary>
    /// 反斜杠
    /// </summary>
    private const string Backslash = "\\";

    /// <summary>
    /// 用例数据：名字与作为实参的另一列
    /// </summary>
    /// <remarks>
    /// 每个按字面应当命中的名字都配一个不转义就会误命中的名字：<c>budgetx2026</c>（<c>_</c>）、<c>500 off</c>（<c>%</c>）、<c>ab</c>（反斜杠被当转义符吃掉）、<c>a</c>（SQL Server 的 <c>[a]</c> 字符类）、<c>xy</c>（转义符 <c>!</c> 自己没转义）、<c>its</c>（单引号）
    /// </remarks>
    private static readonly (string Name, string Fragment)[] _rows =
    [
        ("budget_2026", "t_2"),
        ("budgetx2026", "t_2"),
        ("50% off", "0%"),
        ("500 off", "0%"),
        ("a" + Backslash + "b", "a" + Backslash + "b"),
        ("ab", "a" + Backslash + "b"),
        ("[a]b", "[a]"),
        ("a", "[a]"),
        ("x!y", "!y"),
        ("xy", "!y"),
        ("it's", "'s"),
        ("its", "'s"),
        ("CaseKey", "-"),
    ];

    /// <summary>
    /// 宿主配置：连接绑定与 <c>InitTables</c>
    /// </summary>
    protected abstract IEnumerable<KeyValuePair<string, string?>> Configuration { get; }

    /// <summary>
    /// 本数据库的 <c>LIKE</c> 是否不区分大小写（库与列的缺省排序规则下）
    /// </summary>
    protected abstract bool IgnoresCase { get; }

    /// <summary>
    /// 创建以本数据库装配的特性
    /// </summary>
    protected abstract IFeature CreateFeature();

    [Fact]
    public async Task Repository_Contains_MatchesWildcardCharactersLiterally()
    {
        await using var host = await SeedAsync();

        (string Keyword, string[] Expected)[] cases =
        [
            ("t_2", ["budget_2026"]),
            ("0%", ["50% off"]),
            ("%", ["50% off"]),
            ("_", ["budget_2026"]),
            ("[a]", ["[a]b"]),
            ("[", ["[a]b"]),
            (Backslash, ["a" + Backslash + "b"]),
            ("a" + Backslash + "b", ["a" + Backslash + "b"]),
            ("!", ["x!y"]),
            ("!y", ["x!y"]),
            ("'s", ["it's"]),
            ("' or '1'='1", []),
        ];

        var actual = new List<string>();
        foreach (var (keyword, _) in cases)
        {
            actual.Add(Describe(keyword, await ListAsync(host, r => r.Name.Contains(keyword))));
        }

        Assert.Equal(cases.Select(static c => Describe(c.Keyword, c.Expected)), actual);
        var wildcard = "t_2";
        Assert.Equal(1, await CountAsync(host, r => r.Name.Contains(wildcard)));
    }

    [Fact]
    public async Task Repository_StartsWith_And_EndsWith_MatchLiterally()
    {
        await using var host = await SeedAsync();

        (string Keyword, string[] Expected)[] prefixes =
        [
            ("50%", ["50% off"]),
            ("budget_", ["budget_2026"]),
            ("[a", ["[a]b"]),
            ("a" + Backslash, ["a" + Backslash + "b"]),
            ("x!", ["x!y"]),
        ];
        (string Keyword, string[] Expected)[] suffixes =
        [
            ("_2026", ["budget_2026"]),
            ("% off", ["50% off"]),
            ("]b", ["[a]b"]),
            (Backslash + "b", ["a" + Backslash + "b"]),
            ("!y", ["x!y"]),
        ];

        var starts = new List<string>();
        foreach (var (keyword, _) in prefixes)
        {
            starts.Add(Describe(keyword, await ListAsync(host, r => r.Name.StartsWith(keyword))));
        }

        var ends = new List<string>();
        foreach (var (keyword, _) in suffixes)
        {
            ends.Add(Describe(keyword, await ListAsync(host, r => r.Name.EndsWith(keyword))));
        }

        Assert.Equal(prefixes.Select(static c => Describe(c.Keyword, c.Expected)), starts);
        Assert.Equal(suffixes.Select(static c => Describe(c.Keyword, c.Expected)), ends);
    }

    [Fact]
    public async Task Repository_ConstantAndCharacterArguments_MatchLiterally()
    {
        await using var host = await SeedAsync();

        Assert.Equal(["budget_2026"], await ListAsync(host, static r => r.Name.Contains("t_2")));
        Assert.Equal(["50% off"], await ListAsync(host, static r => r.Name.StartsWith("50%")));
        Assert.Equal(["budget_2026"], await ListAsync(host, static r => r.Name.EndsWith("_2026")));
        Assert.Equal(["50% off"], await ListAsync(host, static r => r.Name.Contains('%')));
        Assert.Equal(["budget_2026"], await ListAsync(host, static r => r.Name.Contains('_')));
    }

    [Fact]
    public async Task ColumnArguments_AreEscapedInSql_AndMatchLiterally()
    {
        await using var host = await SeedAsync();

        Assert.Equal(["50% off", "[a]b", "a" + Backslash + "b", "budget_2026", "it's", "x!y"], await ListAsync(host, static r => r.Name.Contains(r.Fragment)));
        Assert.Equal(["[a]b", "a" + Backslash + "b"], await ListAsync(host, static r => r.Name.StartsWith(r.Fragment)));
        Assert.Equal(["a" + Backslash + "b", "it's", "x!y"], await ListAsync(host, static r => r.Name.EndsWith(r.Fragment)));
    }

    [Fact]
    public async Task ClientQueries_MatchLiterally_LikeTheRepository()
    {
        await using var host = await SeedAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        var underscore = "t_2";
        var percent = "0%";
        var bracket = "[a]";

        Assert.Equal(["budget_2026"], Sorted(await client.Queryable<LiteralMatchRecord>().Where(r => r.Name.Contains(underscore)).Select(static r => r.Name).ToListAsync()));
        Assert.Equal(["50% off"], Sorted(await client.Queryable<LiteralMatchRecord>().Where(r => SqlFunc.Contains(r.Name, percent)).Select(static r => r.Name).ToListAsync()));
        Assert.Equal(["[a]b"], Sorted(await client.Queryable<LiteralMatchRecord>().Where(r => r.Name.StartsWith(bracket)).Select(static r => r.Name).ToListAsync()));
        Assert.Equal(1, await client.Queryable<LiteralMatchRecord>().Where(r => r.Name.EndsWith(percent + " off")).CountAsync());

        // 删除的条件同样按字面：只删掉 budget_2026，budgetx2026 留下
        Assert.Equal(1, await client.Deleteable<LiteralMatchRecord>().Where(r => r.Name.Contains(underscore)).ExecuteCommandAsync());
        Assert.Equal(["budgetx2026"], Sorted(await client.Queryable<LiteralMatchRecord>().Where(static r => r.Name.StartsWith("budget")).Select(static r => r.Name).ToListAsync()));
    }

    [Fact]
    public async Task SqlFuncLike_KeepsWildcards()
    {
        await using var host = await SeedAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        var pattern = "50%";

        // 需要通配的查询用 SqlFunc.Like：模式由调用方给出，不转义
        Assert.Equal(["50% off", "500 off"], Sorted(await client.Queryable<LiteralMatchRecord>().Where(r => SqlFunc.Like(r.Name, pattern)).Select(static r => r.Name).ToListAsync()));
    }

    [Fact]
    public async Task Case_FollowsTheDatabase()
    {
        await using var host = await SeedAsync();
        var lower = "casekey";

        string[] expected = IgnoresCase ? ["CaseKey"] : [];
        Assert.Equal(expected, await ListAsync(host, r => r.Name.Contains(lower)));
        Assert.Equal(expected, await ListAsync(host, static r => r.Name.StartsWith("casek")));
        Assert.Equal(["CaseKey"], await ListAsync(host, static r => r.Name.Contains("CaseKey")));
    }

    /// <summary>
    /// 一组用例的可读描述：实参与命中的名字
    /// </summary>
    /// <remarks>
    /// 断言整组描述，失败时一次列出全部不符的实参
    /// </remarks>
    private static string Describe(string keyword, IEnumerable<string> names) => $"'{keyword}' -> [{string.Join(" | ", names)}]";

    /// <summary>
    /// 按序数排序的名字
    /// </summary>
    private static List<string> Sorted(IEnumerable<string> names) => [.. names.Order(StringComparer.Ordinal)];

    /// <summary>
    /// 启动宿主并写入用例数据
    /// </summary>
    private async Task<TestHost> SeedAsync()
    {
        var host = await TestHost.StartAsync(
            builder => builder.UseFeature(CreateFeature()),
            Configuration,
            static services => services.AddRepository<LiteralMatchRecord, Guid>());
        try
        {
            (await host.InvokeAsync(async context =>
            {
                var results = await Records(context).AddRangeAsync([.. _rows.Select(static row => new LiteralMatchRecord { Id = Guid.CreateVersion7(), Name = row.Name, Fragment = row.Fragment })], context.CancellationToken);
                context.SetOutcome(results.FirstOrDefault(static r => r.IsFailure, Result.Success()));
            })).ThrowIfFailure();
            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// 在新调用里经仓储按规约查询，返回按序数排序的名字
    /// </summary>
    private static async Task<List<string>> ListAsync(TestHost host, Expression<Func<LiteralMatchRecord, bool>> criteria)
    {
        IReadOnlyList<LiteralMatchRecord> found = [];
        (await host.InvokeAsync(async context =>
            found = await Records(context).ListAsync(EntityQuery.All<LiteralMatchRecord>().Where(new Specification<LiteralMatchRecord>(criteria)), context.CancellationToken))).ThrowIfFailure();
        return Sorted(found.Select(static r => r.Name));
    }

    /// <summary>
    /// 在新调用里经仓储按规约计数
    /// </summary>
    private static async Task<long> CountAsync(TestHost host, Expression<Func<LiteralMatchRecord, bool>> criteria)
    {
        long count = -1;
        (await host.InvokeAsync(async context =>
            count = await Records(context).CountAsync(EntityQuery.All<LiteralMatchRecord>().Where(new Specification<LiteralMatchRecord>(criteria)), context.CancellationToken))).ThrowIfFailure();
        return count;
    }

    /// <summary>
    /// 调用作用域里的仓储
    /// </summary>
    private static IRepository<LiteralMatchRecord, Guid> Records(InvocationContext context) => context.Services.GetRequiredService<IRepository<LiteralMatchRecord, Guid>>();
}
