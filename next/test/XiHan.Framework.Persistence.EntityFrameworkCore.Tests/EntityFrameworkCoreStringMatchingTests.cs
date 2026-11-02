// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// SQLite 上字符串的包含、前缀与后缀一律译成 <c>LIKE</c>（ADR-0101）。
/// </summary>
/// <remarks>
/// EF Core 的 SQLite Provider 原本把 <c>Contains</c> 译成区分大小写的 <c>instr</c>，<c>StartsWith</c> / <c>EndsWith</c> 的值实参译成只对 ASCII 不区分大小写的 <c>LIKE</c>、列实参译成区分的 <c>substr</c> 比较。特性建出的 <c>DbContext</c> 把三个方法都译成 <c>LIKE … ESCAPE '\'</c>：只对 ASCII 字母不区分大小写，与 SqlSugar 在 SQLite 上一样；<c>%</c>、<c>_</c>、反斜杠按字面。用例直接用宿主作用域里的 <c>DbContext</c>（不经仓储），覆盖应用自己写的 LINQ。
/// </remarks>
public sealed class EntityFrameworkCoreStringMatchingTests : IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时 SQLite 数据库。
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <summary>
    /// 测试结束后删除临时数据库文件。
    /// </summary>
    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task OnSqlite_AllThreeMethods_IgnoreAsciiCase_ForVariablesConstantsAndColumns()
    {
        await using var host = await SeedAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var notes = scope.ServiceProvider.GetRequiredService<ConformanceDbContext>().Set<ConformanceNote>();
        var keyword = "casekey";
        var accented = "äpfel";

        // 值实参（闭包变量、常量）与列实参（租户列）三种写法、三个方法：ASCII 字母不区分大小写，其余字母区分
        Assert.Equal(["CaseKey"], await TextsAsync(notes, n => n.Text.Contains(keyword)));
        Assert.Equal(["CaseKey"], await TextsAsync(notes, static n => n.Text.Contains("CASEKEY")));
        Assert.Equal(["50% off", "A\\b", "CaseKey", "ax_1", "budget_2026"], await TextsAsync(notes, static n => n.Text.Contains(n.TenantId)));
        Assert.Equal(["CaseKey"], await TextsAsync(notes, n => n.Text.StartsWith(keyword)));
        Assert.Equal(["CaseKey"], await TextsAsync(notes, static n => n.Text.StartsWith("CASEK")));
        Assert.Equal(["A\\b", "CaseKey"], await TextsAsync(notes, static n => n.Text.StartsWith(n.TenantId)));
        Assert.Equal(["CaseKey"], await TextsAsync(notes, n => n.Text.EndsWith(keyword)));
        Assert.Equal(["CaseKey"], await TextsAsync(notes, static n => n.Text.EndsWith("SEKEY")));
        Assert.Equal(["CaseKey", "ax_1"], await TextsAsync(notes, static n => n.Text.EndsWith(n.TenantId)));
        Assert.Empty(await TextsAsync(notes, n => n.Text.Contains(accented)));
        Assert.Equal(["Äpfel"], await TextsAsync(notes, static n => n.Text.Contains("Äpfel")));
    }

    [Fact]
    public async Task OnSqlite_WildcardCharactersInColumnArguments_MatchLiterally()
    {
        await using var host = await SeedAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var notes = scope.ServiceProvider.GetRequiredService<ConformanceDbContext>().Set<ConformanceNote>();

        // 租户列里的 _、%、反斜杠不是通配符或转义符：t_2 不命中 budgetx2026，0% 不命中 500 off，a\ 不命中 ab，x_1 不命中 axy1
        var contained = await TextsAsync(notes, static n => n.Text.Contains(n.TenantId));
        var started = await TextsAsync(notes, static n => n.Text.StartsWith(n.TenantId));
        var ended = await TextsAsync(notes, static n => n.Text.EndsWith(n.TenantId));
        Assert.DoesNotContain("budgetx2026", contained);
        Assert.DoesNotContain("500 off", contained);
        Assert.DoesNotContain("axy1", contained);
        Assert.DoesNotContain("ab", started);
        Assert.DoesNotContain("axy1", ended);
    }

    [Fact]
    public async Task OnSqlite_TranslatesToLikeWithEscape_AndKeepsValuesOutOfTheSqlText()
    {
        await using var host = await SeedAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var notes = scope.ServiceProvider.GetRequiredService<ConformanceDbContext>().Set<ConformanceNote>();
        var keyword = "secret_kw";

        foreach (var query in new[]
        {
            notes.Where(n => n.Text.Contains(keyword)),
            notes.Where(n => n.Text.StartsWith(keyword)),
            notes.Where(n => n.Text.EndsWith(keyword)),
            notes.Where(static n => n.Text.Contains(n.TenantId)),
            notes.Where(static n => n.Text.StartsWith(n.TenantId)),
            notes.Where(static n => n.Text.EndsWith(n.TenantId)),
        })
        {
            // ToQueryString 先列出参数的取值，再是 SQL 本身：只看 SQL 本身
            var text = query.ToQueryString();
            var sql = text[text.IndexOf("SELECT", StringComparison.Ordinal)..];
            Assert.Contains(" LIKE ", sql, StringComparison.Ordinal);
            Assert.Contains("ESCAPE '\\'", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("instr", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("substr", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(keyword, sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task OnSqlite_NullArgumentMatchesNothing_NegationAndCharactersFollowTheSameRule()
    {
        await using var host = await SeedAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var notes = scope.ServiceProvider.GetRequiredService<ConformanceDbContext>().Set<ConformanceNote>();
        string? missing = null;
        var keyword = "casekey";
        var letter = 'k';

        // NULL 实参不匹配（与原来一样）；取反照常；字符实参与字符串一样只对 ASCII 不区分大小写；空串匹配全部
        Assert.Empty(await TextsAsync(notes, n => n.Text.Contains(missing!)));
        Assert.Empty(await TextsAsync(notes, n => n.Text.StartsWith(missing!)));
        Assert.Equal(9, await notes.CountAsync(n => !n.Text.Contains(keyword)));
        Assert.Equal(["CaseKey"], await TextsAsync(notes, n => n.Text.Contains(letter)));
        Assert.Equal(["CaseKey"], await TextsAsync(notes, static n => n.Text.StartsWith('c')));
        Assert.Equal(["CaseKey"], await TextsAsync(notes, static n => n.Text.EndsWith('Y')));
        Assert.Equal(10, await notes.CountAsync(static n => n.Text.Contains("")));
    }

    [Fact]
    public async Task OnSqlite_Projections_TranslateOnTheServer_AndClientEvaluationKeepsDotNetSemantics()
    {
        await using var host = await SeedAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var notes = scope.ServiceProvider.GetRequiredService<ConformanceDbContext>().Set<ConformanceNote>();
        var keyword = "casekey";
        string[] keywords = ["CASEKEY", "äpfel"];

        // 投影里译成 SQL 的部分同样是 LIKE；落到客户端求值的部分按 .NET 原方法（序数、区分大小写）计算，不报错
        var server = await notes.OrderBy(static n => n.Text).Select(n => new { n.Text, Hit = n.Text.Contains(keyword) }).ToListAsync();
        Assert.Equal(["CaseKey"], server.Where(static r => r.Hit).Select(static r => r.Text));
        var client = await notes.OrderBy(static n => n.Text).Select(n => new { n.Text, Hit = Echo(n.Text).Contains(keyword) }).ToListAsync();
        Assert.Equal(10, client.Count);
        Assert.DoesNotContain(client, static r => r.Hit);

        // 参数化的集合与子查询里同样生效
        Assert.Equal(["CaseKey"], await TextsAsync(notes, n => keywords.Any(k => n.Text.Contains(k))));
    }

    [Fact]
    public void Rewrite_OnlyOnSqlite_AndOnlyTheSingleArgumentOverloads()
    {
        Expression<Func<ConformanceNote, bool>> filter = static n => n.Text.Contains("ab") || n.Text.StartsWith('b') || n.Text.EndsWith(n.TenantId);

        // 别的 Provider 与不知道 Provider 时原样返回同一个实例
        Assert.Same(filter, SqliteStringMatchingInterceptor.Rewrite(filter, "Npgsql.EntityFrameworkCore.PostgreSQL"));
        Assert.Same(filter, SqliteStringMatchingInterceptor.Rewrite(filter, null));
        var rewritten = SqliteStringMatchingInterceptor.Rewrite(filter, "Microsoft.EntityFrameworkCore.Sqlite");
        var calls = new List<string>();
        new CallCollector(calls).Visit(rewritten);
        Assert.Equal(["SqliteStringMatching.Contains", "SqliteStringMatching.StartsWith", "SqliteStringMatching.EndsWith"], calls);

        // 带 StringComparison 的重载不在契约内，原样留给 EF Core
        Expression<Func<ConformanceNote, bool>> compared = static n => n.Text.Contains("ab", StringComparison.Ordinal);
        Assert.Same(compared, SqliteStringMatchingInterceptor.Rewrite(compared, "Microsoft.EntityFrameworkCore.Sqlite"));
    }

    /// <summary>
    /// 原样交回字符串、SQL 里译不出的方法。
    /// </summary>
    /// <remarks>
    /// 让投影的这一部分落到客户端求值。
    /// </remarks>
    private static string Echo(string text) => text;

    /// <summary>
    /// 按条件过滤后的笔记内容，按 SQLite 的二进制排序规则排列。
    /// </summary>
    /// <remarks>
    /// 大写字母排在小写之前，非 ASCII 字母排在最后。
    /// </remarks>
    private static async Task<List<string>> TextsAsync(IQueryable<ConformanceNote> notes, Expression<Func<ConformanceNote, bool>> filter)
        => await notes.Where(filter).OrderBy(static n => n.Text).Select(static n => n.Text).ToListAsync();

    /// <summary>
    /// 启动宿主并直接经 <c>DbContext</c> 写入十条笔记。
    /// </summary>
    /// <remarks>
    /// 租户列当作列实参：每条笔记的内容与它的租户列只差大小写、或含有 <c>_</c>、<c>%</c>、反斜杠。
    /// </remarks>
    private async Task<TestHost> SeedAsync()
    {
        var host = await _database.StartHostAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConformanceDbContext>();
        (string Tenant, string Text)[] rows =
        [
            ("casekey", "CaseKey"),
            ("t_2", "budget_2026"),
            ("t_2", "budgetx2026"),
            ("0%", "50% off"),
            ("0%", "500 off"),
            ("a\\", "A\\b"),
            ("a\\", "ab"),
            ("äpfel", "Äpfel"),
            ("x_1", "ax_1"),
            ("x_1", "axy1"),
        ];
        context.Set<ConformanceNote>().AddRange(rows.Select(static row => new ConformanceNote { Id = Guid.CreateVersion7(), TenantId = row.Tenant, Text = row.Text }));
        await context.SaveChangesAsync();
        return host;
    }

    /// <summary>
    /// 按出现顺序记下表达式里字符串匹配方法与标记方法的调用。
    /// </summary>
    private sealed class CallCollector(List<string> calls) : ExpressionVisitor
    {
        /// <inheritdoc />
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var result = base.VisitMethodCall(node);
            if (node.Method.Name is nameof(string.Contains) or nameof(string.StartsWith) or nameof(string.EndsWith))
            {
                calls.Add($"{node.Method.DeclaringType!.Name}.{node.Method.Name}");
            }

            return result;
        }
    }
}
