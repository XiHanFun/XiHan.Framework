// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Data.Sqlite;
using SqlSugar;
using XiHan.Framework.Localization.Database.Translations;
using XiHan.Framework.Persistence.SqlSugar;

namespace XiHan.Framework.Localization.Database.Tests;

/// <summary>
/// 翻译表的唯一索引（ADR-0072）。
/// </summary>
/// <remarks>
/// <c>InitTables</c> 在 SQLite 上建出键与区域的唯一索引，同一键同一区域的第二行被拒绝。
/// </remarks>
public sealed class MessageTranslationIndexTests : IDisposable
{
    /// <summary>
    /// 列出一张表上显式建出的索引：<c>名字 [unique] 列,列</c>，按名字排序。
    /// </summary>
    private const string IndexQuery = """
        SELECT m.name
            || CASE WHEN (SELECT l."unique" FROM pragma_index_list(m.tbl_name) AS l WHERE l.name = m.name) = 1 THEN ' unique ' ELSE ' ' END
            || (SELECT group_concat(i.name, ',') FROM (SELECT name FROM pragma_index_info(m.name) ORDER BY seqno) AS i)
        FROM sqlite_master AS m
        WHERE m.type = 'index' AND m.tbl_name = @table AND m.sql IS NOT NULL
        ORDER BY m.name
        """;

    /// <summary>
    /// 本测试独占的临时数据库文件。
    /// </summary>
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"xihan-localization-index-{Guid.NewGuid():N}.db");

    /// <summary>
    /// 清空连接池后删除数据库文件。
    /// </summary>
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }

    [Fact]
    public void InitTables_CreatesTheUniqueKeyAndLocaleIndex_ThatRejectsADuplicateTranslation()
    {
        using var client = new SqlSugarClient(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = $"Data Source={_path}", IsAutoCloseConnection = true }));
        client.CodeFirst.InitTables<MessageTranslation>();

        Assert.Equal(
            ["UX_MessageTranslation_Ke_Lo unique Key,Locale"],
            client.Ado.SqlQuery<string>(IndexQuery, new { table = nameof(MessageTranslation) }));

        client.Insertable(Translation("zh-CN")).ExecuteCommand();
        client.Insertable(Translation("ja-JP")).ExecuteCommand();
        Assert.ThrowsAny<Exception>(() => client.Insertable(Translation("zh-CN")).ExecuteCommand());
    }

    /// <summary>
    /// 键 <c>orders.not_found</c> 在一个区域里的翻译。
    /// </summary>
    private static MessageTranslation Translation(string locale)
        => new() { Id = Guid.CreateVersion7(), Key = "orders.not_found", Locale = locale, Template = "{id}" };
}
