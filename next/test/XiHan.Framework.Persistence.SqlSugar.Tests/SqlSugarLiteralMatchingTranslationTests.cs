// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 字符串匹配的翻译：不连库，只看 SqlSugar 生成的 SQL 与参数（ADR-0098）
/// </summary>
/// <remarks>
/// 实参是值时绑定成参数，值先转义再拼上通配符，SQL 文本里只有参数名与 <c>ESCAPE '!'</c>，不拼接实参；投影与子查询同样经过它；在真实库上验证过的五种数据库之外沿用 SqlSugar 原来的翻译；应用自己登记的同名函数优先；集合的 <c>Contains</c>（<c>IN</c>）不受影响
/// </remarks>
public sealed class SqlSugarLiteralMatchingTranslationTests
{
    /// <summary>
    /// 带全部特殊字符的实参
    /// </summary>
    private const string Keyword = "t_2[!%'";

    [Theory]
    [InlineData(DbType.Sqlite, "t!_2[!!!%'")]
    [InlineData(DbType.PostgreSQL, "t!_2[!!!%'")]
    [InlineData(DbType.MySql, "t!_2[!!!%'")]
    [InlineData(DbType.Oracle, "t!_2[!!!%'")]
    [InlineData(DbType.SqlServer, "t!_2![!!!%'")]
    public void VerifiedDatabases_BindAnEscapedPattern_AndWriteTheEscapeClause(DbType dbType, string escaped)
    {
        using var client = Client(dbType);
        var keyword = Keyword;

        var contains = client.Queryable<LiteralMatchRecord>().Where(r => r.Name.Contains(keyword)).ToSql();
        var starts = client.Queryable<LiteralMatchRecord>().Where(r => r.Name.StartsWith(keyword)).ToSql();
        var ends = client.Queryable<LiteralMatchRecord>().Where(r => r.Name.EndsWith(keyword)).ToSql();

        foreach (var (sql, parameters) in new[] { contains, starts, ends })
        {
            Assert.Contains("ESCAPE '!'", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("t_2", sql, StringComparison.Ordinal);
            Assert.Single(parameters);
        }

        Assert.Equal($"%{escaped}%", contains.Value[0].Value);
        Assert.Equal($"{escaped}%", starts.Value[0].Value);
        Assert.Equal($"%{escaped}", ends.Value[0].Value);
    }

    [Theory]
    [InlineData(DbType.Dm)]
    [InlineData(DbType.Kdbndp)]
    public void UnverifiedDatabases_KeepSqlSugarsTranslation(DbType dbType)
    {
        using var client = Client(dbType);
        var keyword = Keyword;

        var (sql, parameters) = client.Queryable<LiteralMatchRecord>().Where(r => r.Name.Contains(keyword)).ToSql();

        Assert.DoesNotContain("ESCAPE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Keyword, Assert.Single(parameters).Value);
    }

    [Fact]
    public void ApplicationsOwnFunctionOfTheSameName_TakesPrecedence()
    {
        var config = new ConnectionConfig
        {
            DbType = DbType.Sqlite,
            ConnectionString = "Data Source=translation.db",
            ConfigureExternalServices = new ConfigureExternalServices
            {
                SqlFuncServices = [new SqlFuncExternal { UniqueMethodName = "Contains", MethodValue = static (model, _, _) => $" (instr({model.Args[0].MemberName}, {model.Args[1].MemberName}) > 0) " }],
            },
        };
        using var client = new SqlSugarClient(SqlSugarConventions.Apply(config));
        var keyword = Keyword;

        Assert.Contains("instr(", client.Queryable<LiteralMatchRecord>().Where(r => r.Name.Contains(keyword)).ToSql().Key, StringComparison.Ordinal);
        Assert.Contains("ESCAPE '!'", client.Queryable<LiteralMatchRecord>().Where(r => r.Name.StartsWith(keyword)).ToSql().Key, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_Twice_RegistersEachFunctionOnce()
    {
        var config = SqlSugarConventions.Apply(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = "Data Source=translation.db" }));

        Assert.Equal(["Contains", "EndsWith", "StartsWith"], config.ConfigureExternalServices.SqlFuncServices.Select(static f => f.UniqueMethodName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EnableILike_IsHonored_OnPostgreSql()
    {
        var config = new ConnectionConfig { DbType = DbType.PostgreSQL, ConnectionString = "Host=127.0.0.1;Database=translation", MoreSettings = new ConnMoreSettings { EnableILike = true } };
        using var client = new SqlSugarClient(SqlSugarConventions.Apply(config));
        var keyword = Keyword;

        var sql = client.Queryable<LiteralMatchRecord>().Where(r => r.Name.Contains(keyword)).ToSql().Key;

        Assert.Contains(" ilike ", sql, StringComparison.Ordinal);
        Assert.Contains("ESCAPE '!'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectionContains_StillTranslatesToIn()
    {
        using var client = Client(DbType.PostgreSQL);
        List<string> names = ["a_b", "c%d"];

        var sql = client.Queryable<LiteralMatchRecord>().Where(r => names.Contains(r.Name)).ToSql().Key;

        Assert.Contains(" IN ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ESCAPE", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectedMatch_IsTranslatedTheSameWay()
    {
        using var client = Client(DbType.SqlServer);
        var keyword = Keyword;

        var (sql, parameters) = client.Queryable<LiteralMatchRecord>().Select(r => new { r.Name, Hit = r.Name.Contains(keyword) }).ToSql();

        Assert.Contains("ESCAPE '!'", sql, StringComparison.Ordinal);
        Assert.Equal("%t!_2![!!!%'%", Assert.Single(parameters).Value);
    }

    [Fact]
    public void SubqueryMatch_IsTranslatedTheSameWay()
    {
        using var client = Client(DbType.PostgreSQL);
        var keyword = Keyword;

        var (sql, parameters) = client.Queryable<LiteralMatchRecord>().Where(r => SqlFunc.Subqueryable<LiteralMatchRecord>().Where(s => s.Fragment == r.Name && s.Name.Contains(keyword)).Any()).ToSql();

        Assert.Contains("ESCAPE '!'", sql, StringComparison.Ordinal);
        Assert.Equal("%t!_2[!!!%'%", Assert.Single(parameters).Value);
    }

    /// <summary>
    /// 施加了约定、不连库的客户端
    /// </summary>
    /// <remarks>
    /// 只用来生成 SQL，连接串不会被打开
    /// </remarks>
    private static SqlSugarClient Client(DbType dbType) => new(SqlSugarConventions.Apply(new ConnectionConfig
    {
        DbType = dbType,
        ConnectionString = dbType switch
        {
            DbType.Sqlite => "Data Source=translation.db",
            DbType.Oracle or DbType.Dm => "Data Source=127.0.0.1/translation;User Id=u;Password=p",
            DbType.SqlServer or DbType.MySql or DbType.Kdbndp => "Server=127.0.0.1;Database=translation",
            _ => "Host=127.0.0.1;Database=translation",
        },
    }));
}
